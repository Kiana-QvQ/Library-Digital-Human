"""
知识检索系统
整合ChromaDB向量存储和Neo4j图数据库，提供统一的知识检索接口
"""

from typing import Dict, List, Optional
import logging
from pathlib import Path
import sys

# 添加项目路径
project_root = Path(__file__).parent.parent.parent.parent
sys.path.insert(0, str(project_root))

from app.brain.memory.chromadb_vector_store import ChromaDBVectorStore
from app.brain.memory.neo4j_graph_db import Neo4jGraphDB
from app.shared.config import Config

logger = logging.getLogger(__name__)


class KnowledgeRetriever:
    """知识检索器 - 整合向量存储和图数据库"""
    
    def __init__(self, 
                 chromadb_dir: str = None,
                 neo4j_uri: str = Config.NEO4J_URI,
                 neo4j_username: str = Config.NEO4J_USER,
                 neo4j_password: str = Config.NEO4J_PASSWORD,
                 collection_name: str = "knowledge_base"):
        """
        初始化知识检索器
        
        Args:
            chromadb_dir: ChromaDB数据目录
            neo4j_uri: Neo4j连接URI
            neo4j_username: Neo4j用户名
            neo4j_password: Neo4j密码
            collection_name: ChromaDB集合名称
        """
        # 初始化 ChromaDB（与 ingest / 文档管线使用同一持久化目录）
        if chromadb_dir is None:
            cfg_path = Path(Config.CHROMADB_PERSIST_DIR)
            if cfg_path.is_absolute():
                chromadb_dir = str(cfg_path)
            else:
                chromadb_dir = str((project_root / cfg_path).resolve())
        
        self.vector_store = ChromaDBVectorStore(persist_directory=chromadb_dir)
        self.collection_name = collection_name
        
        # 初始化Neo4j
        self.graph_db = Neo4jGraphDB(
            uri=neo4j_uri,
            username=neo4j_username,
            password=neo4j_password,
            auto_start=False,
            max_retries=2
        )
        
        logger.info("知识检索器初始化完成")
    
    def search_dialogues(
        self,
        query: str,
        n_results: int = 5,
        min_score: float = 0.0,
        kb_id: Optional[str] = None,
    ) -> List[Dict]:
        """
        搜索相关对话（使用向量相似度）
        
        Args:
            query: 查询文本
            n_results: 返回结果数量
            min_score: 最小相似度分数
            kb_id: 若指定，则在集合 ``kb_{kb_id}`` 中检索（与文档入库约定一致）；
                   若为 None，则使用构造时的 ``collection_name``（默认 knowledge_base，兼容旧脚本）
            
        Returns:
            List[Dict]: 相关对话列表，包含文档、元数据、相似度
        """
        try:
            collection_name = f"kb_{kb_id}" if kb_id else self.collection_name
            where = {"kb_id": kb_id} if kb_id else None
            results = self.vector_store.search_similar(
                collection_name=collection_name,
                query_text=query,
                n_results=n_results,
                where=where,
            )
            
            # 过滤低分结果
            filtered_results = [
                r for r in results 
                if r.get('distance', 1.0) <= (1.0 - min_score)  # distance越小越相似
            ]
            
            # 格式化结果
            formatted_results = []
            for r in filtered_results:
                formatted_results.append({
                    'text': r.get('document', ''),
                    'metadata': r.get('metadata', {}),
                    'similarity': 1.0 - r.get('distance', 1.0),  # 转换为相似度分数
                    'id': r.get('id', '')
                })
            
            logger.debug(f"找到 {len(formatted_results)} 条相关对话")
            return formatted_results
            
        except Exception as e:
            logger.error(f"搜索对话失败: {e}")
            return []
    
    def get_character_dialogues(
        self,
        character_name: str,
        limit: int = 10,
        kb_id: Optional[str] = None,
    ) -> List[Dict]:
        """
        获取角色的对话（使用图数据库）
        
        Args:
            character_name: 角色名称
            limit: 返回数量限制
            kb_id: 若指定，仅返回该知识库下的文档对话（与入库时 Character/Dialogue 的 kb_id 一致）
            
        Returns:
            List[Dict]: 角色对话列表
        """
        try:
            if not self.graph_db or not self.graph_db.driver:
                logger.warning("Neo4j未连接，无法查询角色对话")
                return []

            if kb_id:
                query = """
                MATCH (c:Character {name: $char_name, kb_id: $kb_id})-[:SPEAKS]->(d:Dialogue {kb_id: $kb_id})
                OPTIONAL MATCH (d)-[r:BELONGS_TO]->(ch:Chapter {kb_id: $kb_id})
                RETURN d.id as dialogue_id,
                       d.text as text,
                       ch.name as chapter_name,
                       ch.number as chapter_number,
                       r.start_line as start_line,
                       r.end_line as end_line
                ORDER BY ch.number, r.start_line
                LIMIT $limit
                """
                params = {"char_name": character_name, "kb_id": kb_id, "limit": limit}
            else:
                query = """
                MATCH (c:Character {name: $char_name})-[:SPEAKS]->(d:Dialogue)
                OPTIONAL MATCH (d)-[r:BELONGS_TO]->(ch:Chapter)
                RETURN d.id as dialogue_id,
                       d.text as text,
                       ch.name as chapter_name,
                       ch.number as chapter_number,
                       r.start_line as start_line,
                       r.end_line as end_line
                ORDER BY ch.number, r.start_line
                LIMIT $limit
                """
                params = {"char_name": character_name, "limit": limit}

            results = self.graph_db.execute_query(query, params)
            
            formatted_results = []
            for r in results:
                formatted_results.append({
                    'dialogue_id': r.get('dialogue_id', ''),
                    'text': r.get('text', ''),
                    'chapter_name': r.get('chapter_name', ''),
                    'chapter_number': r.get('chapter_number', ''),
                    'start_line': r.get('start_line', ''),
                    'end_line': r.get('end_line', '')
                })
            
            logger.debug(f"找到 {len(formatted_results)} 条 {character_name} 的对话")
            return formatted_results
            
        except Exception as e:
            logger.error(f"查询角色对话失败: {e}")
            return []
    
    def get_chapter_info(
        self,
        chapter_name: str = None,
        chapter_number: int = None,
        kb_id: Optional[str] = None,
    ) -> Dict:
        """
        获取章节信息
        
        Args:
            chapter_name: 章节名称
            chapter_number: 章节编号
            kb_id: 若指定，仅匹配该知识库下的章节
            
        Returns:
            Dict: 章节信息
        """
        try:
            if not self.graph_db or not self.graph_db.driver:
                logger.warning("Neo4j未连接，无法查询章节信息")
                return {}
            
            if chapter_number is not None:
                if kb_id:
                    query = """
                    MATCH (ch:Chapter {number: $chapter_number, kb_id: $kb_id})
                    OPTIONAL MATCH (ch)<-[:BELONGS_TO]-(d:Dialogue {kb_id: $kb_id})
                    OPTIONAL MATCH (c:Character {kb_id: $kb_id})-[:APPEARS_IN]->(ch)
                    RETURN ch.id as chapter_id,
                           ch.name as chapter_name,
                           ch.number as chapter_number,
                           ch.filename as filename,
                           count(DISTINCT d) as dialogue_count,
                           collect(DISTINCT c.name) as characters
                    """
                    params = {"chapter_number": chapter_number, "kb_id": kb_id}
                else:
                    query = """
                    MATCH (ch:Chapter {number: $chapter_number})
                    OPTIONAL MATCH (ch)<-[:BELONGS_TO]-(d:Dialogue)
                    OPTIONAL MATCH (c:Character)-[:APPEARS_IN]->(ch)
                    RETURN ch.id as chapter_id,
                           ch.name as chapter_name,
                           ch.number as chapter_number,
                           ch.filename as filename,
                           count(DISTINCT d) as dialogue_count,
                           collect(DISTINCT c.name) as characters
                    """
                    params = {"chapter_number": chapter_number}
            elif chapter_name:
                if kb_id:
                    query = """
                    MATCH (ch:Chapter {name: $chapter_name, kb_id: $kb_id})
                    OPTIONAL MATCH (ch)<-[:BELONGS_TO]-(d:Dialogue {kb_id: $kb_id})
                    OPTIONAL MATCH (c:Character {kb_id: $kb_id})-[:APPEARS_IN]->(ch)
                    RETURN ch.id as chapter_id,
                           ch.name as chapter_name,
                           ch.number as chapter_number,
                           ch.filename as filename,
                           count(DISTINCT d) as dialogue_count,
                           collect(DISTINCT c.name) as characters
                    """
                    params = {"chapter_name": chapter_name, "kb_id": kb_id}
                else:
                    query = """
                    MATCH (ch:Chapter {name: $chapter_name})
                    OPTIONAL MATCH (ch)<-[:BELONGS_TO]-(d:Dialogue)
                    OPTIONAL MATCH (c:Character)-[:APPEARS_IN]->(ch)
                    RETURN ch.id as chapter_id,
                           ch.name as chapter_name,
                           ch.number as chapter_number,
                           ch.filename as filename,
                           count(DISTINCT d) as dialogue_count,
                           collect(DISTINCT c.name) as characters
                    """
                    params = {"chapter_name": chapter_name}
            else:
                return {}
            
            results = self.graph_db.execute_query(query, params)
            if results:
                return results[0]
            return {}
            
        except Exception as e:
            logger.error(f"查询章节信息失败: {e}")
            return {}
    
    def get_character_relationships(
        self,
        character_name: str,
        kb_id: Optional[str] = None,
    ) -> List[Dict]:
        """
        获取角色的关系网络
        
        Args:
            character_name: 角色名称
            kb_id: 若指定，仅统计同一知识库内角色之间的 SPEAKS_TO
            
        Returns:
            List[Dict]: 关系列表
        """
        try:
            if not self.graph_db or not self.graph_db.driver:
                logger.warning("Neo4j未连接，无法查询角色关系")
                return []

            if kb_id:
                query = """
                MATCH (c1:Character {name: $char_name, kb_id: $kb_id})-[r:SPEAKS_TO]->(c2:Character {kb_id: $kb_id})
                RETURN c2.name as related_character,
                       count(r) as interaction_count,
                       collect(DISTINCT r.chapter)[0..5] as chapters
                ORDER BY interaction_count DESC
                LIMIT 10
                """
                params = {"char_name": character_name, "kb_id": kb_id}
            else:
                query = """
                MATCH (c1:Character {name: $char_name})-[r:SPEAKS_TO]->(c2:Character)
                RETURN c2.name as related_character,
                       count(r) as interaction_count,
                       collect(DISTINCT r.chapter)[0..5] as chapters
                ORDER BY interaction_count DESC
                LIMIT 10
                """
                params = {"char_name": character_name}

            results = self.graph_db.execute_query(query, params)
            
            formatted_results = []
            for r in results:
                formatted_results.append({
                    'related_character': r.get('related_character', ''),
                    'interaction_count': r.get('interaction_count', 0),
                    'chapters': r.get('chapters', [])
                })
            
            logger.debug(f"找到 {len(formatted_results)} 个与 {character_name} 相关的角色")
            return formatted_results
            
        except Exception as e:
            logger.error(f"查询角色关系失败: {e}")
            return []
    
    def hybrid_search(
        self,
        query: str,
        vector_results: int = 5,
        include_character_info: bool = True,
        include_chapter_info: bool = True,
        kb_id: Optional[str] = None,
    ) -> Dict:
        """
        混合搜索：结合向量搜索和图数据库查询
        
        Args:
            query: 查询文本
            vector_results: 向量搜索结果数量
            include_character_info: 是否包含角色信息
            include_chapter_info: 是否包含章节信息
            kb_id: 若指定，向量与图查询均限定在该知识库
            
        Returns:
            Dict: 综合搜索结果
        """
        result = {
            'query': query,
            'vector_results': [],
            'characters': [],
            'chapters': [],
            'summary': ''
        }
        
        # 1. 向量搜索
        vector_results_list = self.search_dialogues(
            query, n_results=vector_results, kb_id=kb_id
        )
        result['vector_results'] = vector_results_list
        
        # 2. 提取角色和章节信息
        if vector_results_list:
            # 从向量搜索结果中提取角色和章节
            characters = set()
            chapters = set()
            
            for r in vector_results_list:
                metadata = r.get('metadata', {})
                speakers = metadata.get('speakers', '')
                chapter_name = metadata.get('chapter_name', '')
                
                if speakers:
                    characters.update(speakers.split(','))
                if chapter_name:
                    chapters.add(chapter_name)
            
            # 3. 查询角色信息
            if include_character_info and characters:
                for char in list(characters)[:5]:  # 限制前5个角色
                    char_dialogues = self.get_character_dialogues(
                        char, limit=3, kb_id=kb_id
                    )
                    if char_dialogues:
                        result['characters'].append({
                            'name': char,
                            'sample_dialogues': char_dialogues
                        })
            
            # 4. 查询章节信息
            if include_chapter_info and chapters:
                for chapter in list(chapters)[:5]:  # 限制前5个章节
                    chapter_info = self.get_chapter_info(
                        chapter_name=chapter, kb_id=kb_id
                    )
                    if chapter_info:
                        result['chapters'].append(chapter_info)
        
        # 5. 生成摘要
        result['summary'] = self._generate_summary(result)
        
        return result
    
    def _generate_summary(self, search_result: Dict) -> str:
        """生成搜索结果摘要"""
        parts = []
        
        if search_result['vector_results']:
            parts.append(f"找到 {len(search_result['vector_results'])} 条相关对话")
        
        if search_result['characters']:
            char_names = [c['name'] for c in search_result['characters']]
            parts.append(f"涉及角色: {', '.join(char_names)}")
        
        if search_result['chapters']:
            chapter_names = [c.get('chapter_name', '') for c in search_result['chapters']]
            parts.append(f"相关章节: {', '.join(chapter_names)}")
        
        return " | ".join(parts) if parts else "未找到相关信息"
    
    def close(self):
        """关闭连接"""
        if self.graph_db:
            self.graph_db.close()
        logger.info("知识检索器连接已关闭")


