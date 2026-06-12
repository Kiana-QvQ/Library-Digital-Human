"""
上下文构建器模块
负责构建和管理对话上下文
"""

from typing import Dict, List, Optional
import logging
import time
from datetime import datetime

logger = logging.getLogger(__name__)

class ContextBuilder:
    """上下文构建器"""
    
    def __init__(self, memory_manager=None, knowledge_retriever=None,
                 max_context_length: int = 4000, 
                 max_history_turns: int = 10,
                 context_window: int = 5):
        """
        初始化上下文构建器
        
        Args:
            memory_manager: 记忆管理器实例
            knowledge_retriever: 知识检索器实例（用于向量存储和图数据库检索）
            max_context_length: 最大上下文长度
            max_history_turns: 最大历史轮数
            context_window: 上下文窗口大小
        """
        # memory_manager 实际上可以是任意实现了 search_memories 接口的存储（例如 Mem0Store）
        self.memory_store = memory_manager
        self.knowledge_retriever = knowledge_retriever
        self.max_context_length = max_context_length
        self.max_history_turns = max_history_turns
        self.context_window = context_window
        
        # 对话历史
        self.conversation_history = []
        self.current_session_id = None
        
        # 上下文缓存
        self.context_cache = {}
        self.cache_ttl = 300  # 缓存5分钟
        
        # 上下文模板
        self.context_templates = {
            'system': "你是一个友好的AI助手，具有以下特点：",
            'user': "用户说：{content}",
            'assistant': "助手回复：{content}",
            'context': "相关上下文：{context}",
            'memory': "相关记忆：{memory}"
        }
        
        logger.info("上下文构建器初始化完成")
    
    def start_new_session(self, session_id: str = None) -> str:
        """
        开始新会话
        
        Args:
            session_id: 会话ID，为None时自动生成
            
        Returns:
            str: 会话ID
        """
        if session_id is None:
            session_id = f"session_{int(time.time())}"
        
        self.current_session_id = session_id
        self.conversation_history = []
        
        logger.info(f"新会话已开始: {session_id}")
        return session_id
    
    def add_turn(self, role: str, content: str, metadata: Dict = None) -> bool:
        """
        添加对话轮次
        
        Args:
            role: 角色 (user/assistant/system)
            content: 内容
            metadata: 元数据
            
        Returns:
            bool: 添加是否成功
        """
        if not self.current_session_id:
            logger.warning("没有活跃会话，请先开始新会话")
            return False
        
        turn = {
            'role': role,
            'content': content,
            'timestamp': time.time(),
            'metadata': metadata or {},
            'turn_id': len(self.conversation_history)
        }
        
        self.conversation_history.append(turn)
        
        # 限制历史长度
        if len(self.conversation_history) > self.max_history_turns:
            self.conversation_history.pop(0)
        
        logger.debug(f"对话轮次已添加: {role} - {content[:50]}...")
        return True
    
    def build_context(
        self,
        current_query: str = None,
        include_memories: bool = True,
        include_system_info: bool = True,
        custom_context: Dict = None,
        use_personal_memory: bool = True,
    ) -> str:
        """
        构建上下文。

        Args:
            current_query: 当前查询
            include_memories: 是否包含记忆（个人 mem0 + 知识库 RAG）
            include_system_info: 是否包含系统信息
            custom_context: 自定义上下文（可含 user_id、session_id）
            use_personal_memory: True=检索并纳入个人长期记忆(mem0)；False=仅知识库/RAG

        Returns:
            str: 构建的上下文
        """
        context_parts = []
        
        # 系统信息
        if include_system_info:
            system_context = self._build_system_context()
            if system_context:
                context_parts.append(system_context)
        
        # 对话历史
        history_context = self._build_history_context()
        if history_context:
            context_parts.append(history_context)
        
        # 相关记忆
        if include_memories and current_query:
            memory_context = self._build_memory_context(
                current_query,
                custom_context.get("user_id") if custom_context else None,
                use_personal_memory=use_personal_memory,
                kb_id=custom_context.get("kb_id") if custom_context else None,
                kb_name=custom_context.get("kb_name") if custom_context else None,
            )
            if memory_context:
                context_parts.append(memory_context)
        
        # 自定义上下文
        if custom_context:
            custom_context_str = self._build_custom_context(custom_context)
            if custom_context_str:
                context_parts.append(custom_context_str)
        
        # 当前查询
        if current_query:
            context_parts.append(f"当前用户输入：{current_query}")
        
        # 合并上下文
        full_context = "\n\n".join(context_parts)
        
        # 截断过长的上下文
        if len(full_context) > self.max_context_length:
            full_context = self._truncate_context(full_context)
        
        return full_context
    
    def _build_system_context(self) -> str:
        """
        构建系统上下文
        
        Returns:
            str: 系统上下文
        """
        system_info = [
            "当前时间：" + datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "会话ID：" + (self.current_session_id or "未知"),
            "对话轮数：" + str(len(self.conversation_history))
        ]
        
        return self.context_templates['system'] + "\n" + "\n".join(system_info)
    
    def _build_history_context(self) -> str:
        """
        构建历史上下文
        
        Returns:
            str: 历史上下文
        """
        if not self.conversation_history:
            return ""
        
        # 获取最近的对话轮次
        recent_turns = self.conversation_history[-self.context_window:]
        
        history_parts = []
        for turn in recent_turns:
            role = turn['role']
            content = turn['content']
            timestamp = datetime.fromtimestamp(turn['timestamp']).strftime("%H:%M")
            
            if role == 'user':
                history_parts.append(f"[{timestamp}] 用户：{content}")
            elif role == 'assistant':
                history_parts.append(f"[{timestamp}] 助手：{content}")
            elif role == 'system':
                history_parts.append(f"[{timestamp}] 系统：{content}")
        
        return "对话历史：\n" + "\n".join(history_parts)
    
    def _build_memory_context(
        self,
        query: str,
        user_id: str | None = None,
        use_personal_memory: bool = True,
        kb_id: str | None = None,
        kb_name: str | None = None,
    ) -> str:
        """
        构建记忆上下文
        
        Args:
            query: 查询字符串
            kb_id: 本地 /api/chat 绑定的知识库 ID；传入时仅从该库的 Chroma 集合检索
            kb_name: 知识库展示名（用于上下文标签）
            
        Returns:
            str: 记忆上下文
        """
        context_parts = []
        
        # 1. 从长期记忆存储（Mem0Store 或 MemoryManager）搜索相关记忆（如果可用）
        if use_personal_memory and self.memory_store:
            relevant_memories = self.memory_store.search_memories(
                query=query,
                user_id=user_id,
                limit=5,
                min_importance=0.3
            )
            logger.debug(
                "ContextBuilder._build_memory_context: use_personal_memory=%s, user_id=%s, 命中记忆条数=%d",
                use_personal_memory,
                user_id,
                len(relevant_memories) if relevant_memories else 0,
            )
            if relevant_memories:
                memory_texts = [f"- {mem['content']}" for mem in relevant_memories]
                context_parts.append("相关记忆：\n" + "\n".join(memory_texts))
        
        # 2. 本地模型路径：仅在指定 kb_id 时从对应 Chroma 集合检索（与文档入库 kb_{id} 一致）
        if (
            kb_id
            and hasattr(self, "knowledge_retriever")
            and self.knowledge_retriever
        ):
            try:
                dialogue_results = self.knowledge_retriever.search_dialogues(
                    query=query,
                    n_results=3,
                    min_score=0.3,
                    kb_id=kb_id,
                )

                if dialogue_results:
                    dialogue_texts = []
                    for r in dialogue_results:
                        text = r.get("text", "")[:200]
                        meta = r.get("metadata", {}) or {}
                        chapter = meta.get("chapter_name", "") or meta.get(
                            "document_name", ""
                        )
                        speakers = meta.get("speakers", "")

                        dialogue_info = f"- [{chapter or '片段'}]"
                        if speakers:
                            dialogue_info += f" {speakers}: "
                        dialogue_info += text
                        dialogue_texts.append(dialogue_info)

                    kb_label = kb_name or kb_id
                    context_parts.append(
                        f"知识库「{kb_label}」检索片段：\n" + "\n".join(dialogue_texts)
                    )
            except Exception as e:
                logger.warning(f"知识检索失败: {e}")
        
        return "\n\n".join(context_parts) if context_parts else ""
    
    async def build_context_async(
        self,
        query: str = None,
        session_id: str = None,
        user_id: Optional[str] = None,
        use_personal_memory: bool = True,
        kb_id: Optional[str] = None,
        kb_name: Optional[str] = None,
    ) -> str:
        """
        异步构建上下文（用于服务层）
        
        Args:
            query: 查询字符串
            session_id: 会话ID
            user_id: 用户ID（用于长期记忆 mem0 按用户检索）
            use_personal_memory: 是否使用个人长期记忆（mem0）
            kb_id: 本地对话绑定的知识库 ID（走 Coze 的前端不会调用此后端聊天，无需传）
            kb_name: 知识库名称（用于上下文与 prompt 展示）
            
        Returns:
            str: 构建的上下文
        """
        custom_context: Dict = {}
        if user_id is not None:
            custom_context["user_id"] = user_id
        if session_id is not None:
            custom_context["session_id"] = session_id
        if kb_id:
            custom_context["kb_id"] = kb_id
        if kb_name:
            custom_context["kb_name"] = kb_name

        return self.build_context(
            current_query=query,
            include_memories=True,
            include_system_info=True,
            custom_context=custom_context or None,
            use_personal_memory=use_personal_memory,
        )
    
    def _build_custom_context(self, custom_context: Dict) -> str:
        """
        构建自定义上下文
        
        Args:
            custom_context: 自定义上下文数据
            
        Returns:
            str: 自定义上下文
        """
        context_parts = []
        
        for key, value in custom_context.items():
            if isinstance(value, (str, int, float)):
                context_parts.append(f"{key}：{value}")
            elif isinstance(value, list):
                context_parts.append(f"{key}：{', '.join(map(str, value))}")
            elif isinstance(value, dict):
                context_parts.append(f"{key}：{self._dict_to_string(value)}")
        
        if context_parts:
            return "额外信息：\n" + "\n".join(context_parts)
        
        return ""
    
    def _dict_to_string(self, data: Dict, indent: int = 0) -> str:
        """
        将字典转换为字符串
        
        Args:
            data: 字典数据
            indent: 缩进级别
            
        Returns:
            str: 字符串表示
        """
        lines = []
        prefix = "  " * indent
        
        for key, value in data.items():
            if isinstance(value, dict):
                lines.append(f"{prefix}{key}：")
                lines.append(self._dict_to_string(value, indent + 1))
            else:
                lines.append(f"{prefix}{key}：{value}")
        
        return "\n".join(lines)
    
    def _truncate_context(self, context: str) -> str:
        """
        截断过长的上下文
        
        Args:
            context: 原始上下文
            
        Returns:
            str: 截断后的上下文
        """
        if len(context) <= self.max_context_length:
            return context
        
        # 从后往前截断，保留最重要的部分
        truncated = context[:self.max_context_length - 100]  # 留出100字符的缓冲
        truncated += "\n\n[上下文已截断...]"
        
        return truncated
    
    def get_conversation_summary(self) -> Dict:
        """
        获取对话摘要
        
        Returns:
            Dict: 对话摘要信息
        """
        if not self.conversation_history:
            return {}
        
        user_turns = [t for t in self.conversation_history if t['role'] == 'user']
        assistant_turns = [t for t in self.conversation_history if t['role'] == 'assistant']
        
        return {
            'session_id': self.current_session_id,
            'total_turns': len(self.conversation_history),
            'user_turns': len(user_turns),
            'assistant_turns': len(assistant_turns),
            'session_duration': time.time() - (self.conversation_history[0]['timestamp'] if self.conversation_history else time.time()),
            'last_activity': datetime.fromtimestamp(self.conversation_history[-1]['timestamp']).isoformat() if self.conversation_history else None
        }
    
    def get_recent_context(self, turns: int = 3) -> List[Dict]:
        """
        获取最近的上下文
        
        Args:
            turns: 轮次数
            
        Returns:
            List[Dict]: 最近的对话轮次
        """
        return self.conversation_history[-turns:] if self.conversation_history else []
    
    def clear_history(self):
        """清空对话历史"""
        self.conversation_history.clear()
        logger.info("对话历史已清空")
    
    def export_context(self, filepath: str) -> bool:
        """
        导出上下文到文件
        
        Args:
            filepath: 文件路径
            
        Returns:
            bool: 导出是否成功
        """
        try:
            import json
            
            export_data = {
                'session_id': self.current_session_id,
                'conversation_history': self.conversation_history,
                'exported_at': datetime.now().isoformat()
            }
            
            with open(filepath, 'w', encoding='utf-8') as f:
                json.dump(export_data, f, ensure_ascii=False, indent=2)
            
            logger.info(f"上下文已导出到: {filepath}")
            return True
        except Exception as e:
            logger.error(f"导出上下文失败: {e}")
            return False
    
    def import_context(self, filepath: str) -> bool:
        """
        从文件导入上下文
        
        Args:
            filepath: 文件路径
            
        Returns:
            bool: 导入是否成功
        """
        try:
            import json
            
            with open(filepath, 'r', encoding='utf-8') as f:
                import_data = json.load(f)
            
            self.current_session_id = import_data.get('session_id')
            self.conversation_history = import_data.get('conversation_history', [])
            
            logger.info(f"上下文已从 {filepath} 导入")
            return True
        except Exception as e:
            logger.error(f"导入上下文失败: {e}")
            return False


# 使用示例
if __name__ == "__main__":
    # 创建上下文构建器
    context_builder = ContextBuilder()
    
    # 开始新会话
    session_id = context_builder.start_new_session()
    print(f"新会话开始: {session_id}")
    
    # 添加对话轮次
    context_builder.add_turn("user", "你好，我想了解一下人工智能")
    context_builder.add_turn("assistant", "你好！我很乐意为你介绍人工智能。AI是计算机科学的一个分支...")
    context_builder.add_turn("user", "那机器学习是什么？")
    
    # 构建上下文
    context = context_builder.build_context(
        current_query="请详细解释一下深度学习",
        include_memories=True,
        include_system_info=True
    )
    
    print("\n构建的上下文:")
    print(context)
    
    # 获取对话摘要
    summary = context_builder.get_conversation_summary()
    print(f"\n对话摘要: {summary}")
    
    # 获取最近上下文
    recent = context_builder.get_recent_context(2)
    print(f"\n最近2轮对话: {recent}")
