"""
决策仲裁
Decision arbitration
"""

from typing import Dict, Any, List
import logging

logger = logging.getLogger(__name__)

class Arbitration:
    """决策仲裁器"""
    
    def __init__(self):
        """初始化决策仲裁器"""
        self.arbitration_strategies = {
            "majority": self._majority_vote,
            "weighted": self._weighted_vote,
            "consensus": self._consensus_vote
        }
        logger.info("决策仲裁器初始化完成")
    
    def arbitrate(self, decisions: List[Dict[str, Any]], 
                  strategy: str = "weighted") -> Dict[str, Any]:
        """
        仲裁多个决策
        
        Args:
            decisions: 决策列表
            strategy: 仲裁策略
            
        Returns:
            Dict: 最终决策
        """
        if strategy not in self.arbitration_strategies:
            raise ValueError(f"未知的仲裁策略: {strategy}")
        
        if not decisions:
            raise ValueError("决策列表不能为空")
        
        return self.arbitration_strategies[strategy](decisions)
    
    def _majority_vote(self, decisions: List[Dict[str, Any]]) -> Dict[str, Any]:
        """多数投票"""
        from collections import Counter
        votes = Counter(d["decision"] for d in decisions)
        winner = votes.most_common(1)[0][0]
        return {"decision": winner, "confidence": votes[winner] / len(decisions)}
    
    def _weighted_vote(self, decisions: List[Dict[str, Any]]) -> Dict[str, Any]:
        """加权投票"""
        weighted_scores = {}
        total_weight = 0
        
        for decision in decisions:
            decision_value = decision["decision"]
            weight = decision.get("weight", 1.0) * decision.get("confidence", 0.5)
            total_weight += weight
            
            if decision_value not in weighted_scores:
                weighted_scores[decision_value] = 0
            weighted_scores[decision_value] += weight
        
        winner = max(weighted_scores.items(), key=lambda x: x[1])
        return {
            "decision": winner[0],
            "confidence": winner[1] / total_weight if total_weight > 0 else 0
        }
    
    def _consensus_vote(self, decisions: List[Dict[str, Any]]) -> Dict[str, Any]:
        """共识投票"""
        if len(decisions) == 1:
            return decisions[0]
        
        # 检查是否所有决策一致
        first_decision = decisions[0]["decision"]
        if all(d["decision"] == first_decision for d in decisions):
            return {
                "decision": first_decision,
                "confidence": 1.0
            }
        
        # 如果不一致，使用加权投票
        return self._weighted_vote(decisions)

