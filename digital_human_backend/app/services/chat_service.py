"""
聊天服务（路由 + 业务逻辑）
Chat service (routes + service logic)
"""

from typing import Dict, Optional, List, Any
import logging
import uuid
from datetime import datetime

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.brain.llm.ollama_chat import OllamaChat
from app.brain.llm.exceptions import LLMRequestError
from app.brain.memory.context_builder import ContextBuilder
from app.brain.memory.knowledge_retriever import KnowledgeRetriever
from app.brain.memory.mem0_store import Mem0Store
from app.brain.memory.neo4j_doc_ingest import (
    get_neo4j_graph_db_optional,
    ingest_chat_turn_to_neo4j,
)
from app.shared.config import Config
from app.shared.runtime_config_store import RuntimeConfigStore
from app.shared.utils import format_response
from app.services.knowledge_repository import get_kb

logger = logging.getLogger(__name__)
chat_router = APIRouter()

_MAX_STORED_SESSION_MESSAGES = 40


def _normalize_model_name(name: str) -> str:
    return (name or "").strip().lower()


def _pick_first_match(models: List[str], keywords: List[str]) -> Optional[str]:
    """
    在可用模型列表中，按关键字优先级挑选第一个匹配。
    匹配规则：name(小写)包含 keyword(小写)。
    """
    if not models:
        return None
    lowered = [m for m in models if isinstance(m, str) and m.strip()]
    if not lowered:
        return None
    for kw in keywords:
        kw_n = _normalize_model_name(kw)
        if not kw_n:
            continue
        for m in lowered:
            if kw_n in _normalize_model_name(m):
                return m
    return None


def _validate_session_id(session_id: Optional[str]) -> Optional[str]:
    if not session_id:
        return None
    cleaned = session_id.strip()
    if not cleaned:
        return None
    try:
        uuid.UUID(cleaned)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="session_id 格式无效") from exc
    return cleaned


def _validate_message_length(message: str, field_name: str = "message") -> str:
    cleaned = (message or "").strip()
    if not cleaned:
        raise HTTPException(status_code=400, detail=f"{field_name} 不能为空")
    if len(cleaned) > Config.LLM_MAX_MESSAGE_CHARS:
        raise HTTPException(
            status_code=400,
            detail=f"{field_name} 过长（上限 {Config.LLM_MAX_MESSAGE_CHARS} 字符）",
        )
    return cleaned


def _trim_openai_history(messages: List[Dict[str, str]]) -> List[Dict[str, str]]:
    max_msgs = Config.LLM_MAX_HISTORY_TURNS * 2
    if max_msgs <= 0 or len(messages) <= max_msgs:
        return messages
    return messages[-max_msgs:]


def _truncate_for_llm(text: str, max_chars: int) -> str:
    if len(text) <= max_chars:
        return text
    return text[: max_chars - 3] + "..."


def _build_openai_user_content(
    message: str,
    full_system_prompt: str,
    is_first_turn: bool,
) -> str:
    if is_first_turn and full_system_prompt:
        combined = f"{full_system_prompt}\n\n{message}"
    elif full_system_prompt:
        # 后续轮次仅附带精简提示，避免每轮重复完整 RAG 导致 token 爆炸
        combined = f"【参考上下文】\n{full_system_prompt}\n\n{message}"
    else:
        combined = message
    return _truncate_for_llm(combined, Config.LLM_MAX_INPUT_CHARS)


class ChatRequest(BaseModel):
    """聊天请求模型"""
    message: str = Field(..., min_length=1, max_length=2000, description="用户消息")
    user_id: Optional[str] = Field(None, description="用户ID")
    session_id: Optional[str] = Field(None, description="会话ID")
    system_prompt: Optional[str] = Field(None, max_length=4000, description="系统提示词")
    model_key: Optional[str] = Field(
        None,
        description="模型选择键（来自 Unity：Chatglm / Qwen）。不传则使用默认模型。",
    )
    memory_profile: Optional[int] = Field(
        0,
        description="0=不使用长期记忆, 1=Memory1, 2=Memory2, 3=Memory3",
    )
    use_personal_memory: bool = Field(
        True,
        description="True=私人对话/用户长期记忆(mem0)；False=面向大众问答，仅用知识库/RAG，不写mem0"
    )
    kb_id: Optional[str] = Field(
        None,
        description="本地 /api/chat（Ollama）对话时绑定的知识库 ID；"
        "不传则使用服务端配置的默认知识库（Config.DEFAULT_CHAT_KB_ID，可由环境变量 DEFAULT_CHAT_KB_ID 覆盖）。"
        "Unity 走 Coze 直连时不使用本接口，无需传此字段。",
    )


