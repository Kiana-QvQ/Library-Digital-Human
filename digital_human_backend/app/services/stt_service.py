"""
STT 服务（路由 + 占位实现）
"""

import logging
from typing import Optional

from fastapi import APIRouter, HTTPException, UploadFile, File, Form
from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)
stt_router = APIRouter()


class STTResponse(BaseModel):
    """STT响应模型"""
    text: str = Field(..., description="识别出的文本")
    confidence: Optional[float] = Field(None, description="识别置信度（0-1）")
    language: Optional[str] = Field(None, description="识别的语言")


@stt_router.post("/recognize", response_model=STTResponse)
async def recognize_speech(
    audio: UploadFile = File(..., description="音频文件"),
    language: Optional[str] = Form(None, description="语言代码（如：zh, en）")
):
    """STT语音识别接口（文件上传）"""
    try:
        audio_data = await audio.read()
        if len(audio_data) > 10 * 1024 * 1024:
            raise HTTPException(status_code=400, detail="音频文件大小不能超过10MB")
        content_type = audio.content_type
        supported_formats = ["audio/wav", "audio/mpeg", "audio/mp3", "audio/flac", "audio/x-wav"]
        if content_type and content_type not in supported_formats:
            logger.warning(f"不支持的音频格式: {content_type}，尝试继续处理")

        result = await _recognize_audio(
            audio_data=audio_data,
            language=language or "zh"
        )
        return STTResponse(
            text=result["text"],
            confidence=result.get("confidence"),
            language=result.get("language")
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"STT识别失败: {e}")
        raise HTTPException(status_code=500, detail=f"STT识别失败: {str(e)}")


@stt_router.post("/recognize_base64", response_model=STTResponse)
async def recognize_speech_base64(
    audio_data: str = Form(..., description="Base64编码的音频数据"),
    language: Optional[str] = Form(None, description="语言代码")
):
    """STT语音识别接口（Base64）"""
    try:
        import base64
        try:
            audio_bytes = base64.b64decode(audio_data)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"Base64解码失败: {str(e)}")

        if len(audio_bytes) > 10 * 1024 * 1024:
            raise HTTPException(status_code=400, detail="音频数据大小不能超过10MB")

        result = await _recognize_audio(
            audio_data=audio_bytes,
            language=language or "zh"
        )
        return STTResponse(
            text=result["text"],
            confidence=result.get("confidence"),
            language=result.get("language")
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"STT识别失败: {e}")
        raise HTTPException(status_code=500, detail=f"STT识别失败: {str(e)}")


@stt_router.get("/languages")
async def get_supported_languages():
    """获取支持的语言列表（占位）"""
    try:
        languages = [
            {"code": "zh", "name": "中文"},
            {"code": "en", "name": "English"},
            {"code": "ja", "name": "日本語"},
            {"code": "ko", "name": "한국어"},
        ]
        return {"languages": languages}
    except Exception as e:
        logger.error(f"获取语言列表失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))


async def _recognize_audio(
    audio_data: bytes,
    language: str = "zh"
) -> dict:
    """
    占位实现：返回固定文本
    TODO: 接入真实 STT 服务（Whisper/Wav2Vec2 等）
    """
    logger.warning("STT服务未实现，返回占位文本")
    return {
        "text": "[STT服务未实现，请配置Whisper或其他STT服务]",
        "confidence": 0.0,
        "language": language
    }

