"""
任务路由
Task routing
"""

from typing import Dict, Any, Optional
import logging

logger = logging.getLogger(__name__)

class TaskRouter:
    """任务路由器"""
    
    def __init__(self):
        """初始化任务路由器"""
        self.routes = {
            "chat": self._route_chat,
            "training": self._route_training,
            "memory": self._route_memory,
            "decision": self._route_decision
        }
        logger.info("任务路由器初始化完成")
    
    def route(self, task_type: str, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """
        路由任务
        
        Args:
            task_type: 任务类型
            task_data: 任务数据
            
        Returns:
            Dict: 路由结果
        """
        if task_type not in self.routes:
            raise ValueError(f"未知的任务类型: {task_type}")
        
        return self.routes[task_type](task_data)
    
    def _route_chat(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """路由聊天任务"""
        return {
            "handler": "chat_service",
            "action": "chat",
            "data": task_data
        }
    
    def _route_training(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """路由训练任务"""
        return {
            "handler": "training_service",
            "action": "train",
            "data": task_data
        }
    
    def _route_memory(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """路由记忆任务"""
        return {
            "handler": "memory_manager",
            "action": "manage",
            "data": task_data
        }
    
    def _route_decision(self, task_data: Dict[str, Any]) -> Dict[str, Any]:
        """路由决策任务"""
        return {
            "handler": "decision_maker",
            "action": "decide",
            "data": task_data
        }

