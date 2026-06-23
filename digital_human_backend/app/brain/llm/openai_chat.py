"""
OpenAI 兼容大模型对话（学校内网私有大模型）
OpenAI-compatible LLM chat client
"""

import json
import re
import ssl
from typing import Any, Dict, List, Optional
from urllib.parse import urlparse

import aiohttp
import logging

from app.brain.llm.exceptions import LLMRequestError

logger = logging.getLogger(__name__)

_ALLOWED_ROLES = frozenset({"user", "assistant"})
_MODEL_NAME_PATTERN = re.compile(r"^[a-zA-Z0-9._\-:/]+$")


def validate_llm_base_url(url: str) -> str:
    """校验 LLM 网关地址，仅允许 http/https。"""
    cleaned = (url or "").strip().rstrip("/")
    if not cleaned:
        raise ValueError("LLM_BASE_URL 不能为空")
    parsed = urlparse(cleaned)
    if parsed.scheme not in ("http", "https"):
        raise ValueError(f"LLM_BASE_URL 仅支持 http/https: {url}")
    if not parsed.netloc:
        raise ValueError(f"LLM_BASE_URL 缺少主机名: {url}")
    return cleaned


def validate_model_name(model: str) -> str:
    """校验模型名，防止注入异常字段。"""
    cleaned = (model or "").strip()
    if not cleaned or len(cleaned) > 128:
        raise ValueError("模型名无效或过长")
    if not _MODEL_NAME_PATTERN.match(cleaned):
        raise ValueError(f"模型名包含非法字符: {cleaned}")
    return cleaned


def sanitize_messages(messages: List[Dict[str, str]]) -> List[Dict[str, str]]:
    """校验并清洗 messages，仅保留 user/assistant 且 content 非空。"""
    if not messages:
        raise LLMRequestError("对话消息不能为空", status_code=400)

    cleaned: List[Dict[str, str]] = []
    for item in messages:
        role = (item.get("role") or "").strip().lower()
        content = (item.get("content") or "").strip()
        if role not in _ALLOWED_ROLES:
            raise LLMRequestError(f"不支持的消息角色: {role}", status_code=400)
        if not content:
            continue
        cleaned.append({"role": role, "content": content})

    if not cleaned or cleaned[-1]["role"] != "user":
        raise LLMRequestError("messages 必须以 user 消息结尾", status_code=400)
    return cleaned


class OpenAIChat:
    """OpenAI 兼容 /v1/chat/completions 客户端"""

    def __init__(
        self,
        base_url: str,
        api_key: str,
        model: str = "qwen2.5-7b-lora-library",
        verify_ssl: bool = False,
        max_tokens: int = 512,
        timeout_s: float = 120.0,
    ):
        self.base_url = validate_llm_base_url(base_url)
        self.api_key = (api_key or "").strip()
        self.model = validate_model_name(model)
        self.verify_ssl = verify_ssl
        self.max_tokens = max(1, min(max_tokens, 768))
        self.timeout_s = max(5.0, min(timeout_s, 300.0))

        if not self.api_key:
            logger.warning("OpenAIChat: LLM_API_KEY 未配置，上游请求将返回 401")

    def _chat_url(self) -> str:
        return f"{self.base_url}/chat/completions"

    def _health_url(self) -> str:
        root = self.base_url
        if root.endswith("/v1"):
            root = root[:-3].rstrip("/")
        return f"{root}/"

    def _ssl_context(self) -> Optional[ssl.SSLContext]:
        if self.verify_ssl:
            return None
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        return ctx

    def _headers(self) -> Dict[str, str]:
        headers = {"Content-Type": "application/json"}
        if self.api_key:
            headers["Authorization"] = f"Bearer {self.api_key}"
        return headers

    async def chat(
        self,
        message: str,
        system_prompt: Optional[str] = None,
        model: Optional[str] = None,
    ) -> str:
        user_content = message
        if system_prompt:
            user_content = f"{system_prompt}\n\n{message}"
        return await self.chat_messages(
            [{"role": "user", "content": user_content}],
            model=model,
        )

    async def chat_messages(
        self,
        messages: List[Dict[str, str]],
        model: Optional[str] = None,
    ) -> str:
        safe_messages = sanitize_messages(messages)
        chosen_model = validate_model_name(model or self.model)

        payload: Dict[str, Any] = {
            "model": chosen_model,
            "messages": safe_messages,
            "stream": False,
            "max_tokens": self.max_tokens,
        }

        connector = aiohttp.TCPConnector(ssl=self._ssl_context())
        timeout = aiohttp.ClientTimeout(total=self.timeout_s)

        try:
            async with aiohttp.ClientSession(
                connector=connector, timeout=timeout
            ) as session:
                async with session.post(
                    self._chat_url(),
                    headers=self._headers(),
                    json=payload,
                ) as response:
                    body_text = await response.text()
                    if response.status == 401:
                        logger.error("OpenAI LLM 鉴权失败 (401)")
                        raise LLMRequestError("模型服务鉴权失败", status_code=502)
                    if response.status == 400:
                        logger.error("OpenAI LLM 参数错误 (400): %s", body_text[:300])
                        raise LLMRequestError("模型服务拒绝请求参数", status_code=400)
                    if response.status != 200:
                        logger.error(
                            "OpenAI LLM 请求失败: status=%s body=%s",
                            response.status,
                            body_text[:300],
                        )
                        raise LLMRequestError("模型服务暂时不可用", status_code=502)

                    try:
                        data = json.loads(body_text)
                    except json.JSONDecodeError:
                        logger.error("OpenAI LLM 响应非 JSON: %s", body_text[:300])
                        raise LLMRequestError("模型服务返回无效数据", status_code=502)

                    if data.get("error"):
                        err = data["error"]
                        err_text = err if isinstance(err, str) else str(err)
                        logger.error("OpenAI LLM 业务错误: %s", err_text[:300])
                        raise LLMRequestError("模型推理失败", status_code=502)

                    choices = data.get("choices") or []
                    if not choices:
                        logger.error("OpenAI LLM 响应缺少 choices: %s", body_text[:300])
                        raise LLMRequestError("模型未返回有效回复", status_code=502)

                    content = (
                        (choices[0].get("message") or {}).get("content") or ""
                    ).strip()
                    if not content:
                        logger.error("OpenAI LLM 响应 content 为空: %s", body_text[:300])
                        raise LLMRequestError("模型返回了空回复", status_code=502)
                    return content
        except LLMRequestError:
            raise
        except aiohttp.ClientError as e:
            logger.error("OpenAI LLM 网络错误: %s", e)
            raise LLMRequestError("无法连接模型服务", status_code=503) from e
        except Exception as e:
            logger.error("OpenAI LLM 调用异常: %s", e)
            raise LLMRequestError("调用模型时发生内部错误", status_code=502) from e

    async def health_check(self) -> bool:
        connector = aiohttp.TCPConnector(ssl=self._ssl_context())
        timeout = aiohttp.ClientTimeout(total=15.0)
        try:
            async with aiohttp.ClientSession(
                connector=connector, timeout=timeout
            ) as session:
                async with session.get(self._health_url()) as response:
                    if response.status != 200:
                        return False
                    body_text = await response.text()
                    try:
                        data = json.loads(body_text)
                    except json.JSONDecodeError:
                        return False
                    code = data.get("code")
                    msg = (data.get("msg") or "").lower()
                    return code == 200 or "running" in msg
        except Exception as e:
            logger.warning("OpenAI LLM 健康检查失败: %s", e)
            return False

    async def get_available_models(self) -> List[str]:
        return [self.model]
