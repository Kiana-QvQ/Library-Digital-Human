"""
TTS 服务（路由 + 占位实现）
"""

import logging
import base64
from typing import Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)
tts_router = APIRouter()


class TTSRequest(BaseModel):
    """TTS请求模型"""
    text: str = Field(..., description="要合成的文本")
    voice_id: Optional[str] = Field(None, description="音色ID（可选）")
    speed: Optional[float] = Field(1.0, description="语速，范围0.5-2.0")
    pitch: Optional[float] = Field(1.0, description="音调，范围0.5-2.0")
    sample_rate: Optional[int] = Field(24000, description="采样率")


class TTSResponse(BaseModel):
    """TTS响应模型"""
    audio_data: str = Field(..., description="Base64编码的音频数据")
    format: str = Field("wav", description="音频格式")
    sample_rate: int = Field(24000, description="采样率")
    duration: Optional[float] = Field(None, description="音频时长（秒）")


@tts_router.post("/synthesize", response_model=TTSResponse)
async def synthesize_speech(request: TTSRequest):
    """TTS语音合成接口（当前为占位实现）"""
    try:
        if not request.text or not request.text.strip():
            raise HTTPException(status_code=400, detail="文本不能为空")
        if len(request.text) > 1000:
            raise HTTPException(status_code=400, detail="文本长度不能超过1000字符")
        if not (0.5 <= request.speed <= 2.0):
            raise HTTPException(status_code=400, detail="语速必须在0.5-2.0之间")
        if not (0.5 <= request.pitch <= 2.0):
            raise HTTPException(status_code=400, detail="音调必须在0.5-2.0之间")

        audio_data = await _synthesize_text(
            text=request.text,
            voice_id=request.voice_id,
            speed=request.speed,
            pitch=request.pitch,
            sample_rate=request.sample_rate
        )
        audio_base64 = base64.b64encode(audio_data).decode("utf-8")
        duration = len(request.text) * 0.1  # 粗略估算

        return TTSResponse(
            audio_data=audio_base64,
            format="wav",
            sample_rate=request.sample_rate,
            duration=duration
        )
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"TTS合成失败: {e}")
        raise HTTPException(status_code=500, detail=f"TTS合成失败: {str(e)}")


async def _synthesize_text(
    text: str,
    voice_id: Optional[str] = None,
    speed: float = 1.0,
    pitch: float = 1.0,
    sample_rate: int = 24000
) -> bytes:
    """
    占位实现：返回静音 WAV 数据
    TODO: 接入真实 TTS 服务（如 app.brain.tts.webui 等）
    """
    logger.warning("TTS服务未实现，返回空音频数据")
    sample_count = int(sample_rate * 0.1)  # 0.1秒静音
    data_size = sample_count * 2  # 16位PCM

    wav_header = bytearray(44)
    wav_header[0:4] = b'RIFF'
    wav_header[4:8] = (36 + data_size).to_bytes(4, 'little')
    wav_header[8:12] = b'WAVE'
    wav_header[12:16] = b'fmt '
    wav_header[16:20] = (16).to_bytes(4, 'little')
    wav_header[20:22] = (1).to_bytes(2, 'little')
    wav_header[22:24] = (1).to_bytes(2, 'little')
    wav_header[24:28] = sample_rate.to_bytes(4, 'little')
    wav_header[28:32] = (sample_rate * 2).to_bytes(4, 'little')
    wav_header[32:34] = (2).to_bytes(2, 'little')
    wav_header[34:36] = (16).to_bytes(2, 'little')
    wav_header[36:40] = b'data'
    wav_header[40:44] = data_size.to_bytes(4, 'little')

    silence = bytes(data_size)
    return bytes(wav_header) + silence


@tts_router.get("/voices")
async def get_available_voices():
    """获取可用的音色列表（占位）"""
    try:
        voices = [
            {"id": "default", "name": "默认音色", "language": "zh"},
        ]
        return {"voices": voices}
    except Exception as e:
        logger.error(f"获取音色列表失败: {e}")
        raise HTTPException(status_code=500, detail=str(e))

