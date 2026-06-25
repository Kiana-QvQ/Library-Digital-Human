"""
FastAPI application entry point — 数字人减配后端（对话转发 + Qt 配置）
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
from app.shared.config import Config
from app.shared.runtime_config_store import RuntimeConfigStore
from app.shared.utils import setup_logging

setup_logging(Config.LOG_LEVEL, Config.LOG_FILE)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Digital Human Backend",
    description="Unity 数字人减配后端：接收对话并转发至学校 OpenAI 兼容大模型；Qt 管理运行时配置。",
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
app.include_router(config_router, prefix="/api/config", tags=["config"])


@app.get("/")
async def root():
    return {
        "message": "Digital Human Backend API",
        "version": "1.0.0",
        "mode": "relay",
        "docs": "/docs",
        "health": "/health",
        "diagnostic": "/api/diagnostic",
    }


def _llm_not_configured_response() -> dict:
    return {
        "backend": "ok",
        "llm_configured": False,
        "llm_reachable": False,
        "llm_provider": "openai",
        "message": "未配置学校大模型地址，请在 Qt 管理台或 .env 设置 LLM_BASE_URL",
        "summary": "后端正常，大模型未配置",
    }


@app.get("/api/diagnostic")
async def api_diagnostic():
    """供 Unity 连接状态栏 / Qt 管理台使用的诊断接口。"""
    try:
        if not RuntimeConfigStore.use_openai_llm():
            return _llm_not_configured_response()

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
        if not RuntimeConfigStore.use_openai_llm():
            return {
                "status": "healthy",
                "llm_provider": "openai",
                "llm_available": False,
                "llm_configured": False,
                "backend_chat_url": cfg.backend_chat_url(),
                "services": {"api": "running", "llm": "not_configured"},
            }

        client = RuntimeConfigStore.create_openai_client()
        llm_available = await client.health_check()
        return {
            "status": "healthy",
            "llm_provider": "openai",
            "llm_available": llm_available,
            "llm_configured": True,
            "llm_model": cfg.llm_default_model,
            "backend_chat_url": cfg.backend_chat_url(),
            "services": {
                "api": "running",
                "llm": "available" if llm_available else "unavailable",
            },
        }
    except Exception as e:
        logger.error("Health check failed: %s", e)
        return JSONResponse(
            status_code=500,
            content={"status": "unhealthy", "error": str(e)},
        )


@app.on_event("startup")
async def startup_event():
    logger.info("Starting digital human backend (relay mode)...")
    logger.info("Service URL: http://%s:%s", Config.HOST, Config.PORT)
    logger.info("API docs: http://%s:%s/docs", Config.HOST, Config.PORT)
    if RuntimeConfigStore.use_openai_llm():
        try:
            RuntimeConfigStore.validate_llm_config()
        except ValueError as exc:
            logger.warning("大模型配置不完整: %s", exc)
        cfg = RuntimeConfigStore.load()
        if not cfg.llm_verify_ssl:
            logger.warning(
                "LLM_VERIFY_SSL=false：已关闭 SSL 证书校验，仅适用于内网自签证书环境"
            )
        logger.info(
            "LLM relay: %s (model=%s)",
            cfg.llm_base_url,
            cfg.llm_default_model,
        )
    else:
        logger.warning(
            "未配置 LLM_BASE_URL；请运行 Qt 管理台或编辑 .env / data/app_config.json"
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
