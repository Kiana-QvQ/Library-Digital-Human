"""
阿里云百炼 DashScope：text-embedding-v4（OpenAI 兼容模式）embedding 调用。

核心能力：从 `--text` 或 `--file` 获取文本 -> 调用 embedding -> 打印维度与前几个数。
"""

from __future__ import annotations

import argparse
from typing import List, Optional

import requests

from Chromadb_store import add_text_embeddings, get_collection


DEFAULT_BASE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings"
DEFAULT_MODEL = "text-embedding-v4"
BAILIAN_API_KEY = "sk-5c724d90aba84ef68253bd20d30879dc"


def _read_lines(path: str) -> List[str]:
    # 读取文件：每行一条文本
    with open(path, "r", encoding="utf-8") as f:
        return [ln.strip() for ln in f.readlines()]


def embed(texts: List[str], *, dimensions: Optional[int] = None) -> List[List[float]]:
    # 调用百炼 embedding（OpenAI 兼容接口）
    payload = {"model": DEFAULT_MODEL, "input": texts if len(texts) > 1 else texts[0]}
    if dimensions is not None:
        payload["dimensions"] = int(dimensions)

    headers = {"Authorization": f"Bearer {BAILIAN_API_KEY}", "Content-Type": "application/json"}
    resp = requests.post(DEFAULT_BASE_URL, headers=headers, json=payload, timeout=30.0)
    resp.raise_for_status()

    data = resp.json()
    items = data.get("data") or []
    return [it.get("embedding") or [] for it in items]


def search_similar(query_text: str, *, top_k: int = 5, dimensions: Optional[int] = None):
    """
    相似检索（从 ChromaDB 查）。
    collection 已配置 embedding_function，所以可以直接传 query_texts。
    """
    col = get_collection(name="bailian_text_embedding_v4", embedding_backend="bailian", dimensions=dimensions)
    return col.query(
        query_texts=[query_text],
        n_results=int(top_k),
        include=["documents", "metadatas", "distances"],
    )


def main() -> int:
    ap = argparse.ArgumentParser(description="百炼 text-embedding-v4：生成 embeddings")
    ap.add_argument("--text", default=None, help="单条文本")
    ap.add_argument("--file", default=None, help="从文件读取文本：每行一条")
    ap.add_argument("--dimensions", type=int, default=None, help="向量维度（可选；v4 支持 64~2048）")
    args = ap.parse_args()

    # 代码内置的 text 示例：不传 --text/--file 时默认用它
    input_texts = "衣服的质量杠杠的，很漂亮，不枉我等了这么久啊，喜欢，以后还来这里买"

    texts: List[str] = []
    if args.text:
        texts = [args.text.strip()]
    if args.file:
        texts = _read_lines(args.file)
    texts = [t for t in texts if t]
    if not texts:
        texts = [input_texts]

    embs = embed(texts, dimensions=args.dimensions)

    # 存储到本地 ChromaDB（持久化在 scripts/Embedding/chroma_data）
    col = get_collection(name="bailian_text_embedding_v4", embedding_backend="bailian", dimensions=args.dimensions)
    add_text_embeddings(
        collection=col,
        texts=texts,
        embeddings=embs,
        metadatas=[{"source": "bailian", "model": DEFAULT_MODEL, "dimensions": args.dimensions} for _ in texts],
    )

    print(f"model={DEFAULT_MODEL}")
    print(f"count={len(embs)}")
    for i, emb in enumerate(embs):
        if i >= 5:
            print(f"... ({len(embs) - 5} more)")
            break
        print(f"- index={i} dim={len(emb)} head={emb[:8]}")

    # 查找示例：用一句 query 在 ChromaDB 中做相似检索
    query_text = "质量很好，很漂亮，等了很久也值得"
    res = search_similar(query_text, top_k=5, dimensions=args.dimensions)
    top_docs = (res.get("documents") or [[]])[0]
    top_dist = (res.get("distances") or [[]])[0]
    print("\nsearch_top:")
    for i, (doc, dist) in enumerate(zip(top_docs, top_dist)):
        print(f"- rank={i+1} dist={dist} doc={str(doc)[:120]}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())