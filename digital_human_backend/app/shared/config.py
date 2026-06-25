"""
配置管理模块 — 数字人减配后端（转发 + Qt）
"""

import os
from urllib.parse import urlparse

from dotenv import load_dotenv

load_dotenv(override=True)


def _safe_int(name: str, default: int, *, min_val: int, max_val: int) -> int:
    raw = os.getenv(name, str(default)).strip()
    try:
        value = int(raw)
    except ValueError:
        return default
    return max(min_val, min(max_val, value))


class Config:
    """应用配置"""

    HOST = os.getenv("HOST", "0.0.0.0")
    PORT = int(os.getenv("PORT", "8173"))
    DEBUG = os.getenv("DEBUG", "False").lower() == "true"

    DATA_DIR = os.getenv("DATA_DIR", "./data")

    # 学校大模型（OpenAI 兼容）；配置后 /api/chat 纯转发
    LLM_BASE_URL = os.getenv("LLM_BASE_URL", "").strip()
    LLM_API_KEY = os.getenv("LLM_API_KEY", "")
    LLM_DEFAULT_MODEL = os.getenv("LLM_DEFAULT_MODEL", "qwen2.5-7b-lora-library")
    LLM_VERIFY_SSL = os.getenv("LLM_VERIFY_SSL", "false").lower() == "true"
    LLM_MAX_TOKENS = _safe_int("LLM_MAX_TOKENS", 512, min_val=1, max_val=768)
    LLM_MAX_INPUT_CHARS = _safe_int("LLM_MAX_INPUT_CHARS", 1800, min_val=256, max_val=4000)
    LLM_MAX_HISTORY_TURNS = _safe_int("LLM_MAX_HISTORY_TURNS", 3, min_val=0, max_val=10)
    LLM_MAX_MESSAGE_CHARS = _safe_int("LLM_MAX_MESSAGE_CHARS", 800, min_val=64, max_val=2000)

    CORS_ORIGINS = os.getenv("CORS_ORIGINS", "*").split(",")
    LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO")
    LOG_FILE = os.getenv("LOG_FILE", "logs/app.log")

    @classmethod
    def use_openai_llm(cls) -> bool:
        return bool(cls.LLM_BASE_URL)

    @classmethod
    def validate_llm_config(cls) -> None:
        if not cls.use_openai_llm():
            raise ValueError("未配置学校大模型地址（LLM_BASE_URL）")
        parsed = urlparse(cls.LLM_BASE_URL)
        if parsed.scheme not in ("http", "https") or not parsed.netloc:
            raise ValueError(f"LLM_BASE_URL 无效: {cls.LLM_BASE_URL}")
        if not cls.LLM_API_KEY:
            raise ValueError("已配置 LLM_BASE_URL 但 LLM_API_KEY 为空")
        if not cls.LLM_DEFAULT_MODEL.strip():
            raise ValueError("LLM_DEFAULT_MODEL 不能为空")
