"""
视觉服务（GLM / 本地视觉模型统一入口）
Vision service (unified entry for GLM and local vision backends)
"""

from typing import Optional, Dict, Any
import base64
import logging

import requests
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.shared.config import Config

logger = logging.getLogger(__name__)
vision_router = APIRouter()


class VisionRequest(BaseModel):
    """视觉分析请求模型"""

    image_base64: str = Field(..., description="不带 data: 前缀的纯 Base64 图片数据")
    prompt: Optional[str] = Field(
        "请详细描述这张图片的内容，包括场景、人物、物体、动作等。",
        description="视觉分析提示词",
    )
    model: Optional[str] = Field(
        None, description="可选的模型名称，默认为后端配置的模型"
    )


class VisionResponse(BaseModel):
    """视觉分析响应模型"""

    description: str = Field(..., description="视觉模型生成的描述")
    model: str = Field(..., description="实际使用的模型名称")
    backend: str = Field(..., description="后端类型：glm / ollama")
    raw: Optional[Dict[str, Any]] = Field(
        None, description="原始响应（用于调试，可选）"
    )


def _call_glm_vision(image_base64: str, prompt: str, model: Optional[str] = None) -> Dict:
    """调用 GLM-4.6V-Flash 视觉模型"""
    if not Config.GLM_API_KEY:
        raise HTTPException(status_code=500, detail="GLM_API_KEY 未配置")

    glm_model = model or "glm-4.6v-flash"

    # 构建 GLM 请求 payload（与官方 curl 示例对齐）
    payload = {
        "model": glm_model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {
                        "type": "image_url",
                        "image_url": {
                            "url": f"data:image/jpeg;base64,{image_base64}",
                        },
                    },
                    {
                        "type": "text",
                        "text": prompt,
                    },
                ],
            }
        ],
        "thinking": {"type": "enabled"},
    }

    headers = {
        "Authorization": f"Bearer {Config.GLM_API_KEY}",
        "Content-Type": "application/json",
    }

    try:
        resp = requests.post(
            Config.GLM_API_BASE_URL,
            json=payload,
            headers=headers,
            timeout=60,
        )
        resp.raise_for_status()
    except requests.exceptions.RequestException as e:
        logger.error(f"调用 GLM 视觉接口失败: {e}")
        raise HTTPException(status_code=502, detail=f"GLM 视觉服务请求失败: {e}")

    data = resp.json()
    try:
        # 兼容 GLM Chat Completions 风格的返回
        choices = data.get("choices") or []
        if not choices:
            raise ValueError("响应中未找到 choices")
        message = choices[0].get("message") or {}
        description = message.get("content") or ""
    except Exception as e:  # noqa: BLE001
        logger.error(f"解析 GLM 视觉响应失败: {e}, data={data}")
        raise HTTPException(status_code=500, detail="解析 GLM 视觉响应失败")

    return {
        "description": description,
        "model": glm_model,
        "backend": "glm",
        "raw": data,
    }


def _call_ollama_vision(image_base64: str, prompt: str, model: Optional[str] = None) -> Dict:
    """调用本地 Ollama 视觉模型（预留扩展，当前简单实现）"""
    ollama_model = model or Config.MODEL_CONFIGS["vlm"]["default_model"]  # type: ignore[attr-defined]

    url = f"{Config.OLLAMA_BASE_URL}/api/chat"
    payload = {
        "model": ollama_model,
        "messages": [
            {
                "role": "user",
                "content": prompt,
                "images": [image_base64],
            }
        ],
        "stream": False,
    }

    try:
        resp = requests.post(url, json=payload, timeout=120)
        resp.raise_for_status()
    except requests.exceptions.RequestException as e:
        logger.error(f"调用 Ollama 视觉接口失败: {e}")
        raise HTTPException(status_code=502, detail=f"Ollama 视觉服务请求失败: {e}")

    data = resp.json()
    description = (
        (data.get("message") or {}).get("content")
        or data.get("response")
        or ""
    )

    return {
        "description": description,
        "model": ollama_model,
        "backend": "ollama",
        "raw": data,
    }


@vision_router.post("/analyze", response_model=VisionResponse)
async def analyze_image(request: VisionRequest) -> VisionResponse:
    """统一的视觉分析接口

    - 输入：Base64 图片（不带 data: 前缀）+ prompt
    - 输出：description + model + backend
    """
    if not request.image_base64:
        raise HTTPException(status_code=400, detail="image_base64 不能为空")

    # 简单校验 Base64
    try:
        # 如果带有 data:image/...;base64, 前缀则去掉
        if request.image_base64.startswith("data:"):
            header, _, data_part = request.image_base64.partition("base64,")
            if not data_part:
                raise ValueError("无效的 data URL")
            image_b64 = data_part
        else:
            image_b64 = request.image_base64

        # 尝试解码验证
        _ = base64.b64decode(image_b64, validate=True)
    except Exception as e:  # noqa: BLE001
        raise HTTPException(status_code=400, detail=f"image_base64 非法: {e}")

    prompt = request.prompt or "请详细描述这张图片的内容，包括场景、人物、物体、动作等。"

    backend = Config.VISION_BACKEND
    if backend == "glm":
        result = _call_glm_vision(image_b64, prompt, model=request.model)
    elif backend == "ollama":
        result = _call_ollama_vision(image_b64, prompt, model=request.model)
    else:
        raise HTTPException(status_code=500, detail=f"不支持的 VISION_BACKEND: {backend}")

    return VisionResponse(
        description=result["description"],
        model=result["model"],
        backend=result["backend"],
        raw=result.get("raw"),
    )



