"""
聊天服务：Unity → 后端 → 学校 OpenAI 兼容网关（纯转发，无 RAG/数据库）
"""

from __future__ import annotations

import logging
import uuid
from datetime import datetime
from typing import Any, Dict, List, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.brain.llm.exceptions import LLMRequestError
from app.shared.config import Config
from app.shared.runtime_config_store import RuntimeConfigStore
from app.shared.utils import format_response

logger = logging.getLogger(__name__)
chat_router = APIRouter()

_MAX_STORED_SESSION_MESSAGES = 40


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
        combined = f"【参考上下文】\n{full_system_prompt}\n\n{message}"
    else:
        combined = message
    return _truncate_for_llm(combined, Config.LLM_MAX_INPUT_CHARS)


class ChatRequest(BaseModel):
    message: str = Field(..., min_length=1, max_length=2000, description="用户消息")
    user_id: Optional[str] = Field(None, description="用户ID")
    session_id: Optional[str] = Field(None, description="会话ID")
    system_prompt: Optional[str] = Field(None, max_length=4000, description="系统提示词")
    model_key: Optional[str] = Field(
        None,
        description="保留字段；转发模式下由 Qt/app_config 固定模型名",
    )
    memory_profile: Optional[int] = Field(0, description="保留字段，当前未使用")
    use_personal_memory: bool = Field(True, description="保留字段，当前未使用")
    kb_id: Optional[str] = Field(None, description="保留字段，当前未使用")


class ChatResponse(BaseModel):
    response: str = Field(..., description="AI回复")
    user_message: str = Field(..., description="用户消息")
    session_id: Optional[str] = Field(None, description="会话ID")


class ChatService:
    """纯转发聊天服务（内存会话，不写数据库）。"""

    def __init__(self) -> None:
        self.sessions: Dict[str, Dict[str, Any]] = {}
        try:
            RuntimeConfigStore.validate_llm_config()
            cfg = RuntimeConfigStore.load()
            logger.info(
                "聊天服务已就绪（纯转发: %s, model=%s）",
                cfg.llm_base_url,
                cfg.llm_default_model,
            )
        except ValueError as exc:
            logger.warning("大模型转发配置不完整，请在 Qt 或 .env 中设置 LLM: %s", exc)

    async def chat(
        self,
        message: str,
        user_id: Optional[str] = None,
        session_id: Optional[str] = None,
        system_prompt: Optional[str] = None,
        **_ignored: Any,
    ) -> Dict[str, Any]:
        RuntimeConfigStore.validate_llm_config()

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
                "messages": [],
            }

        full_system_prompt = (system_prompt or "").strip() or (
            "你是图书馆数字人助手，请简洁友好地回答用户问题。"
        )

        if (user_id or "").startswith("unity"):
            logger.info(
                "Unity 输入: session_id=%s user_id=%s message=%s",
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
        user_content = _build_openai_user_content(message, full_system_prompt, is_first_turn)
        llm_messages.append({"role": "user", "content": user_content})

        cfg = RuntimeConfigStore.load()
        client = RuntimeConfigStore.create_openai_client()
        try:
            response = await client.chat_messages(
                llm_messages,
                model=cfg.llm_default_model,
            )
        except LLMRequestError as exc:
            logger.error("学校大模型请求失败: %s", exc)
            raise HTTPException(status_code=502, detail=str(exc)) from exc

        if (user_id or "").startswith("unity"):
            logger.info(
                "Unity 输出: session_id=%s response=%s",
                session_id,
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

        return {"response": response, "session_id": session_id}

    async def get_history(self, session_id: str, limit: int = 20) -> List[Dict[str, Any]]:
        session = self.sessions.get(session_id)
        if not session:
            return []
        return session["messages"][-limit:]

    async def clear_history(self, session_id: str) -> None:
        if session_id in self.sessions:
            self.sessions[session_id]["messages"] = []

    async def get_available_models(self) -> List[str]:
        cfg = RuntimeConfigStore.load()
        return [cfg.llm_default_model]


chat_service = ChatService()


@chat_router.post("/", response_model=ChatResponse)
async def chat_endpoint(request: ChatRequest) -> ChatResponse:
    try:
        message = _validate_message_length(request.message)
        session_id = _validate_session_id(request.session_id)

        if (request.user_id or "").startswith("unity"):
            logger.info(
                "Unity 已连接: user_id=%s session_id=%s",
                request.user_id or "(empty)",
                request.session_id or "(new)",
            )

        result = await chat_service.chat(
            message=message,
            user_id=request.user_id,
            session_id=session_id,
            system_prompt=request.system_prompt,
        )
        return ChatResponse(
            response=result["response"],
            user_message=message,
            session_id=result.get("session_id"),
        )
    except HTTPException:
        raise
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        logger.error("聊天请求处理失败: %s", exc)
        raise HTTPException(status_code=500, detail="处理请求时出错，请稍后重试") from exc


@chat_router.get("/history/{session_id}")
async def get_chat_history(session_id: str, limit: int = 20):
    try:
        session_id = _validate_session_id(session_id)
        if not session_id:
            raise HTTPException(status_code=400, detail="session_id 不能为空")
        limit = max(1, min(limit, 100))
        history = await chat_service.get_history(session_id, limit)
        return format_response(history, success=True)
    except HTTPException:
        raise
    except Exception as exc:
        logger.error("获取聊天历史失败: %s", exc)
        raise HTTPException(status_code=500, detail="获取聊天历史失败") from exc


@chat_router.delete("/history/{session_id}")
async def clear_chat_history(session_id: str):
    try:
        session_id = _validate_session_id(session_id)
        if not session_id:
            raise HTTPException(status_code=400, detail="session_id 不能为空")
        await chat_service.clear_history(session_id)
        return format_response({"session_id": session_id}, success=True, message="历史已清除")
    except HTTPException:
        raise
    except Exception as exc:
        logger.error("清除聊天历史失败: %s", exc)
        raise HTTPException(status_code=500, detail="清除聊天历史失败") from exc


@chat_router.get("/models")
async def get_available_models():
    try:
        models = await chat_service.get_available_models()
        return format_response(models, success=True)
    except Exception as exc:
        logger.error("获取模型列表失败: %s", exc)
        raise HTTPException(status_code=500, detail="获取模型列表失败") from exc
