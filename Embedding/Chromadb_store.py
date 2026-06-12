"""
ChromaDB 向量存储封装。
本地持久化：把数据写到本目录下的 `./chroma_data/`
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Dict, List, Optional
from uuid import uuid4

import chromadb
import requests


def _sanitize_metadatas(metadatas: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    # Chroma metadata 的 value 不能为 None；这里统一丢弃 None 值
    out: List[Dict[str, Any]] = []
    for md in metadatas:
        md = md or {}
        out.append({k: v for k, v in md.items() if v is not None})
    return out


DEFAULT_DASHSCOPE_BASE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings"
DEFAULT_DASHSCOPE_MODEL = "text-embedding-v4"
DEFAULT_DASHSCOPE_API_KEY = ""

DEFAULT_OLLAMA_BASE_URL = "http://localhost:11434"
DEFAULT_OLLAMA_EMBED_MODEL = "qwen3-embedding:0.6b"


class BailianEmbeddingFunction:
    """
    百炼（DashScope OpenAI 兼容）embedding_function。
    让 Chroma 支持 query_texts / add(documents=...) 自动向量化。
    """

    def __init__(
        self,
        *,
        api_key: str = DEFAULT_DASHSCOPE_API_KEY,
        base_url: str = DEFAULT_DASHSCOPE_BASE_URL,
        model: str = DEFAULT_DASHSCOPE_MODEL,
        dimensions: Optional[int] = None,
        timeout_s: float = 30.0,
    ):
        # 建议通过环境变量注入 key（避免密钥入库）
        import os

        self.api_key = ((api_key or "").strip() or (os.getenv("DASHSCOPE_API_KEY") or "").strip())
        self.base_url = (base_url or "").strip()
        self.model = (model or "").strip()
        self.dimensions = dimensions
        self.timeout_s = float(timeout_s)

    def __call__(self, input: List[str]) -> List[List[float]]:  # chromadb embedding_function protocol
        payload: Dict[str, Any] = {"model": self.model, "input": input if len(input) > 1 else input[0]}
        if self.dimensions is not None:
            payload["dimensions"] = int(self.dimensions)
        headers = {"Authorization": f"Bearer {self.api_key}", "Content-Type": "application/json"}
        resp = requests.post(self.base_url, headers=headers, json=payload, timeout=self.timeout_s)
        resp.raise_for_status()
        data = resp.json()
        items = data.get("data") or []
        return [it.get("embedding") or [] for it in items]


class OllamaEmbeddingFunction:
    """
    Ollama /api/embeddings embedding_function。
    """

    def __init__(self, *, base_url: str = DEFAULT_OLLAMA_BASE_URL, model: str = DEFAULT_OLLAMA_EMBED_MODEL):
        self.base_url = (base_url or "").rstrip("/")
        self.model = (model or "").strip()

    def __call__(self, input: List[str]) -> List[List[float]]:
        url = f"{self.base_url}/api/embeddings"
        out: List[List[float]] = []
        for text in input:
            payload = {"model": self.model, "prompt": text}
            r = requests.post(url, json=payload, timeout=60)
            r.raise_for_status()
            emb = (r.json() or {}).get("embedding")
            if not isinstance(emb, list):
                raise RuntimeError(f"Ollama embedding 返回格式异常: {r.text[:500]}")
            out.append(emb)
        return out


def get_collection(
    *,
    name: str = "embeddings",
    persist_dir: Optional[str] = None,
    embedding_backend: Optional[str] = None,
    dimensions: Optional[int] = None,
):
    base_dir = Path(__file__).resolve().parent
    persist_path = Path(persist_dir) if persist_dir else (base_dir / "chroma_data")
    persist_path.mkdir(parents=True, exist_ok=True)

    client = chromadb.PersistentClient(path=str(persist_path))
    embedding_function = None
    if embedding_backend == "bailian":
        embedding_function = BailianEmbeddingFunction(dimensions=dimensions)
    elif embedding_backend == "ollama":
        embedding_function = OllamaEmbeddingFunction()

    return client.get_or_create_collection(name=name, embedding_function=embedding_function)


def add_text_embeddings(
    *,
    collection,
    texts: List[str],
    embeddings: Optional[List[List[float]]] = None,
    metadatas: Optional[List[Dict[str, Any]]] = None,
    ids: Optional[List[str]] = None,
) -> List[str]:
    if embeddings is not None and len(texts) != len(embeddings):
        raise ValueError(f"texts 与 embeddings 数量不一致: {len(texts)} != {len(embeddings)}")

    if ids is None:
        ids = [uuid4().hex for _ in texts]

    if metadatas is None:
        metadatas = [{} for _ in texts]
    metadatas = _sanitize_metadatas(metadatas)

    # Chroma: documents=原文, embeddings=向量, metadatas=额外字段, ids=主键
    if embeddings is None:
        # 交给 collection 的 embedding_function 自动向量化
        collection.add(ids=ids, documents=texts, metadatas=metadatas)
    else:
        collection.add(ids=ids, documents=texts, embeddings=embeddings, metadatas=metadatas)
    return ids