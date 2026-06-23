"""
配置查询 API
Config query API
"""

from __future__ import annotations

import json
import logging
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, Optional

from fastapi import APIRouter, Body, HTTPException, status
from pydantic import BaseModel, Field

from app.shared.config import Config
from app.shared.runtime_config_store import RuntimeConfigStore

router = APIRouter()
logger = logging.getLogger(__name__)


class CozeConfigResponse(BaseModel):
    """Coze 配置返回模型（提供给 Unity / Qt）。"""

    cozeApiKey: str = Field(..., description="Coze API Key，用于 Unity 前端")
    cozeAgentId: str = Field(..., description="Coze Agent ID")
    cozeBaseUrl: str = Field(..., description="Coze API 基础地址")

    # 版本与生命周期信息（用于每月轮换、预警）
    version: Optional[str] = Field(
        None,
        description="当前配置版本号（例如 2025-03，用于标记本轮 Coze Key）",
    )
    expireAt: Optional[str] = Field(
        None,
        description="当前配置预计失效时间（ISO8601 字符串）",
    )
    lastRotatedAt: Optional[str] = Field(
        None,
        description="最近一次轮换时间（ISO8601 字符串）",
    )
    daysToExpire: Optional[int] = Field(
        None,
        description="距离 expireAt 的剩余天数（后端计算，给前端直接使用）",
    )
    status: str = Field(
        "ok",
        description="配置状态：ok / expiring / expired / invalid",
    )


class CozeRotateRequest(BaseModel):
    """旋转 Coze 配置的请求体，用于内部管理接口。"""

    cozeApiKey: str = Field(..., description="新的 Coze API Key")
    cozeAgentId: str = Field(..., description="新的 Coze Agent ID")
    cozeBaseUrl: str = Field(..., description="新的 Coze API Base URL")

    version: Optional[str] = Field(
        None,
        description="本轮配置版本号（例如 2025-03）。如果不传，后端会自动使用当前日期。",
    )
    expireAt: Optional[str] = Field(
        None,
        description="预计失效时间（ISO8601 字符串）；如果不传，可以之后再通过文件手工调整。",
    )


def _coze_config_file() -> Path:
    """返回 Coze 配置文件路径（data 目录下，便于持久化与版本化）。"""
    # digital_human_backend/app/api/config.py -> digital_human_backend/data/coze_config.json
    return Path(__file__).resolve().parents[2] / "data" / "coze_config.json"


def _load_coze_file_config() -> Dict[str, Any]:
    """从 JSON 文件读取 Coze 扩展配置（如果不存在则返回空字典）。"""
    path = _coze_config_file()
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        logger.error("读取 coze_config.json 失败: %s", exc)
        return {}


def _save_coze_file_config(data: Dict[str, Any]) -> None:
    """将 Coze 配置写入 JSON 文件（覆盖写入）。"""
    path = _coze_config_file()
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    except Exception as exc:  # noqa: BLE001
        logger.error("写入 coze_config.json 失败: %s", exc)
        raise


def _build_coze_response() -> CozeConfigResponse:
    """合并环境变量与 JSON 文件，构建统一的 CozeConfigResponse。"""
    file_cfg = _load_coze_file_config()

    api_key = file_cfg.get("cozeApiKey") or Config.COZE_API_KEY or ""
    agent_id = file_cfg.get("cozeAgentId") or Config.COZE_AGENT_ID or ""
    base_url = (
        file_cfg.get("cozeBaseUrl")
        or Config.COZE_BASE_URL
        or "https://api.coze.cn/v3/chat"
    )

    version = file_cfg.get("version")
    expire_at = file_cfg.get("expireAt")
    last_rotated_at = file_cfg.get("lastRotatedAt")

    days_to_expire: Optional[int] = None
    status = "ok"

    if expire_at:
        try:
            expire_dt = datetime.fromisoformat(expire_at)
            now = datetime.now(expire_dt.tzinfo) if expire_dt.tzinfo else datetime.now()
            delta = expire_dt - now
            days_to_expire = int(delta.total_seconds() // 86400)
            if days_to_expire < 0:
                status = "expired"
            elif days_to_expire <= 3:
                status = "expiring"
        except Exception as exc:  # noqa: BLE001
            logger.warning("解析 Coze 配置 expireAt 失败: %s", exc)
            status = "invalid"

    if not api_key:
        status = "invalid"

    masked = "(empty)"
    if api_key:
        masked = api_key[:4] + "****" + api_key[-4:] if len(api_key) > 8 else "****"

    logger.info(
        "Config request: /api/config/coze api_key=%s agent_id=%s base_url=%s version=%s status=%s",
        masked,
        agent_id or "(empty)",
        base_url or "(empty)",
        version or "(none)",
        status,
    )

    return CozeConfigResponse(
        cozeApiKey=api_key,
        cozeAgentId=agent_id,
        cozeBaseUrl=base_url,
        version=version,
        expireAt=expire_at,
        lastRotatedAt=last_rotated_at,
        daysToExpire=days_to_expire,
        status=status,
    )


@router.get("/coze", response_model=CozeConfigResponse)
async def get_coze_config() -> CozeConfigResponse:
    """
    获取 Coze 配置，提供给 Unity/Qt 用来更新 CozeAgentClient。
    优先从 data/coze_config.json 读取（支持版本/过期时间），未配置则回退到环境变量。
    """
    return _build_coze_response()


@router.post(
    "/coze/rotate",
    response_model=CozeConfigResponse,
    status_code=status.HTTP_200_OK,
)
async def rotate_coze_config(payload: CozeRotateRequest = Body(...)) -> CozeConfigResponse:
    """
    轮换 Coze 配置的内部接口（建议只在内网或带鉴权环境下使用）。

    - 将新的 Key/Agent/BaseURL 写入 data/coze_config.json
    - 自动记录 lastRotatedAt 与默认 version（如果未传）
    - 返回最新的 CozeConfigResponse，便于调用方立刻刷新
    """
    now = datetime.now().astimezone()

    file_cfg: Dict[str, Any] = {
        "cozeApiKey": payload.cozeApiKey,
        "cozeAgentId": payload.cozeAgentId,
        "cozeBaseUrl": payload.cozeBaseUrl,
        "version": payload.version or now.strftime("%Y-%m"),
        "lastRotatedAt": now.isoformat(),
        "expireAt": payload.expireAt,
    }

    try:
        _save_coze_file_config(file_cfg)
    except Exception as exc:  # noqa: BLE001
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"写入 Coze 配置失败: {exc}",
        ) from exc

    logger.info(
        "Coze 配置已轮换：version=%s expireAt=%s",
        file_cfg.get("version"),
        file_cfg.get("expireAt") or "(none)",
    )

    return _build_coze_response()


