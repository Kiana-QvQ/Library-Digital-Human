"""
知识库与文档元数据的简单持久化仓库。

注意：
- 为了避免一次性引入完整数据库，这里使用 JSON 文件做轻量持久化。
- 只存储「知识库」和「原始文档元数据」，真正的向量与 Chunk 仍由 ChromaDB 管理。
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict
from datetime import datetime
from threading import RLock
from typing import Dict, List, Optional

from app.shared.config import Config


STATE_FILE = os.path.join(Config.DATA_DIR, "knowledge_state.json")
_lock = RLock()


def _now_iso() -> str:
    return datetime.utcnow().isoformat()


@dataclass
class KnowledgeBaseMeta:
    id: str
    name: str
    description: str = ""
    created_at: str = ""
    updated_at: str = ""


@dataclass
class DocumentMeta:
    id: str
    kb_id: str
    name: str
    file_path: str
    file_type: str
    size: int
    status: str = "uploaded"  # uploaded / indexing / indexed / failed
    chunk_count: int = 0
    created_at: str = ""
    updated_at: str = ""


class _State:
    """内部状态容器，避免直接操作裸 dict。"""

    def __init__(self) -> None:
        self.knowledge_bases: Dict[str, KnowledgeBaseMeta] = {}
        self.documents: Dict[str, DocumentMeta] = {}

    def to_dict(self) -> Dict:
        return {
            "knowledge_bases": {k: asdict(v) for k, v in self.knowledge_bases.items()},
            "documents": {k: asdict(v) for k, v in self.documents.items()},
        }

    @classmethod
    def from_dict(cls, data: Dict) -> "_State":
        inst = cls()
        for kb_id, kb_data in (data.get("knowledge_bases") or {}).items():
            inst.knowledge_bases[kb_id] = KnowledgeBaseMeta(**kb_data)
        for doc_id, doc_data in (data.get("documents") or {}).items():
            inst.documents[doc_id] = DocumentMeta(**doc_data)
        return inst


_state = _State()


def _ensure_data_dir() -> None:
    os.makedirs(Config.DATA_DIR, exist_ok=True)


def load_state() -> None:
    """在应用启动时加载一次。"""
    global _state
    with _lock:
        _ensure_data_dir()
        if not os.path.exists(STATE_FILE):
            _state = _State()
            return
        try:
            with open(STATE_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
            _state = _State.from_dict(data)
        except Exception:
            # 如果状态文件损坏，回退为空状态，避免阻塞服务
            _state = _State()


def save_state() -> None:
    with _lock:
        _ensure_data_dir()
        tmp_path = STATE_FILE + ".tmp"
        with open(tmp_path, "w", encoding="utf-8") as f:
            json.dump(_state.to_dict(), f, ensure_ascii=False, indent=2)
        os.replace(tmp_path, STATE_FILE)


# ---- KnowledgeBase 操作 ----


def create_kb(kb_id: str, name: str, description: str = "") -> KnowledgeBaseMeta:
    now = _now_iso()
    kb = KnowledgeBaseMeta(
        id=kb_id,
        name=name,
        description=description or "",
        created_at=now,
        updated_at=now,
    )
    with _lock:
        _state.knowledge_bases[kb_id] = kb
        save_state()
    return kb


def list_kb() -> List[KnowledgeBaseMeta]:
    with _lock:
        return list(_state.knowledge_bases.values())


def get_kb(kb_id: str) -> Optional[KnowledgeBaseMeta]:
    with _lock:
        return _state.knowledge_bases.get(kb_id)


def delete_kb(kb_id: str) -> None:
    """删除 KB 以及其下的文档元数据（不直接删除 Chroma 中的向量，只是逻辑层移除）。"""
    with _lock:
        _state.knowledge_bases.pop(kb_id, None)
        # 同时移除所有隶属该 KB 的文档元数据
        _state.documents = {
            doc_id: doc
            for doc_id, doc in _state.documents.items()
            if doc.kb_id != kb_id
        }
        save_state()


# ---- Document 操作 ----


def create_document(
    doc_id: str,
    kb_id: str,
    name: str,
    file_path: str,
    file_type: str,
    size: int,
) -> DocumentMeta:
    now = _now_iso()
    doc = DocumentMeta(
        id=doc_id,
        kb_id=kb_id,
        name=name,
        file_path=file_path,
        file_type=file_type,
        size=size,
        status="uploaded",
        chunk_count=0,
        created_at=now,
        updated_at=now,
    )
    with _lock:
        _state.documents[doc_id] = doc
        save_state()
    return doc


def get_document(doc_id: str) -> Optional[DocumentMeta]:
    with _lock:
        return _state.documents.get(doc_id)


def list_documents_by_kb(kb_id: str) -> List[DocumentMeta]:
    with _lock:
        return [d for d in _state.documents.values() if d.kb_id == kb_id]


def update_document(doc: DocumentMeta) -> None:
    doc.updated_at = _now_iso()
    with _lock:
        if doc.id in _state.documents:
            _state.documents[doc.id] = doc
            save_state()


def delete_document(doc_id: str) -> None:
    with _lock:
        _state.documents.pop(doc_id, None)
        save_state()


# 在模块导入时尝试加载一次状态
load_state()