class ChatResponse(BaseModel):
    """聊天响应模型"""
    response: str = Field(..., description="AI回复")
    user_message: str = Field(..., description="用户消息")
    session_id: Optional[str] = Field(None, description="会话ID")


class ChatService:
    """聊天服务类"""

    def __init__(self):
        """初始化聊天服务"""
        self.ollama_chat = None
        self.memory_store = None
        self.knowledge_retriever = None
        self.context_builder = None

        if self.use_openai:
            try:
                RuntimeConfigStore.validate_llm_config()
            except ValueError as exc:
                logger.warning(
                    "OpenAI 配置不完整，请通过 Qt 管理台或 .env 补全后再对话: %s",
                    exc,
                )
            cfg = RuntimeConfigStore.load()
            logger.info(
                "聊天服务初始化（OpenAI 纯转发: %s, model=%s，已跳过 RAG/mem0）",
                cfg.llm_base_url,
                cfg.llm_default_model,
            )
        else:
            self.ollama_chat = OllamaChat(
                base_url=Config.OLLAMA_BASE_URL,
                model=Config.OLLAMA_DEFAULT_MODEL,
            )
            self.memory_store = Mem0Store()
            self.knowledge_retriever = KnowledgeRetriever()
            self.context_builder = ContextBuilder(
                memory_manager=self.memory_store,
                knowledge_retriever=self.knowledge_retriever,
            )
            logger.info("聊天服务初始化完成（Ollama 本地模型 + 知识检索）")

        self.sessions: Dict[str, Dict] = {}

    @property
    def use_openai(self) -> bool:
        return RuntimeConfigStore.use_openai_llm()

    async def _chat_openai_relay(
        self,
        message: str,
        user_id: Optional[str],
        session_id: str,
        system_prompt: Optional[str] = None,
    ) -> Dict:
        """纯转发模式：Unity → 后端 → 学校 OpenAI 兼容网关，不做 RAG/mem0。"""
        RuntimeConfigStore.validate_llm_config()

        full_system_prompt = (system_prompt or "").strip() or (
            "你是图书馆数字人助手，请简洁友好地回答用户问题。"
        )

        preview = message.strip().replace("\n", " ")
        if len(preview) > 80:
            preview = preview[:80] + "..."
        logger.info(
            "ChatService._chat_openai_relay session_id=%s user_id=%s message_preview=%s",
            session_id,
            user_id or "(empty)",
            preview,
        )

        if (user_id or "").startswith("unity"):
            logger.info(
                "Unity 输入内容: session_id=%s user_id=%s message=%s",
                session_id,
                user_id or "(empty)",
                message.replace("\n", " "),
            )

        history = self.sessions[session_id]["messages"]
        trimmed_history = _trim_openai_history([
            {"role": m["role"], "content": m["content"]}
            for m in history
            if m.get("role") in ("user", "assistant") and m.get("content")
        ])
        llm_messages: List[Dict[str, str]] = list(trimmed_history)
        is_first_turn = len(history) == 0
        user_content = _build_openai_user_content(
            message,
            full_system_prompt,
            is_first_turn,
        )
        llm_messages.append({"role": "user", "content": user_content})

        cfg = RuntimeConfigStore.load()
        client = RuntimeConfigStore.create_openai_client()
        response = await client.chat_messages(
            llm_messages,
            model=cfg.llm_default_model,
        )

        if (user_id or "").startswith("unity"):
            logger.info(
                "Unity 模型输出: session_id=%s user_id=%s response=%s",
                session_id,
                user_id or "(empty)",
                response.replace("\n", " "),
            )

        self.sessions[session_id]["messages"].append({
            "role": "user",
            "content": message,
            "timestamp": datetime.now().isoformat(),
        })
        self.sessions[session_id]["messages"].append({
            "role": "assistant",
            "content": response,
            "timestamp": datetime.now().isoformat(),
        })
        if len(self.sessions[session_id]["messages"]) > _MAX_STORED_SESSION_MESSAGES:
            self.sessions[session_id]["messages"] = (
                self.sessions[session_id]["messages"][-_MAX_STORED_SESSION_MESSAGES:]
            )

        return {
            "response": response,
            "session_id": session_id,
        }

    async def chat(
        self,
        message: str,
        user_id: Optional[str] = None,
        session_id: Optional[str] = None,
        system_prompt: Optional[str] = None,
        use_personal_memory: bool = True,
        model_key: Optional[str] = None,
        memory_profile: Optional[int] = 0,
        kb_id: Optional[str] = None,
        kb_name: Optional[str] = None,
    ) -> Dict:
        """
        处理聊天请求。
        use_personal_memory=True：私人对话，使用 mem0 长期记忆并写入；
        use_personal_memory=False：面向大众问答，仅用知识库/RAG，不写入 mem0。
        kb_id：已在路由层校验存在时传入；配合 kb_name 用于检索与系统提示（仅本地模型路径）。
        """
        try:
            if not session_id:
                session_id = str(uuid.uuid4())

            if session_id in self.sessions:
                stored_user = self.sessions[session_id].get("user_id") or "unity_user"
                request_user = user_id or "unity_user"
                if stored_user != request_user:
                    raise HTTPException(status_code=403, detail="无权访问此会话")

            if session_id not in self.sessions:
                self.sessions[session_id] = {
                    "user_id": user_id or "unity_user",
                    "created_at": datetime.now().isoformat(),
                    "messages": []
                }

            # OpenAI 网关纯转发：不检索知识库、不写 mem0/Neo4j
            if self.use_openai:
                return await self._chat_openai_relay(
                    message=message,
                    user_id=user_id,
                    session_id=session_id,
                    system_prompt=system_prompt,
                )

            # 依据 memory_profile 映射实际用于长期记忆的 user_id（实现 Memory1/2/3）
            effective_user_id = user_id or "unity_user"
            profile = memory_profile or 0
            if profile == 0:
                if use_personal_memory:
                    logger.info(
                        "ChatService.chat: memory_profile=0, 强制关闭个人长期记忆（仅用知识库/RAG）。"
                    )
                use_personal_memory = False
            elif profile == 1:
                effective_user_id = f"{effective_user_id}_mem1"
            elif profile == 2:
                effective_user_id = f"{effective_user_id}_mem2"
            elif profile == 3:
                effective_user_id = f"{effective_user_id}_mem3"
            else:
                logger.warning(
                    "ChatService.chat: 收到未知 memory_profile=%s，将视为 0（不写个人记忆）。",
                    profile,
                )
                use_personal_memory = False

            context = await self.context_builder.build_context_async(
                query=message,
                session_id=session_id,
                user_id=effective_user_id,
                use_personal_memory=use_personal_memory,
                kb_id=kb_id,
                kb_name=kb_name,
            )

            default_base = (
                "你是一个知识丰富、友好的 AI 助手，可以结合用户长期记忆（如有）和知识库文档，"
                "为用户提供有帮助、可信赖的回答。"
            )
            if system_prompt:
                full_system_prompt = system_prompt
                if kb_id and kb_name:
                    full_system_prompt += (
                        f"\n\n【知识库绑定】当前为本地模型对话，已绑定知识库「{kb_name}」。"
                        "请优先根据下方「相关上下文」中来自该库的检索片段回答；"
                        "事实与设定以片段为准，不足时请如实说明。"
                    )
            elif kb_id and kb_name:
                full_system_prompt = (
                    f"你是一个知识丰富、友好的 AI 助手。当前使用本地模型，并已绑定知识库「{kb_name}」。"
                    "请优先依据系统消息中的「相关上下文」里来自该知识库的检索片段作答；"
                    "若片段不足以回答，请说明信息不足，再可作谨慎、简短的合理推断。"
                    "若本轮启用了个人长期记忆（Memory1/2/3 等），可结合用户偏好，但事实与剧情仍以该知识库检索内容为准。"
                )
            else:
                full_system_prompt = default_base

            if context:
                full_system_prompt += f"\n\n相关上下文：{context}"

            model_key_normalized = (model_key or "").strip().lower()
            if model_key_normalized in ["chatglm", "chatglm3", "glm"]:
                chosen_model = Config.OLLAMA_MODEL_CHATGLM
            elif model_key_normalized in ["qwen", "qwen2", "qwen2.5"]:
                chosen_model = Config.OLLAMA_MODEL_QWEN
            elif model_key_normalized:
                chosen_model = model_key.strip()
            else:
                chosen_model = Config.OLLAMA_DEFAULT_MODEL

            if not self.use_openai:
                try:
                    available = await self.ollama_chat.get_available_models()
                    available_set = {_normalize_model_name(m) for m in available}
                    if _normalize_model_name(chosen_model) not in available_set:
                        fallback = None
                        if model_key_normalized in ["chatglm", "chatglm3", "glm"]:
                            fallback = _pick_first_match(
                                available,
                                keywords=["chatglm", "glm", "entropy", "chatglm3"],
                            )
                        elif model_key_normalized in ["qwen", "qwen2", "qwen2.5"]:
                            fallback = _pick_first_match(
                                available,
                                keywords=["qwen3", "qwen2.5", "qwen2", "qwen"],
                            )
                        else:
                            fallback = _pick_first_match(
                                available, keywords=[model_key_normalized]
                            ) if model_key_normalized else None
                            if not fallback and available:
                                fallback = available[0]

                        if fallback:
                            logger.warning(
                                "ChatService.chat: chosen_model '%s' not in available models; fallback to '%s'.",
                                chosen_model,
                                fallback,
                            )
                            chosen_model = fallback
                        else:
                            logger.warning(
                                "ChatService.chat: chosen_model '%s' not in available models and no fallback found; will try anyway.",
                                chosen_model,
                            )
                except Exception as e:
                    logger.warning(
                        "ChatService.chat: failed to validate/resolve model (chosen_model=%s): %s",
                        chosen_model,
                        e,
                    )
            preview = message.strip().replace("\n", " ")
            if len(preview) > 80:
                preview = preview[:80] + "..."
            logger.info(
                "ChatService.chat session_id=%s user_id=%s effective_user_id=%s "
                "model_key=%s chosen_model=%s memory_profile=%s kb_id=%s message_preview=%s",
                session_id,
                user_id or "(empty)",
                effective_user_id,
                model_key or "(empty)",
                chosen_model,
                profile,
                kb_id or "(none)",
                preview,
            )

            # 额外记录 Unity 侧完整输入（方便在 Qt 日志中排查问题）
            if (user_id or "").startswith("unity"):
                logger.info(
                    "Unity 输入内容: session_id=%s user_id=%s message=%s",
                    session_id,
                    user_id or "(empty)",
                    message.replace("\n", " "),
                )

            response = await self.ollama_chat.chat(
                message=message,
                system_prompt=full_system_prompt,
                model=chosen_model,
            )

            # 额外记录 Unity 侧模型完整输出
            if (user_id or "").startswith("unity"):
                logger.info(
                    "Unity 模型输出: session_id=%s user_id=%s response=%s",
                    session_id,
                    user_id or "(empty)",
                    response.replace("\n", " "),
                )

            self.sessions[session_id]["messages"].append({
                "role": "user",
                "content": message,
                "timestamp": datetime.now().isoformat()
            })
            self.sessions[session_id]["messages"].append({
                "role": "assistant",
                "content": response,
                "timestamp": datetime.now().isoformat()
            })
            if len(self.sessions[session_id]["messages"]) > _MAX_STORED_SESSION_MESSAGES:
                self.sessions[session_id]["messages"] = (
                    self.sessions[session_id]["messages"][-_MAX_STORED_SESSION_MESSAGES:]
                )

            # 用户对话写入 Neo4j（与文档解析共用 Character/Dialogue/Chapter，角色为「用户」「助手」）
            graph_db = get_neo4j_graph_db_optional()
            if graph_db:
                try:
                    turn_index = len(self.sessions[session_id]["messages"]) // 2
                    ingest_chat_turn_to_neo4j(
                        session_id=session_id,
                        user_id=effective_user_id,
                        user_message=message,
                        assistant_message=response,
                        turn_index=turn_index,
                        graph_db=graph_db,
                    )
                finally:
                    graph_db.close()

            # 仅私人对话模式写入长期记忆（mem0）；大众问答模式不写入
            if use_personal_memory:
                logger.info(
                    "ChatService.chat: 写入长期记忆 user_id=%s, session_id=%s, profile=%s",
                    effective_user_id,
                    session_id,
                    profile,
                )
                self.memory_store.add_conversation_memory(
                    user_id=effective_user_id,
                    session_id=session_id,
                    user_message=message,
                    assistant_message=response,
                )
            else:
                logger.debug(
                    "ChatService.chat: 本轮对话未写入长期记忆（use_personal_memory=%s, profile=%s）。",
                    use_personal_memory,
                    profile,
                )

            return {
                "response": response,
                "session_id": session_id
            }

        except HTTPException:
            raise
        except LLMRequestError as e:
            logger.error("LLM 请求失败: %s", e.message)
            raise HTTPException(status_code=e.status_code, detail=e.message) from e
        except Exception as e:
            logger.error(f"聊天处理失败: {e}")
            raise

    async def get_history(self, session_id: str, limit: int = 20) -> List[Dict]:
        """获取聊天历史"""
        if session_id not in self.sessions:
            return []
        messages = self.sessions[session_id]["messages"]
        return messages[-limit:] if len(messages) > limit else messages

    async def clear_history(self, session_id: str):
        """清除聊天历史"""
        if session_id in self.sessions:
            self.sessions[session_id]["messages"] = []
            logger.info(f"会话 {session_id} 的历史已清除")

    async def get_available_models(self) -> List[str]:
        """获取可用模型列表"""
        if self.use_openai:
            cfg = RuntimeConfigStore.load()
            return [cfg.llm_default_model]
        return await self.ollama_chat.get_available_models()