@router.post(
    "/coze/test",
    status_code=status.HTTP_200_OK,
)
async def test_coze_config() -> Dict[str, Any]:
    """
    使用当前配置对 Coze 进行一次轻量级连通性测试。

    这里暂时只返回当前配置的状态与掩码信息；
    如果未来你有稳定的 Coze 健康检查接口，可以在这里补充真实 HTTP 调用。
    """
    cfg = _build_coze_response()
    masked = "(empty)"
    if cfg.cozeApiKey:
        masked = (
            cfg.cozeApiKey[:4] + "****" + cfg.cozeApiKey[-4:]
            if len(cfg.cozeApiKey) > 8
            else "****"
        )

    return {
        "status": cfg.status,
        "version": cfg.version,
        "expireAt": cfg.expireAt,
        "daysToExpire": cfg.daysToExpire,
        "cozeApiKey_masked": masked,
        "cozeAgentId": cfg.cozeAgentId,
        "cozeBaseUrl": cfg.cozeBaseUrl,
    }


class AppConfigResponse(BaseModel):
    backendHost: str
    backendPort: int
    unityBackendHost: str
    backendChatUrl: str
    llmBaseUrl: str
    llmApiKeyMasked: str = ""
    llmDefaultModel: str
    llmVerifySsl: bool
    llmMaxTokens: int
    llmConfigured: bool
    updatedAt: Optional[str] = None


class AppConfigUpdateRequest(BaseModel):
    backendHost: Optional[str] = None
    backendPort: Optional[int] = None
    unityBackendHost: Optional[str] = None
    llmBaseUrl: Optional[str] = None
    llmApiKey: Optional[str] = None
    llmDefaultModel: Optional[str] = None
    llmVerifySsl: Optional[bool] = None
    llmMaxTokens: Optional[int] = None


@router.get("/app", response_model=AppConfigResponse)
async def get_app_config() -> AppConfigResponse:
    """Unity / Qt 读取运行时配置（不返回完整 API Key）。"""
    data = RuntimeConfigStore.load().to_public_dict()
    return AppConfigResponse(**data)


@router.put("/app", response_model=AppConfigResponse)
async def update_app_config(payload: AppConfigUpdateRequest) -> AppConfigResponse:
    """Qt 管理台保存学校大模型与 Unity 后端地址，保存后下次对话立即生效。"""
    updates = payload.model_dump(exclude_none=True)
    if not updates:
        raise HTTPException(status_code=400, detail="没有可更新的字段")
    logger.info("PUT /api/config/app fields=%s", list(updates.keys()))
    cfg = RuntimeConfigStore.save(updates)
    return AppConfigResponse(**cfg.to_public_dict())


@router.post("/app/test-llm")
async def test_app_llm_config() -> Dict[str, Any]:
    """测试当前运行时 LLM 配置（Qt 管理台用）。"""
    if not RuntimeConfigStore.use_openai_llm():
        raise HTTPException(status_code=400, detail="未配置 llmBaseUrl")
    client = RuntimeConfigStore.create_openai_client()
    ok = await client.health_check()
    cfg = RuntimeConfigStore.load()
    return {
        "llmReachable": ok,
        "llmBaseUrl": cfg.llm_base_url,
        "llmModel": cfg.llm_default_model,
        "summary": "学校大模型可用" if ok else "学校大模型不可达",
    }

