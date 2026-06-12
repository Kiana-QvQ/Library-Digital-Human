"""
mem0 封装层：用于替换本地 MemoryManager，作为“长期对话记忆”后端。

设计目标：
- 对上提供与 MemoryManager 类似的接口：add_memory/search_memories；
- 内部优先使用 mem0.Memory，自身不可用时回退到 MemoryManager。
"""

from __future__ import annotations

from typing import Dict, List, Optional
import logging
import os
from pathlib import Path
import tempfile

from app.brain.memory.memory_manager import MemoryManager
from app.shared.config import Config

logger = logging.getLogger(__name__)

try:
    # 直接使用 pip 安装的 mem0ai 包；其图存储/向量存储配置由环境变量或外部配置决定
    from mem0 import Memory as Mem0Memory  # type: ignore
    from mem0.configs.base import MemoryConfig
    from mem0.configs.vector_stores.qdrant import QdrantConfig
    from mem0.llms.configs import LlmConfig
    from mem0.embeddings.configs import EmbedderConfig
    from mem0.vector_stores.configs import VectorStoreConfig
except Exception:  # pragma: no cover - 环境未安装 mem0 时触发
    Mem0Memory = None  # type: ignore[misc,assignment]


class Mem0Store:
    """统一的长期记忆存储接口，优先使用 mem0，失败时回退到 MemoryManager。"""

    def __init__(self) -> None:
        self._fallback = MemoryManager()
        self._mem0: Optional[Mem0Memory] = None  # type: ignore[type-arg]

        # 允许通过环境变量完全关闭 mem0，避免 /tmp/qdrant.lock 之类的问题
        disable_mem0 = os.getenv("DISABLE_MEM0", "false").lower() == "true"

        if disable_mem0:
            logger.warning("检测到 DISABLE_MEM0=true，将直接使用本地 MemoryManager，跳过 mem0 初始化。")
            return

        if Mem0Memory is None:
            logger.warning("mem0 未安装，将使用本地 MemoryManager 作为记忆后端。")
            return

        # 优先使用 Docker 方式运行的远程 Qdrant，避免本地嵌入式 Qdrant 的 .lock 冲突
        # 注意：mem0==1.0.3 的 QdrantConfig 校验对 (url, api_key) 较严格；本项目优先走 host/port 方案。
        qdrant_host = (Config.MEM0_QDRANT_HOST or "").strip() or "localhost"
        qdrant_port = int(getattr(Config, "MEM0_QDRANT_PORT", 6333) or 6333)
        qdrant_collection = (Config.MEM0_QDRANT_COLLECTION or "").strip() or "mem0"
        ollama_base_url = (Config.MEM0_OLLAMA_BASE_URL or "").strip() or Config.OLLAMA_BASE_URL
        ollama_llm_model = (Config.MEM0_OLLAMA_LLM_MODEL or "").strip() or "qwen3:latest"
        ollama_embed_model = (Config.MEM0_OLLAMA_EMBED_MODEL or "").strip() or "nomic-embed-text:latest"
        embedding_dims = int(getattr(Config, "MEM0_EMBEDDING_DIMS", 768) or 768)

        try:
            # 仍保留项目级临时目录：即使未来误走到本地模式，也尽量避开系统 /tmp
            project_root = Path(__file__).resolve().parents[3]
            project_tmp = (project_root / "data" / "qdrant_mem0").resolve()
            project_tmp.mkdir(parents=True, exist_ok=True)

            tempfile.tempdir = str(project_tmp)
            os.environ.setdefault("QDRANT_STORAGE_PATH", str(project_tmp))
            os.environ.setdefault("QDRANT__STORAGE__PATH", str(project_tmp))

            cfg = MemoryConfig()
            cfg.vector_store = VectorStoreConfig(
                provider="qdrant",
                config=QdrantConfig(
                    collection_name=qdrant_collection,
                    embedding_model_dims=embedding_dims,
                    host=qdrant_host,
                    port=qdrant_port,
                    # 明确不走本地 path 模式
                    path=None,
                ).model_dump(exclude_none=True),
            )
            cfg.embedder = EmbedderConfig(
                provider="ollama",
                config={
                    "model": ollama_embed_model,
                    "embedding_dims": embedding_dims,
                    "ollama_base_url": ollama_base_url,
                },
            )
            cfg.llm = LlmConfig(
                provider="ollama",
                config={
                    "model": ollama_llm_model,
                    "ollama_base_url": ollama_base_url,
                    # 这里保持偏稳的生成配置，避免记忆抽取发散
                    "temperature": 0.1,
                    "max_tokens": 800,
                    "top_p": 0.9,
                    "top_k": 20,
                },
            )

            self._mem0 = Mem0Memory(config=cfg)  # type: ignore[call-arg]
            logger.info(
                "mem0 Memory 初始化成功，将用于长期对话记忆（qdrant=%s:%s, collection=%s, embed=%s, llm=%s）。",
                qdrant_host,
                qdrant_port,
                qdrant_collection,
                ollama_embed_model,
                ollama_llm_model,
            )
        except Exception as e:  # pragma: no cover
            err_msg = str(e)
            logger.warning(
                "初始化 mem0 Memory 失败，将回退到 MemoryManager: %s",
                err_msg,
            )
            self._mem0 = None

    # ---- 写入记忆 ----

    def add_conversation_memory(
        self,
        user_id: Optional[str],
        session_id: Optional[str],
        user_message: str,
        assistant_message: str,
    ) -> None:
        """
        添加一轮“用户-助手”对话记忆。
        """
        if self._mem0 is not None:
            try:
                uid = user_id or "anonymous_user"
                messages = [
                    {"role": "user", "content": user_message},
                    {"role": "assistant", "content": assistant_message},
                ]
                logger.debug(
                    "Mem0Store.add_conversation_memory: 使用 mem0 写入对话记忆 uid=%s, session_id=%s",
                    uid,
                    session_id,
                )
                # 使用 mem0 作为长期记忆后端，带上基础元数据，便于后续按类型/会话过滤
                self._mem0.add(  # type: ignore[call-arg,attr-defined]
                    messages,
                    user_id=uid,
                    metadata={
                        "source": "digital_human_chat",
                        "type": "chat_fact",
                        "session_id": str(session_id) if session_id else "",
                    },
                    infer=True,
                )
                logger.info(
                    "Mem0Store.add_conversation_memory: mem0 写入成功 uid=%s, session_id=%s",
                    uid,
                    session_id,
                )
                return
            except Exception as e:  # pragma: no cover
                logger.error("Mem0Store.add_conversation_memory: mem0.add 失败，退回到本地 MemoryManager: %s", e)

        # 回退路径：使用原有 MemoryManager
        logger.debug(
            "Mem0Store.add_conversation_memory: 使用 MemoryManager 回退写入，user_id=%s, session_id=%s",
            user_id,
            session_id,
        )
        content = f"用户: {user_message}\nAI: {assistant_message}"
        tags: List[str] = []
        if session_id:
            tags.append(session_id)
        self._fallback.add_memory(
            content=content,
            memory_type="episodic",
            importance=0.6,
            tags=tags,
            metadata={"user_id": user_id, "session_id": session_id},
        )

    # ---- 检索记忆 ----

    def search_memories(
        self,
        query: str,
        user_id: Optional[str] = None,
        limit: int = 5,
        min_importance: float = 0.0,
    ) -> List[Dict]:
        """
        检索与查询相关的长期记忆。

        返回结构与 MemoryManager.search_memories 相似：
        [
          {
            "id": ...,
            "content": ...,
            "relevance_score": float,
            ...
          },
          ...
        ]
        """
        if self._mem0 is not None:
            try:
                uid = user_id or "anonymous_user"
                res = self._mem0.search(  # type: ignore[call-arg,attr-defined]
                    query=query,
                    user_id=uid,
                    limit=limit,
                    # 仅检索聊天事实类记忆；如需扩展，可在调用层增加 filters 参数
                    filters={"type": "chat_fact"},
                )
                results = res.get("results") if isinstance(res, dict) else res
                out: List[Dict] = []
                for entry in results or []:
                    mem_text = entry.get("memory") or entry.get("content") or ""
                    score = entry.get("score", 1.0)
                    out.append(
                        {
                            "id": entry.get("id"),
                            "content": mem_text,
                            "relevance_score": float(score),
                        }
                    )
                return out
            except Exception as e:  # pragma: no cover
                logger.error(f"mem0.search 失败，退回到本地 MemoryManager: {e}")

        # 回退到原有 MemoryManager
        return self._fallback.search_memories(
            query=query,
            memory_type=None,
            limit=limit,
            min_importance=min_importance,
        )

