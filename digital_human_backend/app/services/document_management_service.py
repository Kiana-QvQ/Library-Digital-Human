"""
文档上传与构建记忆的服务与路由。

参考 RAGFlow 的 document_app 设计，提供精简能力：
- /upload:  将文件上传到某个知识库，记录文档元数据但不立即解析。
- /list:    查看某知识库下的所有文档及状态。
- /run:     触发对指定文档的解析与向量化存储（构建记忆）。
- /delete:  删除文档元数据、磁盘文件，并尽量同步删除 Chroma 中该文档向量与 Neo4j 中对应图数据。
"""

from __future__ import annotations

import logging
import os
import uuid
from typing import List, Optional

from fastapi import APIRouter, File, Form, HTTPException, UploadFile
from pydantic import BaseModel, Field

from app.brain.memory.chromadb_vector_store import ChromaDBVectorStore
from app.brain.memory.neo4j_doc_ingest import delete_document_from_neo4j
from app.services.ingest_pipeline import process_document
from app.services.knowledge_repository import (
    DocumentMeta,
    KnowledgeBaseMeta,
    create_document,
    delete_document,
    get_document,
    get_kb,
    list_documents_by_kb,
)
from app.shared.config import Config

logger = logging.getLogger(__name__)

document_router = APIRouter()

_vector_store = ChromaDBVectorStore(persist_directory=Config.CHROMADB_PERSIST_DIR)


class DocumentInfo(BaseModel):
    id: str
    kb_id: str
    name: str
    file_type: str
    size: int
    status: str
    chunk_count: int
    created_at: str
    updated_at: str

    @classmethod
    def from_meta(cls, meta: DocumentMeta) -> "DocumentInfo":
        return cls(
            id=meta.id,
            kb_id=meta.kb_id,
            name=meta.name,
            file_type=meta.file_type,
            size=meta.size,
            status=meta.status,
            chunk_count=meta.chunk_count,
            created_at=meta.created_at,
            updated_at=meta.updated_at,
        )


class DocumentListResponse(BaseModel):
    kb_id: str
    documents: List[DocumentInfo]


class RunDocumentsRequest(BaseModel):
    doc_ids: List[str] = Field(..., description="需要解析的文档ID列表")


@document_router.post("/upload", response_model=List[DocumentInfo])
async def upload_documents(
    kb_id: str = Form(..., description="目标知识库ID"),
    files: List[UploadFile] = File(..., description="要上传的文件列表"),
) -> List[DocumentInfo]:
    """
    向指定知识库上传一个或多个文档。
    仅保存文件与元数据，不立即解析为向量。
    """
    kb: Optional[KnowledgeBaseMeta] = get_kb(kb_id)
    if not kb:
        raise HTTPException(status_code=404, detail="知识库不存在")

    if not files:
        raise HTTPException(status_code=400, detail="未选择文件")

    upload_dir = os.path.join(Config.DATA_DIR, "uploads", kb_id)
    os.makedirs(upload_dir, exist_ok=True)

    results: List[DocumentInfo] = []

    for f in files:
        original_name = f.filename or ""
        if not original_name:
            continue

        ext = os.path.splitext(original_name)[1].lower()
        doc_id = str(uuid.uuid4())
        safe_name = f"{doc_id}{ext or ''}"
        file_path = os.path.join(upload_dir, safe_name)

        try:
            content = await f.read()
            with open(file_path, "wb") as out:
                out.write(content)
        except Exception as e:
            logger.error(f"保存上传文件失败: {original_name} - {e}")
            continue

        meta = create_document(
            doc_id=doc_id,
            kb_id=kb_id,
            name=original_name,
            file_path=file_path,
            file_type=ext.lstrip(".") or "unknown",
            size=len(content),
        )
        results.append(DocumentInfo.from_meta(meta))
        logger.info(
            f"上传文档成功: doc_id={meta.id}, kb_id={meta.kb_id}, name={meta.name}, size={meta.size}"
        )

    if not results:
        raise HTTPException(status_code=500, detail="文件上传失败，请检查日志")

    return results


@document_router.get("/list", response_model=DocumentListResponse)
async def list_documents(kb_id: str) -> DocumentListResponse:
    """
    列出指定知识库下的所有文档及其状态。
    """
    kb = get_kb(kb_id)
    if not kb:
        raise HTTPException(status_code=404, detail="知识库不存在")

    docs = list_documents_by_kb(kb_id)
    return DocumentListResponse(
        kb_id=kb_id, documents=[DocumentInfo.from_meta(d) for d in docs]
    )


@document_router.post("/run")
async def run_documents(req: RunDocumentsRequest) -> dict:
    """
    触发对指定文档的解析与向量化存储（构建记忆）。
    目前仅支持 txt / md 文件，其他类型会返回失败状态。
    """
    if not req.doc_ids:
        raise HTTPException(status_code=400, detail="doc_ids 不能为空")

    success_docs: List[str] = []
    failed_docs: List[str] = []

    for doc_id in req.doc_ids:
        doc = get_document(doc_id)
        if not doc:
            failed_docs.append(doc_id)
            continue
        kb = get_kb(doc.kb_id)
        if not kb:
            failed_docs.append(doc_id)
            continue

        try:
            _, ok = process_document(_vector_store, kb, doc)
            if ok:
                success_docs.append(doc_id)
            else:
                failed_docs.append(doc_id)
        except Exception as e:
            logger.error(f"文档解析失败: doc_id={doc_id} - {e}")
            failed_docs.append(doc_id)

    return {
        "success": True,
        "indexed": success_docs,
        "failed": failed_docs,
    }


@document_router.delete("/{doc_id}")
async def delete_document_endpoint(doc_id: str) -> dict:
    """
    删除文档：元数据、磁盘文件、Chroma 中该 document_id 的向量块、Neo4j 中对应 Chapter/Dialogue。
    """
    doc = get_document(doc_id)
    if not doc:
        raise HTTPException(status_code=404, detail="文档不存在")

    collection_name = f"kb_{doc.kb_id}"
    try:
        _vector_store.delete_chunks_by_document_id(collection_name, doc.id)
    except Exception as e:
        logger.warning("删除向量块失败 doc_id=%s: %s", doc.id, e)

    delete_document_from_neo4j(doc.id, doc.kb_id)

    # 删除物理文件（如果存在）
    try:
        if os.path.exists(doc.file_path):
            os.remove(doc.file_path)
    except Exception as e:
        logger.warning(f"删除文档文件失败: {doc.file_path} - {e}")

    delete_document(doc_id)
    logger.info(f"删除文档: {doc_id}")
    return {"success": True, "doc_id": doc_id}

