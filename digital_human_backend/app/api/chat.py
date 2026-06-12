"""
聊天API路由
Chat API routes
"""

from fastapi import APIRouter, HTTPException, Body
from typing import Optional, Dict, Any
from pydantic import BaseModel, Field
import logging

from app.services.chat_service import ChatService
from app.shared.utils import format_response, validate_request

logger = logging.getLogger(__name__)
router = APIRouter()

# 请求模型
class ChatRequest(BaseModel):
    """聊天请求模型"""
    message: str = Field(..., description="用户消息")
    user_id: Optional[str] = Field(None, description="用户ID")
    session_id: Optional[str] = Field(None, description="会话ID")
    system_prompt: Optional[str] = Field(None, description="系统提示词")
    stream: bool = Field(False, description="是否使用流式响应")

class ChatResponse(BaseModel):
    """聊天响应模型"""
    response: str = Field(..., description="AI回复")
    user_message: str = Field(..., description="用户消息")
    session_id: Optional[str] = Field(None, description="会话ID")

# 初始化聊天服务
chat_service = ChatService()

@router.post("/", response_model=ChatResponse)
async def chat_endpoint(request: ChatRequest):
    """
    聊天接口
    
    Args:
        request: 聊天请求
        
    Returns:
        ChatResponse: 聊天响应
    """
    try:
        # 验证请求
        if not request.message or not request.message.strip():
            raise HTTPException(status_code=400, detail="消息不能为空")
        
        # 调用聊天服务
        result = await chat_service.chat(
            message=request.message,
            user_id=request.user_id,
            session_id=request.session_id,
            system_prompt=request.system_prompt
        )
        
        return ChatResponse(
            response=result["response"],
            user_message=request.message,
            session_id=result.get("session_id")
        )
        
    except Exception as e:
        logger.error(f"聊天请求处理失败: {e}")
        raise HTTPException(status_code=500, detail=f"处理请求时出错: {str(e)}")

@router.get("/history/{session_id}")
async def get_chat_history(session_id: str, limit: int = 20):
    """
    获取聊天历史
    
    Args:
        session_id: 会话ID
        limit: 返回数量限制
        
    Returns:
        Dict: 聊天历史
    """
    try:
        history = await chat_service.get_history(session_id, limit)
        return format_response(history, success=True)
    except Exception as e:
        logger.error(f"获取聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.delete("/history/{session_id}")
async def clear_chat_history(session_id: str):
    """
    清除聊天历史
    
    Args:
        session_id: 会话ID
        
    Returns:
        Dict: 操作结果
    """
    try:
        await chat_service.clear_history(session_id)
        return format_response({"session_id": session_id}, success=True, message="历史已清除")
    except Exception as e:
        logger.error(f"清除聊天历史失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.get("/models")
async def get_available_models():
    """
    获取可用模型列表
    
    Returns:
        Dict: 模型列表
    """
    try:
        models = await chat_service.get_available_models()
        return format_response(models, success=True)
    except Exception as e:
        logger.error(f"获取模型列表失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))

