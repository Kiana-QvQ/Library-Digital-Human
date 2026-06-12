"""
知识库（KnowledgeBase）相关路由与服务。

参考 RAGFlow 的 kb_app 设计，但做了精简：
- 只提供创建 / 列表 / 详情 / 删除四个基础能力。
"""

from __future__ import annotations

import logging
import uuid
from typing import List, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.services.knowledge_repository import (
    KnowledgeBaseMeta,
    create_kb,
    list_kb,
    get_kb,
    delete_kb,
)

logger = logging.getLogger(__name__)

knowledge_router = APIRouter()


class KnowledgeBaseCreateRequest(BaseModel):
    """创建知识库的请求体"""

    name: str = Field(..., description="知识库名称")
    description: Optional[str] = Field("", description="知识库描述")


class KnowledgeBaseResponse(BaseModel):
    """知识库基础信息返回"""

    id: str
    name: str
    description: str = ""
    created_at: str
    updated_at: str

    @classmethod
    def from_meta(cls, meta: KnowledgeBaseMeta) -> "KnowledgeBaseResponse":
        return cls(
            id=meta.id,
            name=meta.name,
            description=meta.description,
            created_at=meta.created_at,
            updated_at=meta.updated_at,
        )


@knowledge_router.post("/", response_model=KnowledgeBaseResponse)
async def create_knowledge_base(req: KnowledgeBaseCreateRequest) -> KnowledgeBaseResponse:
    """
    创建知识库。
    """
    name = req.name.strip()
    if not name:
        raise HTTPException(status_code=400, detail="知识库名称不能为空")

    kb_id = str(uuid.uuid4())
    meta = create_kb(kb_id=kb_id, name=name, description=req.description or "")
    logger.info(f"创建知识库: {kb_id} - {name}")
    return KnowledgeBaseResponse.from_meta(meta)


@knowledge_router.get("/", response_model=List[KnowledgeBaseResponse])
async def list_knowledge_bases() -> List[KnowledgeBaseResponse]:
    """
    列出所有知识库。
    """
    metas = list_kb()
    return [KnowledgeBaseResponse.from_meta(m) for m in metas]


@knowledge_router.get("/{kb_id}", response_model=KnowledgeBaseResponse)
async def get_knowledge_base_detail(kb_id: str) -> KnowledgeBaseResponse:
    """
    获取单个知识库详情。
    """
    meta = get_kb(kb_id)
    if not meta:
        raise HTTPException(status_code=404, detail="知识库不存在")
    return KnowledgeBaseResponse.from_meta(meta)


@knowledge_router.delete("/{kb_id}")
async def delete_knowledge_base(kb_id: str) -> dict:
    """
    删除知识库及其下所有文档元数据。
    实际向量数据仍保留在 Chroma 中，后续如有需要可以补充同步清理逻辑。
    """
    meta = get_kb(kb_id)
    if not meta:
        raise HTTPException(status_code=404, detail="知识库不存在")

    delete_kb(kb_id)
    logger.info(f"删除知识库: {kb_id} - {meta.name}")
    return {"success": True, "kb_id": kb_id}