# 使用示例
if __name__ == "__main__":
    # 初始化检索器
    retriever = KnowledgeRetriever()
    
    # 测试向量搜索
    print("=" * 80)
    print("测试向量搜索")
    print("=" * 80)
    results = retriever.search_dialogues("琪亚娜和芽衣的对话", n_results=3)
    for i, r in enumerate(results, 1):
        print(f"\n结果 {i}:")
        print(f"  相似度: {r['similarity']:.3f}")
        print(f"  文本: {r['text'][:100]}...")
        print(f"  章节: {r['metadata'].get('chapter_name', '')}")
        print(f"  说话人: {r['metadata'].get('speakers', '')}")
    
    # 测试图数据库查询
    print("\n" + "=" * 80)
    print("测试角色对话查询")
    print("=" * 80)
    dialogues = retriever.get_character_dialogues("琪亚娜", limit=3)
    for i, d in enumerate(dialogues, 1):
        print(f"\n对话 {i}:")
        print(f"  章节: {d['chapter_name']}")
        print(f"  文本: {d['text'][:100]}...")
    
    # 测试混合搜索
    print("\n" + "=" * 80)
    print("测试混合搜索")
    print("=" * 80)
    hybrid_result = retriever.hybrid_search("关于崩坏兽的战斗")
    print(f"摘要: {hybrid_result['summary']}")
    print(f"找到 {len(hybrid_result['vector_results'])} 条向量搜索结果")
    print(f"涉及 {len(hybrid_result['characters'])} 个角色")
    print(f"相关 {len(hybrid_result['chapters'])} 个章节")
    
    # 关闭连接
    retriever.close()

