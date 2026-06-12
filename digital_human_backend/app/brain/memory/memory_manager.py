"""
记忆管理器模块
负责数字人的记忆存储、检索和管理
"""

from typing import Dict, List, Optional
import logging
import time
import json
import hashlib
from datetime import datetime

logger = logging.getLogger(__name__)

class MemoryManager:
    """记忆管理器"""
    
    def __init__(self, max_memories: int = 10000, 
                 memory_decay_rate: float = 0.01,
                 importance_threshold: float = 0.5):
        """
        初始化记忆管理器
        
        Args:
            max_memories: 最大记忆数量
            memory_decay_rate: 记忆衰减率
            importance_threshold: 重要性阈值
        """
        self.max_memories = max_memories
        self.memory_decay_rate = memory_decay_rate
        self.importance_threshold = importance_threshold
        
        # 记忆存储
        self.memories = {}  # {memory_id: memory_data}
        self.memory_index = {}  # 索引：{keyword: [memory_ids]}
        self.memory_types = {
            'episodic': [],  # 情节记忆
            'semantic': [],  # 语义记忆
            'procedural': [],  # 程序记忆
            'working': []  # 工作记忆
        }
        
        # 统计信息
        self.stats = {
            'total_memories': 0,
            'created_at': time.time(),
            'last_access': time.time()
        }
        
        logger.info("记忆管理器初始化完成")
    
    def add_memory(self, content: str, memory_type: str = 'episodic', 
                   importance: float = 0.5, tags: List[str] = None,
                   metadata: Dict = None) -> str:
        """
        添加记忆
        
        Args:
            content: 记忆内容
            memory_type: 记忆类型
            importance: 重要性 (0-1)
            tags: 标签列表
            metadata: 元数据
            
        Returns:
            str: 记忆ID
        """
        # 生成记忆ID
        memory_id = self._generate_memory_id(content, memory_type)
        
        # 创建记忆数据
        memory_data = {
            'id': memory_id,
            'content': content,
            'type': memory_type,
            'importance': importance,
            'tags': tags or [],
            'metadata': metadata or {},
            'created_at': time.time(),
            'last_accessed': time.time(),
            'access_count': 0,
            'decay_factor': 1.0
        }
        
        # 存储记忆
        self.memories[memory_id] = memory_data
        
        # 更新索引
        self._update_memory_index(memory_id, content, tags)
        
        # 添加到类型列表
        if memory_type in self.memory_types:
            self.memory_types[memory_type].append(memory_id)
        
        # 更新统计
        self.stats['total_memories'] += 1
        self.stats['last_access'] = time.time()
        
        # 检查是否需要清理旧记忆
        self._cleanup_old_memories()
        
        logger.info(f"记忆已添加: {memory_id[:8]}... ({memory_type})")
        return memory_id
    
    def _generate_memory_id(self, content: str, memory_type: str) -> str:
        """
        生成记忆ID
        
        Args:
            content: 记忆内容
            memory_type: 记忆类型
            
        Returns:
            str: 记忆ID
        """
        timestamp = str(int(time.time() * 1000))
        content_hash = hashlib.md5(content.encode()).hexdigest()[:8]
        return f"{memory_type}_{content_hash}_{timestamp}"
    
    def _update_memory_index(self, memory_id: str, content: str, tags: List[str]):
        """
        更新记忆索引
        
        Args:
            memory_id: 记忆ID
            content: 记忆内容
            tags: 标签列表
        """
        # 从内容中提取关键词
        keywords = self._extract_keywords(content)
        
        # 添加标签关键词
        if tags:
            keywords.extend(tags)
        
        # 更新索引
        for keyword in keywords:
            if keyword not in self.memory_index:
                self.memory_index[keyword] = []
            if memory_id not in self.memory_index[keyword]:
                self.memory_index[keyword].append(memory_id)
    
    def _extract_keywords(self, content: str) -> List[str]:
        """
        从内容中提取关键词
        
        Args:
            content: 内容文本
            
        Returns:
            List[str]: 关键词列表
        """
        # 简化的关键词提取
        words = content.lower().split()
        # 过滤掉常见停用词
        stop_words = {'的', '了', '在', '是', '我', '你', '他', '她', '它', '们', '这', '那', '和', '与', '或', '但', '因为', '所以'}
        keywords = [word for word in words if len(word) > 1 and word not in stop_words]
        return keywords[:10]  # 最多返回10个关键词
    
    def search_memories(self, query: str, memory_type: str = None, 
                       limit: int = 10, min_importance: float = 0.0) -> List[Dict]:
        """
        搜索记忆
        
        Args:
            query: 查询字符串
            memory_type: 记忆类型过滤
            limit: 返回数量限制
            min_importance: 最小重要性
            
        Returns:
            List[Dict]: 匹配的记忆列表
        """
        # 提取查询关键词
        query_keywords = self._extract_keywords(query)
        
        # 找到相关记忆ID
        candidate_ids = set()
        for keyword in query_keywords:
            if keyword in self.memory_index:
                candidate_ids.update(self.memory_index[keyword])
        
        # 计算相关性得分
        scored_memories = []
        for memory_id in candidate_ids:
            if memory_id not in self.memories:
                continue
            
            memory = self.memories[memory_id]
            
            # 类型过滤
            if memory_type and memory['type'] != memory_type:
                continue
            
            # 重要性过滤
            if memory['importance'] < min_importance:
                continue
            
            # 计算相关性得分
            relevance_score = self._calculate_relevance(memory, query_keywords)
            
            scored_memories.append({
                'memory': memory,
                'relevance_score': relevance_score
            })
        
        # 按相关性排序
        scored_memories.sort(key=lambda x: x['relevance_score'], reverse=True)
        
        # 返回结果
        results = []
        for item in scored_memories[:limit]:
            memory = item['memory'].copy()
            memory['relevance_score'] = item['relevance_score']
            results.append(memory)
        
        # 更新访问统计
        for result in results:
            self._update_access_stats(result['id'])
        
        return results
    
    def _calculate_relevance(self, memory: Dict, query_keywords: List[str]) -> float:
        """
        计算记忆相关性得分
        
        Args:
            memory: 记忆数据
            query_keywords: 查询关键词
            
        Returns:
            float: 相关性得分
        """
        content_keywords = self._extract_keywords(memory['content'])
        tag_keywords = memory.get('tags', [])
        
        # 计算关键词匹配
        content_matches = len(set(query_keywords) & set(content_keywords))
        tag_matches = len(set(query_keywords) & set(tag_keywords))
        
        # 计算基础得分
        base_score = content_matches * 0.7 + tag_matches * 0.3
        
        # 应用重要性权重
        importance_weight = memory['importance']
        
        # 应用衰减因子
        decay_weight = memory['decay_factor']
        
        # 应用访问频率权重
        access_weight = min(1.0, memory['access_count'] / 10.0)
        
        final_score = base_score * importance_weight * decay_weight * (1.0 + access_weight)
        
        return final_score
    
    def _update_access_stats(self, memory_id: str):
        """
        更新访问统计
        
        Args:
            memory_id: 记忆ID
        """
        if memory_id in self.memories:
            self.memories[memory_id]['last_accessed'] = time.time()
            self.memories[memory_id]['access_count'] += 1
            self.stats['last_access'] = time.time()
    
    def get_memory(self, memory_id: str) -> Optional[Dict]:
        """
        获取特定记忆
        
        Args:
            memory_id: 记忆ID
            
        Returns:
            Optional[Dict]: 记忆数据
        """
        if memory_id in self.memories:
            self._update_access_stats(memory_id)
            return self.memories[memory_id]
        return None
    
    def update_memory(self, memory_id: str, updates: Dict) -> bool:
        """
        更新记忆
        
        Args:
            memory_id: 记忆ID
            updates: 更新数据
            
        Returns:
            bool: 更新是否成功
        """
        if memory_id not in self.memories:
            return False
        
        # 更新记忆数据
        for key, value in updates.items():
            if key in ['content', 'importance', 'tags', 'metadata']:
                self.memories[memory_id][key] = value
        
        # 如果内容或标签发生变化，更新索引
        if 'content' in updates or 'tags' in updates:
            memory = self.memories[memory_id]
            self._update_memory_index(memory_id, memory['content'], memory['tags'])
        
        logger.info(f"记忆已更新: {memory_id[:8]}...")
        return True
    
    def delete_memory(self, memory_id: str) -> bool:
        """
        删除记忆
        
        Args:
            memory_id: 记忆ID
            
        Returns:
            bool: 删除是否成功
        """
        if memory_id not in self.memories:
            return False
        
        memory = self.memories[memory_id]
        
        # 从类型列表中移除
        if memory['type'] in self.memory_types:
            if memory_id in self.memory_types[memory['type']]:
                self.memory_types[memory['type']].remove(memory_id)
        
        # 从索引中移除
        self._remove_from_index(memory_id)
        
        # 删除记忆
        del self.memories[memory_id]
        self.stats['total_memories'] -= 1
        
        logger.info(f"记忆已删除: {memory_id[:8]}...")
        return True
    
    def _remove_from_index(self, memory_id: str):
        """
        从索引中移除记忆
        
        Args:
            memory_id: 记忆ID
        """
        for keyword, memory_ids in self.memory_index.items():
            if memory_id in memory_ids:
                memory_ids.remove(memory_id)
                if not memory_ids:  # 如果列表为空，删除关键词
                    del self.memory_index[keyword]
    
    def _cleanup_old_memories(self):
        """
        清理旧记忆
        """
        if len(self.memories) <= self.max_memories:
            return
        
        # 按重要性排序，删除最不重要的记忆
        sorted_memories = sorted(
            self.memories.items(),
            key=lambda x: (x[1]['importance'], x[1]['last_accessed'])
        )
        
        # 删除最不重要的记忆
        memories_to_delete = len(self.memories) - self.max_memories
        for i in range(memories_to_delete):
            memory_id, _ = sorted_memories[i]
            self.delete_memory(memory_id)
    
    def get_memory_statistics(self) -> Dict:
        """
        获取记忆统计信息
        
        Returns:
            Dict: 统计信息
        """
        type_counts = {mem_type: len(memories) for mem_type, memories in self.memory_types.items()}
        
        if self.memories:
            importances = [mem['importance'] for mem in self.memories.values()]
            avg_importance = sum(importances) / len(importances)
        else:
            avg_importance = 0.0
        
        return {
            'total_memories': self.stats['total_memories'],
            'type_counts': type_counts,
            'avg_importance': avg_importance,
            'indexed_keywords': len(self.memory_index),
            'created_at': datetime.fromtimestamp(self.stats['created_at']).isoformat(),
            'last_access': datetime.fromtimestamp(self.stats['last_access']).isoformat()
        }
    
    def save_memories(self, filepath: str) -> bool:
        """
        保存记忆到文件
        
        Args:
            filepath: 文件路径
            
        Returns:
            bool: 保存是否成功
        """
        try:
            data = {
                'memories': self.memories,
                'memory_types': self.memory_types,
                'memory_index': self.memory_index,
                'stats': self.stats
            }
            
            with open(filepath, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            
            logger.info(f"记忆已保存到: {filepath}")
            return True
        except Exception as e:
            logger.error(f"保存记忆失败: {e}")
            return False
    
    def load_memories(self, filepath: str) -> bool:
        """
        从文件加载记忆
        
        Args:
            filepath: 文件路径
            
        Returns:
            bool: 加载是否成功
        """
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            self.memories = data.get('memories', {})
            self.memory_types = data.get('memory_types', {})
            self.memory_index = data.get('memory_index', {})
            self.stats = data.get('stats', self.stats)
            
            logger.info(f"记忆已从 {filepath} 加载")
            return True
        except Exception as e:
            logger.error(f"加载记忆失败: {e}")
            return False


# 使用示例
if __name__ == "__main__":
    # 创建记忆管理器
    memory_manager = MemoryManager()
    
    # 添加记忆
    memory_id1 = memory_manager.add_memory(
        "用户喜欢喝咖啡",
        memory_type="semantic",
        importance=0.8,
        tags=["用户偏好", "饮食"]
    )
    
    memory_id2 = memory_manager.add_memory(
        "今天和用户讨论了人工智能的发展",
        memory_type="episodic",
        importance=0.6,
        tags=["对话", "AI"]
    )
    
    # 搜索记忆
    results = memory_manager.search_memories("用户喜欢什么", limit=5)
    print("搜索结果:")
    for result in results:
        print(f"- {result['content']} (相关性: {result['relevance_score']:.3f})")
    
    # 获取统计信息
    stats = memory_manager.get_memory_statistics()
    print("\n记忆统计:", stats)
    
    # 保存记忆
    memory_manager.save_memories("memories.json")
