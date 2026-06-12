"""
WebSocket 服务（路由 + 连接管理）
"""

import json
import uuid
import logging
from typing import Dict, Set

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from app.services.chat_service import chat_service

logger = logging.getLogger(__name__)
websocket_router = APIRouter()


class ConnectionManager:
    """WebSocket连接管理器"""

    def __init__(self):
        self.active_connections: Dict[str, WebSocket] = {}
        self.session_connections: Dict[str, Set[str]] = {}

    async def connect(self, websocket: WebSocket, connection_id: str, session_id: str = None):
        await websocket.accept()
        self.active_connections[connection_id] = websocket
        if session_id:
            if session_id not in self.session_connections:
                self.session_connections[session_id] = set()
            self.session_connections[session_id].add(connection_id)
        logger.info(f"WebSocket连接已建立: {connection_id}")

    def disconnect(self, connection_id: str, session_id: str = None):
        if connection_id in self.active_connections:
            del self.active_connections[connection_id]
        if session_id and session_id in self.session_connections:
            self.session_connections[session_id].discard(connection_id)
            if not self.session_connections[session_id]:
                del self.session_connections[session_id]
        logger.info(f"WebSocket连接已断开: {connection_id}")

    async def send_personal_message(self, message: str, connection_id: str):
        if connection_id in self.active_connections:
            websocket = self.active_connections[connection_id]
            await websocket.send_text(message)

    async def send_to_session(self, message: str, session_id: str):
        if session_id in self.session_connections:
            for connection_id in self.session_connections[session_id]:
                await self.send_personal_message(message, connection_id)


manager = ConnectionManager()


@websocket_router.websocket("/chat/{session_id}")
async def websocket_chat_endpoint(websocket: WebSocket, session_id: str):
    """WebSocket聊天端点"""
    connection_id = str(uuid.uuid4())
    await manager.connect(websocket, connection_id, session_id)
    try:
        while True:
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

            await websocket.send_text(json.dumps({
                "type": "processing",
                "message": "正在处理..."
            }))

            try:
                result = await chat_service.chat(
                    message=user_message,
                    session_id=session_id,
                    system_prompt=system_prompt
                )
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

