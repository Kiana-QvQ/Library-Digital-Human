"""
文档解析与向量化管线（基于 pdfplumber / python-docx / 纯文本）。

职责：
- 从文件路径读取文本（支持 txt / md / pdf / docx）。
- 简单按固定长度切分为 chunks。
- 使用 ChromaDBVectorStore 将 chunks 写入向量库集合。
- 若 Neo4j 可用，将文档解析为角色/章节/对话并写入图库（供 KnowledgeRetriever 查询）。

说明：
- 这是参考 RAGFlow 的“上传 → 解析 → 切块 → 入向量库”流程做的适配版本，
  数据落地在 ChromaDB；图数据落地在 Neo4j（可选）。
"""

from __future__ import annotations

import logging
import os
from typing import List, Tuple

from app.brain.memory.chromadb_vector_store import ChromaDBVectorStore
from app.brain.memory.neo4j_doc_ingest import (
    get_neo4j_graph_db_optional,
    ingest_document_to_neo4j,
)
from app.brain.memory.rag_chunker import ChunkConfig, chunk_text
from app.services.knowledge_repository import (
    DocumentMeta,
    KnowledgeBaseMeta,
    update_document,
)

logger = logging.getLogger(__name__)


def _read_text_from_file(path: str) -> str:
    """
    从文件读取纯文本，支持 txt / md / pdf / docx。

    - txt / md: 直接按 UTF-8 读取
    - pdf: 使用 pdfplumber 提取每一页文本
    - docx: 使用 python-docx 提取段落文本
    """
    ext = os.path.splitext(path)[1].lower()

    # 纯文本/Markdown
    if ext in {".txt", ".md", ".markdown"}:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            return f.read()

    # PDF
    if ext == ".pdf":
        try:
            import pdfplumber  # type: ignore
        except ImportError as e:
            raise RuntimeError(
                "解析 PDF 需要依赖 pdfplumber，请先在后端环境中安装：pip install pdfplumber"
            ) from e

        texts: List[str] = []
        with pdfplumber.open(path) as pdf:
            for page in pdf.pages:
                page_text = page.extract_text() or ""
                if page_text.strip():
                    texts.append(page_text)
        return "\n\n".join(texts)

    # DOCX
    if ext in {".docx"}:
        try:
            from docx import Document  # type: ignore
        except ImportError as e:
            raise RuntimeError(
                "解析 Word 需要依赖 python-docx，请先在后端环境中安装：pip install python-docx"
            ) from e

        doc = Document(path)
        paragraphs: List[str] = []
        for p in doc.paragraphs:
            t = (p.text or "").strip()
            if t:
                paragraphs.append(t)
        return "\n".join(paragraphs)

    raise ValueError(f"暂不支持的文件类型: {ext}")


def _get_collection_name_for_kb(kb: KnowledgeBaseMeta) -> str:
    """
    根据知识库ID生成集合名。
    目前约定为每个 KB 一个集合，形如: \"kb_{kb.id}\"。
    """
    return f"kb_{kb.id}"


def process_document(
    vector_store: ChromaDBVectorStore,
    kb: KnowledgeBaseMeta,
    doc: DocumentMeta,
) -> Tuple[int, bool]:
    """
    解析单个文档并写入向量库。

    Returns:
        (chunk_count, success)
    """
    try:
        text = _read_text_from_file(doc.file_path)
    except Exception as e:
        logger.error(f"读取文档失败 [{doc.id}]: {e}")
        doc.status = "failed"
        update_document(doc)
        return 0, False

    # 使用 rag_chunker 的逻辑进行分块，参数可以后续从 KB / 文档配置中透传
    cfg = ChunkConfig(
        chunk_token_num=512,
        delimiter="\n!?。；！？",
        overlapped_percent=10.0,
    )
    chunks = chunk_text(text, cfg)
    if not chunks:
        logger.warning(f"文档无有效内容，跳过构建记忆: {doc.id}")
        doc.status = "failed"
        update_document(doc)
        return 0, False

    collection_name = _get_collection_name_for_kb(kb)

    metadatas = [
        {
            "kb_id": kb.id,
            "kb_name": kb.name,
            "document_id": doc.id,
            "document_name": doc.name,
            "chunk_index": i,
        }
        for i in range(len(chunks))
    ]
    ids = [f"{doc.id}_chunk_{i}" for i in range(len(chunks))]

    ok = vector_store.add_documents(
        collection_name=collection_name,
        documents=chunks,
        metadatas=metadatas,
        ids=ids,
    )
    if not ok:
        logger.error(f"向量化写入失败: doc_id={doc.id}")
        doc.status = "failed"
        update_document(doc)
        return 0, False

    # 若 Neo4j 可用，将同一份正文解析为角色/章节/对话并写入图库（QT 上传后解析时生效）
    graph_db = get_neo4j_graph_db_optional()
    if graph_db:
        try:
            # 这里显式传入 kb.id，保证同名角色/章节在不同知识库之间相互隔离
            ingest_document_to_neo4j(
                text=text,
                doc_id=doc.id,
                doc_name=doc.name,
                kb_id=kb.id,
                graph_db=graph_db,
                file_path=doc.file_path,
            )
        finally:
            graph_db.close()

    doc.status = "indexed"
    doc.chunk_count = len(chunks)
    update_document(doc)

    logger.info(
        f"文档构建记忆完成: doc_id={doc.id}, kb_id={kb.id}, chunks={len(chunks)}"
    )
    return len(chunks), True

