"""
Ollama 本地 embeddings 示例：使用 qwen3-embedding:0.6b

用法：
  python digital_human_backend/scripts/Embedding/ollama_qwen3_embedding_0_6b.py
"""

from __future__ import annotations

from typing import List, Optional

import requests

from Chromadb_store import add_text_embeddings, get_collection

OLLAMA_BASE_URL = "http://localhost:11434"
OLLAMA_EMBED_MODEL = "qwen3-embedding:0.6b"
'''
嵌入维度：最高支持 4096，支持用户自定义输出维度，范围从 32 到 4096
qwen3-embedding：latest(4.7GB 40K)
qwen3-embedding：0.6b(639MB 32K)
'''

def embed(text: str, *, model: str = OLLAMA_EMBED_MODEL, base_url: str = OLLAMA_BASE_URL) -> List[float]:
    # Ollama embeddings 接口：POST /api/embeddings
    # payload 关键字段：model + prompt
    url = f"{base_url.rstrip('/')}/api/embeddings"
    payload = {"model": model, "prompt": text}
    r = requests.post(url, json=payload, timeout=60)
    if r.status_code >= 400:
        raise RuntimeError(f"Ollama embedding 请求失败: status={r.status_code}, body={r.text[:500]}")

    data = r.json()
    emb = data.get("embedding")
    if not isinstance(emb, list):
        raise RuntimeError(f"Ollama embedding 返回格式异常：{type(emb)}，body={r.text[:500]}")
    return emb


def search_similar(query_text: str, *, top_k: int = 5):
    """
    相似检索（从 ChromaDB 查）。
    collection 已配置 embedding_function，所以可以直接传 query_texts。
    """
    col = get_collection(name="ollama_qwen3_embedding_0_6b", embedding_backend="ollama")
    return col.query(
        query_texts=[query_text],
        n_results=int(top_k),
        include=["documents", "metadatas", "distances"],
    )


def main() -> int:
    # 内置一个 text 示例；确保模型已在 Ollama 中下载后再跑
    input_text = "衣服的质量杠杠的，很漂亮，不枉我等了这么久啊，喜欢，以后还来这里买"

    emb = embed(input_text)

    # 存储到本地 ChromaDB（持久化在 scripts/Embedding/chroma_data）
    col = get_collection(name="ollama_qwen3_embedding_0_6b", embedding_backend="ollama")
    add_text_embeddings(
        collection=col,
        texts=[input_text],
        embeddings=[emb],
        metadatas=[{"source": "ollama", "model": OLLAMA_EMBED_MODEL}],
    )

    print(f"model={OLLAMA_EMBED_MODEL}")
    print(f"dim={len(emb)}")
    print(f"head={emb[:10]}")

    # 查找示例：用一句 query 在 ChromaDB 中做相似检索
    query_text = "这件衣服质量不错，外观也好看"
    res = search_similar(query_text, top_k=5)
    top_docs = (res.get("documents") or [[]])[0]
    top_dist = (res.get("distances") or [[]])[0]
    print("\nsearch_top:")
    for i, (doc, dist) in enumerate(zip(top_docs, top_dist)):
        print(f"- rank={i+1} dist={dist} doc={str(doc)[:120]}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())