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
from app.brain.memory.context_builder import ContextBuilder
from app.brain.memory.knowledge_retriever import KnowledgeRetriever
from app.brain.memory.mem0_store import Mem0Store
from app.brain.memory.neo4j_doc_ingest import (
    get_neo4j_graph_db_optional,
    ingest_chat_turn_to_neo4j,
)
from app.shared.config import Config
from app.shared.utils import format_response
from app.services.knowledge_repository import get_kb

logger = logging.getLogger(__name__)
chat_router = APIRouter()

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


class ChatRequest(BaseModel):
    """聊天请求模型"""
    message: str = Field(..., description="用户消息")
    user_id: Optional[str] = Field(None, description="用户ID")
    session_id: Optional[str] = Field(None, description="会话ID")
    system_prompt: Optional[str] = Field(None, description="系统提示词")
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
        self.ollama_chat = OllamaChat(
            base_url=Config.OLLAMA_BASE_URL,
            model=Config.OLLAMA_DEFAULT_MODEL
        )
        # 使用 Mem0Store 作为长期对话记忆的后端（内部优先 mem0，失败时回退到 MemoryManager）
        self.memory_store = Mem0Store()
        self.knowledge_retriever = KnowledgeRetriever()
        self.context_builder = ContextBuilder(
            memory_manager=self.memory_store,
            knowledge_retriever=self.knowledge_retriever
        )
        self.sessions: Dict[str, Dict] = {}
        logger.info("聊天服务初始化完成（已集成知识检索系统）")

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

            if session_id not in self.sessions:
                self.sessions[session_id] = {
                    "user_id": user_id,
                    "created_at": datetime.now().isoformat(),
                    "messages": []
                }

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
                # 允许直接传入 Ollama tag（高级用法）
                chosen_model = model_key.strip()
            else:
                chosen_model = Config.OLLAMA_DEFAULT_MODEL

            # 兜底：若 chosen_model 在本机 Ollama 不存在，则从 /api/tags 自动挑一个最匹配的
            # 这能避免默认配置指向不存在的模型导致 404（例如 qwen2.5:3b 不在本地 tags 中）。
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
                        # 未知 key：优先尝试包含 key 的模型，否则用第一个可用模型
                        fallback = _pick_first_match(available, keywords=[model_key_normalized]) if model_key_normalized else None
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
        return await self.ollama_chat.get_available_models()


chat_service = ChatService()


@chat_router.post("/", response_model=ChatResponse)
async def chat_endpoint(request: ChatRequest):
    """聊天接口"""
    try:
        if not request.message or not request.message.strip():
            raise HTTPException(status_code=400, detail="消息不能为空")

        kb_id_clean = (request.kb_id or "").strip() or None
        if not kb_id_clean and Config.DEFAULT_CHAT_KB_ID:
            kb_id_clean = Config.DEFAULT_CHAT_KB_ID

        kb_meta = None
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
            message=request.message,
            user_id=request.user_id,
            session_id=request.session_id,
            system_prompt=request.system_prompt,
            use_personal_memory=request.use_personal_memory,
            model_key=request.model_key,
            memory_profile=request.memory_profile,
            kb_id=kb_id_clean,
            kb_name=kb_meta.name if kb_meta else None,
        )
        return ChatResponse(
            response=result["response"],
            user_message=request.message,
            session_id=result.get("session_id")
        )
    except Exception as e:
        logger.error(f"聊天请求处理失败: {e}")
        raise HTTPException(status_code=500, detail=f"处理请求时出错: {str(e)}")


@chat_router.get("/history/{session_id}")
async def get_chat_history(session_id: str, limit: int = 20):
    """获取聊天历史"""
    try:
        history = await chat_service.get_history(session_id, limit)
        return format_response(history, success=True)
    except Exception as e:
        logger.error(f"获取聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@chat_router.delete("/history/{session_id}")
async def clear_chat_history(session_id: str):
    """清除聊天历史"""
    try:
        await chat_service.clear_history(session_id)
        return format_response({"session_id": session_id}, success=True, message="历史已清除")
    except Exception as e:
        logger.error(f"清除聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@chat_router.get("/models")
async def get_available_models():
    """获取可用模型列表"""
    try:
        models = await chat_service.get_available_models()
        return format_response(models, success=True)
    except Exception as e:
        logger.error(f"获取模型列表失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))
