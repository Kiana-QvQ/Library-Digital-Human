"""
基于 RAGFlow `rag/app/naive.py` 思路的精简版分块器。

目标：
- 复用它「按分隔符切分 → 按 token 数合并成 chunk → 支持重叠」这一套思路，
- 但不引入 deepdoc / layout 模型等重依赖，只针对我们已经抽取出的纯文本做处理。

说明：
- 这里的 “token 数” 用近似的字符数代替，避免额外 tokenizer 依赖；
  如果以后接入真正的分词/Tokenizer，可以只替换 `_estimate_tokens` 实现。
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List
import logging

logger = logging.getLogger(__name__)


@dataclass
class ChunkConfig:
    """分块配置，参考 RAGFlow 的 parser_config 关键字段。"""

    chunk_token_num: int = 512
    delimiter: str = "\n!?。；！？"
    overlapped_percent: float = 10.0  # 相邻 chunk 的文本重叠比例（0–100）


def _estimate_tokens(text: str) -> int:
    """
    估算 token 数。

    - 为避免额外依赖，这里简单使用字符数近似；
    - 如果以后集成真正 tokenizer，只需要改这里即可。
    """
    if not text:
        return 0
    return len(text)


def _split_by_delimiters(text: str, delimiters: str) -> List[str]:
    """
    按给定分隔符粗略切成句子片段。
    - 这里不引入复杂正则，直接遍历字符。
    """
    if not text:
        return []

    segments: List[str] = []
    buf: List[str] = []
    deli_set = set(delimiters)

    for ch in text:
        buf.append(ch)
        if ch in deli_set:
            seg = "".join(buf).strip()
            if seg:
                segments.append(seg)
            buf = []

    # 剩余部分
    if buf:
        seg = "".join(buf).strip()
        if seg:
            segments.append(seg)

    return segments


def chunk_text(text: str, config: ChunkConfig | None = None) -> List[str]:
    """
    按 RAGFlow 思路对「整篇文本」进行分块：
    1. 先按分隔符切成较小的语义段（句子级）；
    2. 再按 chunk_token_num 合并成 chunk；
    3. 邻接 chunk 之间保留一定比例的重叠文本。
    """
    if config is None:
        config = ChunkConfig()

    if not text:
        return []

    # Step1: 先按 delimiter 切成句子级片段
    segments = _split_by_delimiters(text, config.delimiter)
    if not segments:
        # 如果用分隔符切不出东西，就直接整个文本一个 chunk
        return [text.strip()]

    max_tokens = max(1, int(config.chunk_token_num))
    overlap_ratio = max(0.0, min(100.0, float(config.overlapped_percent))) / 100.0

    chunks: List[str] = []
    current: List[str] = []
    current_tokens = 0

    for seg in segments:
        seg_tokens = _estimate_tokens(seg)

        # 如果当前 chunk 已经接近上限，再加这句就会超出，则先结算当前 chunk
        if current and current_tokens + seg_tokens > max_tokens:
            joined = "".join(current).strip()
            if joined:
                chunks.append(joined)

            # 计算重叠部分：从当前 chunk 末尾截取一部分作为下一个 chunk 的开头
            if overlap_ratio > 0 and chunks[-1]:
                overlap_len = int(len(chunks[-1]) * overlap_ratio)
                if overlap_len > 0:
                    overlap_text = chunks[-1][-overlap_len:]
                    current = [overlap_text]
                    current_tokens = _estimate_tokens(overlap_text)
                else:
                    current = []
                    current_tokens = 0
            else:
                current = []
                current_tokens = 0

        # 把当前句子追加到 chunk
        current.append(seg)
        current_tokens += seg_tokens

    # 最后一块
    if current:
        joined = "".join(current).strip()
        if joined:
            chunks.append(joined)

    logger.debug("rag_chunker: 输入长度=%d, 段数=%d, 输出chunk数=%d", len(text), len(segments), len(chunks))
    return chunks

