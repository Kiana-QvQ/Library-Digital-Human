"""
运行时配置存储（Qt 管理台写入，无需重打包 Unity；LLM 配置保存后下次对话立即生效）
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, Optional

from app.shared.config import Config

logger = logging.getLogger(__name__)


def _config_file() -> Path:
    return Path(__file__).resolve().parents[2] / "data" / "app_config.json"


@dataclass
class AppRuntimeConfig:
    backend_host: str = "0.0.0.0"
    backend_port: int = 8173
    unity_backend_host: str = "127.0.0.1"
    llm_base_url: str = ""
    llm_api_key: str = ""
    llm_default_model: str = "qwen2.5-7b-lora-library"
    llm_verify_ssl: bool = False
    llm_max_tokens: int = 512
    updated_at: Optional[str] = None

    def backend_chat_url(self) -> str:
        host = (self.unity_backend_host or "127.0.0.1").strip()
        port = int(self.backend_port or 8173)
        return f"http://{host}:{port}/api/chat"

    def to_file_dict(self, *, include_secret: bool = True) -> Dict[str, Any]:
        data: Dict[str, Any] = {
            "backendHost": self.backend_host,
            "backendPort": self.backend_port,
            "unityBackendHost": self.unity_backend_host,
            "llmBaseUrl": self.llm_base_url,
            "llmDefaultModel": self.llm_default_model,
            "llmVerifySsl": self.llm_verify_ssl,
            "llmMaxTokens": self.llm_max_tokens,
            "updatedAt": self.updated_at,
        }
        if include_secret:
            data["llmApiKey"] = self.llm_api_key
        return data

    def to_public_dict(self) -> Dict[str, Any]:
        key = self.llm_api_key or ""
        if key:
            masked = key[:4] + "****" + key[-4:] if len(key) > 8 else "****"
        else:
            masked = ""
        return {
            "backendHost": self.backend_host,
            "backendPort": self.backend_port,
            "unityBackendHost": self.unity_backend_host,
            "backendChatUrl": self.backend_chat_url(),
            "llmBaseUrl": self.llm_base_url,
            "llmApiKeyMasked": masked,
            "llmDefaultModel": self.llm_default_model,
            "llmVerifySsl": self.llm_verify_ssl,
            "llmMaxTokens": self.llm_max_tokens,
            "llmConfigured": bool(self.llm_base_url.strip()),
            "updatedAt": self.updated_at,
        }


class RuntimeConfigStore:
    """读取/保存 data/app_config.json，空字段回退到 .env。"""

    @staticmethod
    def _defaults_from_env() -> AppRuntimeConfig:
        return AppRuntimeConfig(
            backend_host=Config.HOST,
            backend_port=int(Config.PORT),
            unity_backend_host="127.0.0.1",
            llm_base_url=Config.LLM_BASE_URL,
            llm_api_key=Config.LLM_API_KEY,
            llm_default_model=Config.LLM_DEFAULT_MODEL,
            llm_verify_ssl=Config.LLM_VERIFY_SSL,
            llm_max_tokens=Config.LLM_MAX_TOKENS,
        )

    @staticmethod
    def _pick_str(raw: Dict[str, Any], key: str, default: str) -> str:
        value = raw.get(key)
        if value is None:
            return default
        text = str(value).strip()
        return text if text else default

    @classmethod
    def load(cls) -> AppRuntimeConfig:
        base = cls._defaults_from_env()
        path = _config_file()
        if not path.exists():
            return base

        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            logger.error("读取 app_config.json 失败: %s", exc)
            return base

        return AppRuntimeConfig(
            backend_host=cls._pick_str(raw, "backendHost", base.backend_host),
            backend_port=int(raw.get("backendPort", base.backend_port)),
            unity_backend_host=cls._pick_str(
                raw, "unityBackendHost", base.unity_backend_host
            ),
            llm_base_url=cls._pick_str(raw, "llmBaseUrl", base.llm_base_url),
            llm_api_key=cls._pick_str(raw, "llmApiKey", base.llm_api_key),
            llm_default_model=cls._pick_str(
                raw, "llmDefaultModel", base.llm_default_model
            ),
            llm_verify_ssl=bool(raw.get("llmVerifySsl", base.llm_verify_ssl)),
            llm_max_tokens=int(raw.get("llmMaxTokens", base.llm_max_tokens)),
            updated_at=raw.get("updatedAt"),
        )

    @classmethod
    def save(cls, updates: Dict[str, Any]) -> AppRuntimeConfig:
        current = cls.load()
        data = current.to_file_dict(include_secret=True)
        for key, value in updates.items():
            if value is None:
                continue
            if key == "llmApiKey" and not str(value).strip():
                continue
            data[key] = value
        data["updatedAt"] = datetime.now().astimezone().isoformat()

        path = _config_file()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps(data, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        logger.info("运行时配置已保存: %s", path)
        return cls.load()

    @classmethod
    def use_openai_llm(cls) -> bool:
        return bool(cls.load().llm_base_url.strip())

    @classmethod
    def validate_llm_config(cls) -> None:
        cfg = cls.load()
        if not cfg.llm_base_url.strip():
            raise ValueError("未配置学校大模型地址（llmBaseUrl）")
        if not cfg.llm_api_key.strip():
            raise ValueError("未配置学校大模型 API Key（llmApiKey）")
        if not cfg.llm_default_model.strip():
            raise ValueError("未配置模型名（llmDefaultModel）")

    @classmethod
    def create_openai_client(cls):
        from app.brain.llm.openai_chat import OpenAIChat

        cfg = cls.load()
        return OpenAIChat(
            base_url=cfg.llm_base_url,
            api_key=cfg.llm_api_key,
            model=cfg.llm_default_model,
            verify_ssl=cfg.llm_verify_ssl,
            max_tokens=cfg.llm_max_tokens,
        )