chat_service = ChatService()


@chat_router.post("/", response_model=ChatResponse)
async def chat_endpoint(request: ChatRequest):
    """聊天接口"""
    try:
        message = _validate_message_length(request.message)
        session_id = _validate_session_id(request.session_id)

        kb_id_clean = (request.kb_id or "").strip() or None
        kb_meta = None
        if not chat_service.use_openai:
            if not kb_id_clean and Config.DEFAULT_CHAT_KB_ID:
                kb_id_clean = Config.DEFAULT_CHAT_KB_ID
            if kb_id_clean:
                kb_meta = get_kb(kb_id_clean)
                if not kb_meta:
                    raise HTTPException(status_code=404, detail="知识库不存在")

        # 若来自 Unity（例如默认 user_id=unity_user），在日志中额外标记，方便在 Qt 日志面板中观察 Unity 连接情况
        if (request.user_id or "").startswith("unity"):
            logger.info(
                "Unity 客户端已连接后端: user_id=%s session_id=%s model_key=%s",
                request.user_id or "(empty)",
                request.session_id or "(new)",
                request.model_key or "(empty)",
            )
        logger.info(
            "POST /api/chat user_id=%s session_id=%s model_key=%s memory_profile=%s "
            "use_personal_memory=%s kb_id=%s",
            request.user_id or "(empty)",
            request.session_id or "(new)",
            request.model_key or "(empty)",
            request.memory_profile or 0,
            request.use_personal_memory,
            kb_id_clean or "(none)",
        )
        result = await chat_service.chat(
            message=message,
            user_id=request.user_id,
            session_id=session_id,
            system_prompt=request.system_prompt,
            use_personal_memory=request.use_personal_memory,
            model_key=request.model_key,
            memory_profile=request.memory_profile,
            kb_id=kb_id_clean,
            kb_name=kb_meta.name if kb_meta else None,
        )
        return ChatResponse(
            response=result["response"],
            user_message=message,
            session_id=result.get("session_id")
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"聊天请求处理失败: {e}")
        raise HTTPException(status_code=500, detail="处理请求时出错，请稍后重试")


@chat_router.get("/history/{session_id}")
async def get_chat_history(session_id: str, limit: int = 20):
    """获取聊天历史"""
    try:
        session_id = _validate_session_id(session_id)
        if not session_id:
            raise HTTPException(status_code=400, detail="session_id 不能为空")
        limit = max(1, min(limit, 100))
        history = await chat_service.get_history(session_id, limit)
        return format_response(history, success=True)
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"获取聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail="获取聊天历史失败")


@chat_router.delete("/history/{session_id}")
async def clear_chat_history(session_id: str):
    """清除聊天历史"""
    try:
        session_id = _validate_session_id(session_id)
        if not session_id:
            raise HTTPException(status_code=400, detail="session_id 不能为空")
        await chat_service.clear_history(session_id)
        return format_response({"session_id": session_id}, success=True, message="历史已清除")
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"清除聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail="清除聊天历史失败")


@chat_router.get("/models")
async def get_available_models():
    """获取可用模型列表"""
    try:
        models = await chat_service.get_available_models()
        return format_response(models, success=True)
    except Exception as e:
        logger.error(f"获取模型列表失败: {e}")
        raise HTTPException(status_code=500, detail="获取模型列表失败")
