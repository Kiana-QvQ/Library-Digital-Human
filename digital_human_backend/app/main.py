"""
FastAPI application entry point.
"""
import sys
from pathlib import Path
import logging

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

_root = Path(__file__).resolve().parent.parent
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))

from app.api.config import router as config_router
from app.services.chat_service import chat_router
from app.services.document_management_service import document_router
from app.services.knowledge_base_service import knowledge_router
from app.services.stt_service import stt_router
from app.services.tts_service import tts_router
from app.services.vision_service import vision_router
from app.services.websocket_service import websocket_router
from app.shared.config import Config
from app.shared.runtime_config_store import RuntimeConfigStore
from app.shared.utils import setup_logging

setup_logging(Config.LOG_LEVEL, Config.LOG_FILE)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Digital Human Backend",
    description="Backend services for chat, speech, knowledge base, and vision.",
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=Config.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(chat_router, prefix="/api/chat", tags=["chat"])
app.include_router(tts_router, prefix="/api/tts", tags=["tts"])
app.include_router(stt_router, prefix="/api/stt", tags=["stt"])
app.include_router(websocket_router, prefix="/ws", tags=["websocket"])
app.include_router(vision_router, prefix="/api/vision", tags=["vision"])
app.include_router(knowledge_router, prefix="/api/knowledge_bases", tags=["knowledge"])
app.include_router(document_router, prefix="/api/documents", tags=["documents"])
app.include_router(config_router, prefix="/api/config", tags=["config"])


@app.get("/")
async def root():
    return {
        "message": "Digital Human Backend API",
        "version": "1.0.0",
        "docs": "/docs",
        "health": "/health",
        "diagnostic": "/api/diagnostic",
    }


@app.get("/api/diagnostic")
async def api_diagnostic():
    """供 Unity 连接状态栏 / Qt 管理台使用的诊断接口。"""
    try:
        if RuntimeConfigStore.use_openai_llm():
            client = RuntimeConfigStore.create_openai_client()
            cfg = RuntimeConfigStore.load()
            llm_reachable = await client.health_check()
            if llm_reachable:
                summary = "后端与学校大模型均可用"
            else:
                summary = "后端正常，但学校大模型不可达（请确认在 172.16.59.x 内网）"
            return {
                "backend": "ok",
                "llm_configured": True,
                "llm_reachable": llm_reachable,
                "llm_provider": "openai",
                "llm_model": cfg.llm_default_model,
                "llm_base_url": cfg.llm_base_url,
                "message": summary,
                "summary": summary,
            }

        from app.brain.llm.ollama_chat import OllamaChat

        ollama = OllamaChat(
            base_url=Config.OLLAMA_BASE_URL,
            model=Config.OLLAMA_DEFAULT_MODEL,
        )
        ollama_ok = await ollama.health_check()
        summary = "Ollama 可用" if ollama_ok else "后端正常，Ollama 不可用"
        return {
            "backend": "ok",
            "llm_configured": False,
            "llm_reachable": ollama_ok,
            "llm_provider": "ollama",
            "llm_model": Config.OLLAMA_DEFAULT_MODEL,
            "message": summary,
            "summary": summary,
        }
    except Exception as e:
        logger.error("Diagnostic failed: %s", e)
        return JSONResponse(
            status_code=503,
            content={
                "backend": "error",
                "llm_configured": RuntimeConfigStore.use_openai_llm(),
                "llm_reachable": False,
                "message": "后端诊断失败",
                "summary": "后端异常，请查看日志",
            },
        )


@app.get("/health")
async def health_check():
    try:
        cfg = RuntimeConfigStore.load()
        if RuntimeConfigStore.use_openai_llm():
            client = RuntimeConfigStore.create_openai_client()
            llm_available = await client.health_check()
            return {
                "status": "healthy",
                "llm_provider": "openai",
                "llm_available": llm_available,
                "llm_model": cfg.llm_default_model,
                "backend_chat_url": cfg.backend_chat_url(),
                "services": {
                    "api": "running",
                    "llm": "available" if llm_available else "unavailable",
                },
            }

        from app.brain.llm.ollama_chat import OllamaChat

        ollama_chat = OllamaChat(
            base_url=Config.OLLAMA_BASE_URL,
            model=Config.OLLAMA_DEFAULT_MODEL,
        )
        ollama_available = await ollama_chat.health_check()

        return {
            "status": "healthy",
            "llm_provider": "ollama",
            "ollama_available": ollama_available,
            "services": {
                "api": "running",
                "ollama": "available" if ollama_available else "unavailable",
            },
        }
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        return JSONResponse(
            status_code=500,
            content={
                "status": "unhealthy",
                "error": str(e),
            },
        )


@app.on_event("startup")
async def startup_event():
    logger.info("Starting digital human backend...")
    logger.info(f"Service URL: http://{Config.HOST}:{Config.PORT}")
    logger.info(f"API docs: http://{Config.HOST}:{Config.PORT}/docs")
    if RuntimeConfigStore.use_openai_llm():
        try:
            RuntimeConfigStore.validate_llm_config()
        except ValueError as exc:
            logger.warning("OpenAI 配置不完整: %s", exc)
        cfg = RuntimeConfigStore.load()
        if not cfg.llm_verify_ssl:
            logger.warning(
                "LLM_VERIFY_SSL=false：已关闭 SSL 证书校验，仅适用于内网自签证书环境"
            )
        logger.info(
            "LLM provider: OpenAI-compatible gateway (%s, model=%s)",
            cfg.llm_base_url,
            cfg.llm_default_model,
        )
    else:
        logger.info(
            "LLM provider: Ollama (%s, model=%s)",
            Config.OLLAMA_BASE_URL,
            Config.OLLAMA_DEFAULT_MODEL,
        )


@app.on_event("shutdown")
async def shutdown_event():
    logger.info("Stopping digital human backend...")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app.main:app",
        host=Config.HOST,
        port=Config.PORT,
        reload=Config.DEBUG,
        log_level=Config.LOG_LEVEL.lower(),
    )
