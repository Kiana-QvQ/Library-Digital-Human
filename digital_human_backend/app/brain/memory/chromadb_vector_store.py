"""
ChromaDB向量存储模块
用于存储和检索知识库的向量化表示
"""

import chromadb
from chromadb.config import Settings
from typing import List, Dict, Optional, Any, Any
import logging

logger = logging.getLogger(__name__)

class ChromaDBVectorStore:
    """ChromaDB向量存储管理器"""
    
    def __init__(self, persist_directory: str = "./data/chromadb"):
        """
        初始化ChromaDB客户端
        
        Args:
            persist_directory: 数据持久化目录
        """
        self.persist_directory = persist_directory
        self.client = chromadb.PersistentClient(
            path=persist_directory,
            settings=Settings(anonymized_telemetry=False)
        )
        self.collections = {}
        
    def create_collection(self, name: str, metadata: Optional[Dict] = None) -> bool:
        """
        创建向量集合
        
        Args:
            name: 集合名称
            metadata: 元数据
            
        Returns:
            bool: 创建是否成功
        """
        try:
            # ChromaDB要求元数据不能为空，至少需要一个键值对
            if metadata is None or len(metadata) == 0:
                metadata = {"source": "knowledge_base", "created_by": "system"}

            collection = self.client.create_collection(
                name=name,
                metadata=metadata
            )
            self.collections[name] = collection
            logger.info(f"集合 '{name}' 创建成功")
            return True
        except Exception as e:
            logger.error(f"创建集合失败: {e}")
            return False
    
    def get_collection(self, name: str):
        """
        获取集合；如果不存在则自动创建
        """
        # 内存缓存中已存在，直接返回
        if name in self.collections:
            return self.collections[name]

        try:
            # 优先尝试从持久化客户端获取已有集合
            collection = self.client.get_collection(name)
            self.collections[name] = collection
            return collection
        except Exception as e:
            logger.warning(f"获取集合 '{name}' 失败，将尝试创建新集合: {e}")

        # 如果集合不存在，尝试创建一个新的集合
        created = self.create_collection(
            name=name,
            metadata={"source": "knowledge_base", "collection": name},
        )
        if not created:
            # 维持原有失败语义：由上层捕获并记录“添加文档失败”
            raise RuntimeError(f"无法获取或创建集合: {name}")

        return self.collections[name]
    
    def add_documents(self, collection_name: str, documents: List[str], 
                     metadatas: List[Dict] = None, ids: List[str] = None) -> bool:
        """
        添加文档到向量集合
        
        Args:
            collection_name: 集合名称
            documents: 文档列表
            metadatas: 元数据列表
            ids: 文档ID列表
            
        Returns:
            bool: 添加是否成功
        """
        try:
            collection = self.get_collection(collection_name)
            collection.add(
                documents=documents,
                metadatas=metadatas or [{}] * len(documents),
                ids=ids or [f"doc_{i}" for i in range(len(documents))]
            )
            logger.info(f"成功添加 {len(documents)} 个文档到集合 '{collection_name}'")
            return True
        except Exception as e:
            logger.error(f"添加文档失败: {e}")
            return False
    
    def search_similar(
        self,
        collection_name: str,
        query_text: str,
        n_results: int = 5,
        where: Optional[Dict[str, Any]] = None,
    ) -> List[Dict]:
        """
        搜索相似文档
        
        Args:
            collection_name: 集合名称
            query_text: 查询文本
            n_results: 返回结果数量
            where: 可选的 metadata 过滤条件（Chroma where 语法）
            
        Returns:
            List[Dict]: 相似文档列表
        """
        try:
            collection = self.get_collection(collection_name)
            kwargs: Dict[str, Any] = {
                "query_texts": [query_text],
                "n_results": n_results,
            }
            if where:
                kwargs["where"] = where
            results = collection.query(**kwargs)

            docs = results.get("documents") or []
            if not docs or not docs[0]:
                return []

            row = docs[0]
            metas = (results.get("metadatas") or [[]])[0]
            dists = (results.get("distances") or [[]])[0]
            ids = (results.get("ids") or [[]])[0]

            formatted_results = []
            for i in range(len(row)):
                formatted_results.append({
                    "document": row[i],
                    "metadata": metas[i] if i < len(metas) else {},
                    "distance": dists[i] if i < len(dists) else 1.0,
                    "id": ids[i] if i < len(ids) else "",
                })

            return formatted_results
        except Exception as e:
            logger.error(f"搜索失败: {e}")
            return []
    
    def update_document(self, collection_name: str, doc_id: str, 
                       document: str, metadata: Dict = None) -> bool:
        """
        更新文档
        
        Args:
            collection_name: 集合名称
            doc_id: 文档ID
            document: 新文档内容
            metadata: 新元数据
            
        Returns:
            bool: 更新是否成功
        """
        try:
            collection = self.get_collection(collection_name)
            collection.update(
                ids=[doc_id],
                documents=[document],
                metadatas=[metadata or {}]
            )
            logger.info(f"文档 '{doc_id}' 更新成功")
            return True
        except Exception as e:
            logger.error(f"更新文档失败: {e}")
            return False
    
    def delete_document(self, collection_name: str, doc_id: str) -> bool:
        """
        删除文档
        
        Args:
            collection_name: 集合名称
            doc_id: 文档ID
            
        Returns:
            bool: 删除是否成功
        """
        try:
            collection = self.get_collection(collection_name)
            collection.delete(ids=[doc_id])
            logger.info(f"文档 '{doc_id}' 删除成功")
            return True
        except Exception as e:
            logger.error(f"删除文档失败: {e}")
            return False

    def delete_chunks_by_document_id(self, collection_name: str, document_id: str) -> bool:
        """
        按元数据 document_id 删除某知识库集合内该文档的所有分块向量
        （与 ingest_pipeline 写入时的 chunk id / metadata 约定一致）。
        """
        try:
            collection = self.get_collection(collection_name)
            batch = collection.get(where={"document_id": document_id})
            ids = batch.get("ids") or []
            if not ids:
                logger.debug(
                    "集合 '%s' 中无 document_id=%s 的向量",
                    collection_name,
                    document_id,
                )
                return True
            collection.delete(ids=ids)
            logger.info(
                "已从集合 '%s' 删除 %d 条向量 (document_id=%s)",
                collection_name,
                len(ids),
                document_id,
            )
            return True
        except Exception as e:
            logger.error(f"按文档删除向量失败: {e}")
            return False
    
    def get_collection_info(self, collection_name: str) -> Dict:
        """
        获取集合信息
        
        Args:
            collection_name: 集合名称
            
        Returns:
            Dict: 集合信息
        """
        try:
            collection = self.get_collection(collection_name)
            count = collection.count()
            return {
                'name': collection_name,
                'count': count,
                'metadata': collection.metadata
            }
        except Exception as e:
            logger.error(f"获取集合信息失败: {e}")
            return {}
    
    def list_collections(self) -> List[str]:
        """列出所有集合"""
        try:
            collections = self.client.list_collections()
            return [col.name for col in collections]
        except Exception as e:
            logger.error(f"列出集合失败: {e}")
            return []
    
    def delete_collection(self, name: str) -> bool:
        """
        删除集合
        
        Args:
            name: 集合名称
            
        Returns:
            bool: 删除是否成功
        """
        try:
            if name in self.collections:
                del self.collections[name]
            self.client.delete_collection(name)
            logger.info(f"集合 '{name}' 删除成功")
            return True
        except Exception as e:
            logger.error(f"删除集合失败: {e}")
            return False


# 使用示例
if __name__ == "__main__":
    # 初始化向量存储
    vector_store = ChromaDBVectorStore()
    
    # 创建知识库集合
    vector_store.create_collection("knowledge_base")
    
    # 添加文档
    documents = [
        "人工智能是计算机科学的一个分支",
        "机器学习是人工智能的核心技术",
        "深度学习是机器学习的一个子领域"
    ]
    
    vector_store.add_documents("knowledge_base", documents)
    
    # 搜索相似文档
    results = vector_store.search_similar("knowledge_base", "什么是AI？", n_results=3)
    for result in results:
        print(f"相似度: {result['distance']:.3f}")
        print(f"内容: {result['document']}")
        print("---")
