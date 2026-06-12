"""
WebSocket连接管理
WebSocket connection management
"""

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from typing import Dict, Set
import logging
import json

from app.services.chat_service import ChatService

logger = logging.getLogger(__name__)
router = APIRouter()

# WebSocket连接管理器
class ConnectionManager:
    """WebSocket连接管理器"""
    
    def __init__(self):
        self.active_connections: Dict[str, WebSocket] = {}
        self.session_connections: Dict[str, Set[str]] = {}  # session_id -> set of connection_ids
    
    async def connect(self, websocket: WebSocket, connection_id: str, session_id: str = None):
        """
        连接WebSocket
        
        Args:
            websocket: WebSocket连接
            connection_id: 连接ID
            session_id: 会话ID
        """
        await websocket.accept()
        self.active_connections[connection_id] = websocket
        
        if session_id:
            if session_id not in self.session_connections:
                self.session_connections[session_id] = set()
            self.session_connections[session_id].add(connection_id)
        
        logger.info(f"WebSocket连接已建立: {connection_id}")
    
    def disconnect(self, connection_id: str, session_id: str = None):
        """
        断开WebSocket连接
        
        Args:
            connection_id: 连接ID
            session_id: 会话ID
        """
        if connection_id in self.active_connections:
            del self.active_connections[connection_id]
        
        if session_id and session_id in self.session_connections:
            self.session_connections[session_id].discard(connection_id)
            if not self.session_connections[session_id]:
                del self.session_connections[session_id]
        
        logger.info(f"WebSocket连接已断开: {connection_id}")
    
    async def send_personal_message(self, message: str, connection_id: str):
        """
        发送个人消息
        
        Args:
            message: 消息内容
            connection_id: 连接ID
        """
        if connection_id in self.active_connections:
            websocket = self.active_connections[connection_id]
            await websocket.send_text(message)
    
    async def send_to_session(self, message: str, session_id: str):
        """
        发送消息到会话的所有连接
        
        Args:
            message: 消息内容
            session_id: 会话ID
        """
        if session_id in self.session_connections:
            for connection_id in self.session_connections[session_id]:
                await self.send_personal_message(message, connection_id)

# 创建连接管理器实例
manager = ConnectionManager()
chat_service = ChatService()

@router.websocket("/chat/{session_id}")
async def websocket_chat_endpoint(websocket: WebSocket, session_id: str):
    """
    WebSocket聊天端点
    
    Args:
        websocket: WebSocket连接
        session_id: 会话ID
    """
    import uuid
    connection_id = str(uuid.uuid4())
    
    await manager.connect(websocket, connection_id, session_id)
    
    try:
        while True:
            # 接收消息
            data = await websocket.receive_text()
            message_data = json.loads(data)
            
            user_message = message_data.get("message", "")
            system_prompt = message_data.get("system_prompt", None)
            
            if not user_message:
                await websocket.send_text(json.dumps({
                    "type": "error",
                    "message": "消息不能为空"
                }))
                continue
            
            # 发送处理中消息
            await websocket.send_text(json.dumps({
                "type": "processing",
                "message": "正在处理..."
            }))
            
            # 调用聊天服务
            try:
                result = await chat_service.chat(
                    message=user_message,
                    session_id=session_id,
                    system_prompt=system_prompt
                )
                
                # 发送响应
                await websocket.send_text(json.dumps({
                    "type": "response",
                    "response": result["response"],
                    "user_message": user_message,
                    "session_id": session_id
                }))
                
            except Exception as e:
                logger.error(f"WebSocket聊天处理失败: {e}")
                await websocket.send_text(json.dumps({
                    "type": "error",
                    "message": f"处理失败: {str(e)}"
                }))
                
    except WebSocketDisconnect:
        manager.disconnect(connection_id, session_id)
        logger.info(f"WebSocket连接断开: {connection_id}")

