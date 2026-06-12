"""
核心决策器
Decision maker - core decision making module
"""

from typing import Dict, Any, List, Optional
import logging

from app.brain.llm.ollama_chat import OllamaChat
from app.brain.memory.mem0_store import Mem0Store
from app.coordinator.arbitration import Arbitration

logger = logging.getLogger(__name__)

class DecisionMaker:
    """核心决策器"""
    
    def __init__(self):
        """初始化决策器"""
        self.llm = OllamaChat()
        self.memory_store = Mem0Store()
        self.arbitration = Arbitration()
        
        logger.info("决策器初始化完成")
    
    async def make_decision(self, context: Dict[str, Any], 
                           options: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        做出决策
        
        Args:
            context: 上下文信息
            options: 可选决策列表
            
        Returns:
            Dict: 最终决策
        """
        try:
            # 分析上下文
            analysis = await self._analyze_context(context)
            
            # 评估每个选项
            evaluated_options = []
            for option in options:
                evaluation = await self._evaluate_option(option, context, analysis)
                evaluated_options.append(evaluation)
            
            # 仲裁决策
            decision = self.arbitration.arbitrate(evaluated_options, strategy="weighted")
            
            # 记录决策到长期记忆
            self.memory_store.add_conversation_memory(
                user_id=context.get("user_id"),
                session_id=context.get("session_id"),
                user_message=str(context),
                assistant_message=str(decision),
            )
            
            return decision
            
        except Exception as e:
            logger.error(f"决策失败: {e}")
            raise
    
    async def _analyze_context(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """
        分析上下文
        
        Args:
            context: 上下文信息
            
        Returns:
            Dict: 分析结果
        """
        # 从长期记忆中获取相关记忆
        query = context.get("query", "")
        relevant_memories = self.memory_store.search_memories(
            query,
            user_id=context.get("user_id"),
            limit=5,
        )
        
        return {
            "relevant_memories": relevant_memories,
            "context_summary": str(context)[:200]
        }
    
    async def _evaluate_option(self, option: Dict[str, Any], 
                              context: Dict[str, Any],
                              analysis: Dict[str, Any]) -> Dict[str, Any]:
        """
        评估选项
        
        Args:
            option: 选项
            context: 上下文
            analysis: 分析结果
            
        Returns:
            Dict: 评估结果
        """
        # 简化的评估逻辑
        score = 0.5  # 默认分数
        
        # 根据选项类型调整分数
        if option.get("type") == "chat":
            score = 0.8
        elif option.get("type") == "memory":
            score = 0.6
        
        return {
            "decision": option.get("action", "unknown"),
            "confidence": score,
            "weight": option.get("weight", 1.0)
        }

