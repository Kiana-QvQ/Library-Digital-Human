"""
配置管理模块
Configuration management
"""

import os
from typing import Dict, Any
from urllib.parse import urlparse
from dotenv import load_dotenv

# 加载环境变量（允许 .env 覆盖系统已有环境变量，确保以项目配置为准）
load_dotenv(override=True)


def _safe_int(name: str, default: int, *, min_val: int, max_val: int) -> int:
    raw = os.getenv(name, str(default)).strip()
    try:
        value = int(raw)
    except ValueError:
        return default
    return max(min_val, min(max_val, value))

class Config:
    """应用配置类"""
    
    # 服务配置
    HOST = os.getenv("HOST", "0.0.0.0")
    PORT = int(os.getenv("PORT", "8000"))
    DEBUG = os.getenv("DEBUG", "False").lower() == "true"
    
    # 数据库配置
    DATABASE_URL = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/digital_human")
    REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379")
    MONGODB_URL = os.getenv("MONGODB_URL", "mongodb://localhost:27017")
    
    # Neo4j配置
    NEO4J_URI = os.getenv("NEO4J_URI", "bolt://localhost:7687")
    NEO4J_USER = os.getenv("NEO4J_USER", "neo4j")
    NEO4J_PASSWORD = os.getenv("NEO4J_PASSWORD", "neo4jneo4j")
    # 图入库：文档路径中包含以下任一子串时，强制「一文件一个 Chapter」（不按正文内 ## / 第N章 再切分）
    NEO4J_SINGLE_CHAPTER_PATH_MARKERS = tuple(
        m.strip()
        for m in os.getenv("NEO4J_SINGLE_CHAPTER_PATH_MARKERS", "崩坏3").split(",")
        if m.strip()
    )
    # 与路径规则二选一满足即可：原始文件名匹配该正则时也强制一文件一章（默认匹配 [P01]… 类剧情文件名）
    _scrx = os.getenv("NEO4J_SINGLE_CHAPTER_FILENAME_REGEX", r"^\[P\d+\]").strip()
    NEO4J_SINGLE_CHAPTER_FILENAME_REGEX = _scrx or None
    
    # ChromaDB配置
    CHROMADB_PERSIST_DIR = os.getenv("CHROMADB_PERSIST_DIR", "./data/chromadb")

    # 本地 /api/chat：请求未传 kb_id 时使用的默认知识库；留空表示不绑定知识库
    _default_chat_kb = os.getenv("DEFAULT_CHAT_KB_ID", "").strip()
    DEFAULT_CHAT_KB_ID = _default_chat_kb or None
    
    # AI模型配置
    OPENAI_API_KEY = os.getenv("OPENAI_API_KEY", "")
    HUGGINGFACE_TOKEN = os.getenv("HUGGINGFACE_TOKEN", "")

    # 学校私有大模型（OpenAI 兼容）；设置 LLM_BASE_URL 后 /api/chat 优先走此路线
    LLM_BASE_URL = os.getenv("LLM_BASE_URL", "").strip()
    LLM_API_KEY = os.getenv("LLM_API_KEY", "")
    LLM_DEFAULT_MODEL = os.getenv("LLM_DEFAULT_MODEL", "qwen2.5-7b-lora-library")
    LLM_VERIFY_SSL = os.getenv("LLM_VERIFY_SSL", "false").lower() == "true"
    LLM_MAX_TOKENS = _safe_int("LLM_MAX_TOKENS", 512, min_val=1, max_val=768)
    # 对方服务总 token 上限 1024；限制输入字符与历史轮数，避免 500
    LLM_MAX_INPUT_CHARS = _safe_int("LLM_MAX_INPUT_CHARS", 1800, min_val=256, max_val=4000)
    LLM_MAX_HISTORY_TURNS = _safe_int("LLM_MAX_HISTORY_TURNS", 3, min_val=0, max_val=10)
    LLM_MAX_MESSAGE_CHARS = _safe_int("LLM_MAX_MESSAGE_CHARS", 800, min_val=64, max_val=2000)

    OLLAMA_BASE_URL = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
    # 默认模型：尽量选择“更可能存在”的本地模型名。
    # 你仍然可以通过环境变量 OLLAMA_DEFAULT_MODEL 覆盖为自己实际存在的 tag。
    OLLAMA_DEFAULT_MODEL = os.getenv("OLLAMA_DEFAULT_MODEL", "gemma3:1b")

    # Ollama 模型映射（用于 Unity 的 Chatglm / Qwen 选择）
    OLLAMA_MODEL_QWEN = os.getenv("OLLAMA_MODEL_QWEN", OLLAMA_DEFAULT_MODEL)
    OLLAMA_MODEL_CHATGLM = os.getenv("OLLAMA_MODEL_CHATGLM", OLLAMA_DEFAULT_MODEL)

    # Coze 配置（供 Unity 读取并同步到 CozeAgentClient）
    COZE_API_KEY = os.getenv("COZE_API_KEY", "")
    COZE_AGENT_ID = os.getenv("COZE_AGENT_ID", "")
    COZE_BASE_URL = os.getenv("COZE_BASE_URL", "https://api.coze.cn/v3/chat")

    # GLM 视觉模型配置（用于 GLM-4.6V-Flash 等）
    GLM_API_KEY = os.getenv("GLM_API_KEY", "")
    GLM_API_BASE_URL = os.getenv(
        "GLM_API_BASE_URL",
        "https://open.bigmodel.cn/api/paas/v4/chat/completions",
    )

    # 视觉后端选择：glm / ollama
    VISION_BACKEND = os.getenv("VISION_BACKEND", "glm").lower()
    
    # 模型路径
    MODELS_DIR = os.getenv("MODELS_DIR", "./models")
    DATA_DIR = os.getenv("DATA_DIR", "./data")
    
    # CORS配置
    CORS_ORIGINS = os.getenv("CORS_ORIGINS", "*").split(",")
    
    # 日志配置
    LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO")
    LOG_FILE = os.getenv("LOG_FILE", "logs/app.log")

    # mem0 / Qdrant（长期对话记忆）
    # 建议使用 Docker 方式运行 Qdrant（避免本地嵌入式 Qdrant 的 .lock 冲突）。
    MEM0_QDRANT_URL = os.getenv("MEM0_QDRANT_URL", "http://localhost:6333")
    MEM0_QDRANT_API_KEY = os.getenv("MEM0_QDRANT_API_KEY", "")
    MEM0_QDRANT_COLLECTION = os.getenv("MEM0_QDRANT_COLLECTION", "mem0")
    MEM0_QDRANT_HOST = os.getenv("MEM0_QDRANT_HOST", "localhost")
    MEM0_QDRANT_PORT = int(os.getenv("MEM0_QDRANT_PORT", "6333"))

    # mem0 使用的本地 Ollama（用于事实抽取 LLM + embeddings，避免默认 OpenAI 走外网超时）
    MEM0_OLLAMA_BASE_URL = os.getenv("MEM0_OLLAMA_BASE_URL", OLLAMA_BASE_URL)
    MEM0_OLLAMA_LLM_MODEL = os.getenv("MEM0_OLLAMA_LLM_MODEL", "qwen3:latest")
    MEM0_OLLAMA_EMBED_MODEL = os.getenv("MEM0_OLLAMA_EMBED_MODEL", "nomic-embed-text:latest")
    # nomic-embed-text:latest 在你本机返回 768 维；需与 Qdrant collection 向量维度一致
    MEM0_EMBEDDING_DIMS = int(os.getenv("MEM0_EMBEDDING_DIMS", "768"))

    @classmethod
    def use_openai_llm(cls) -> bool:
        """是否使用 OpenAI 兼容网关（配置了 LLM_BASE_URL 即为 True）"""
        return bool(cls.LLM_BASE_URL)

    @classmethod
    def validate_llm_config(cls) -> None:
        """启动时校验 LLM 配置，尽早暴露配置错误。"""
        if not cls.use_openai_llm():
            return
        parsed = urlparse(cls.LLM_BASE_URL)
        if parsed.scheme not in ("http", "https") or not parsed.netloc:
            raise ValueError(f"LLM_BASE_URL 无效: {cls.LLM_BASE_URL}")
        if not cls.LLM_API_KEY:
            raise ValueError("已配置 LLM_BASE_URL 但 LLM_API_KEY 为空")
        if not cls.LLM_DEFAULT_MODEL.strip():
            raise ValueError("LLM_DEFAULT_MODEL 不能为空")

# 模型配置
MODEL_CONFIGS = {
    "llm": {
        # 与 Config.OLLAMA_DEFAULT_MODEL 保持一致，避免文档/默认值不一致导致指向不存在的模型
        "default_model": Config.OLLAMA_DEFAULT_MODEL,
        "max_tokens": 2048,
        "temperature": 0.7,
        "top_p": 0.9,
        "base_url": Config.OLLAMA_BASE_URL
    },
    "vlm": {
        # 这里的 vlm 主要用于本地视觉模型（如 Ollama 视觉）
        "default_model": os.getenv("OLLAMA_VISION_MODEL", "qwen2.5vl:3b"),
        "image_size": 224,
        "base_url": Config.OLLAMA_BASE_URL,
    },
    "asr": {
        "default_model": "whisper-base",
        "language": "zh"
    },
    "tts": {
        "default_model": "vits",
        "speaker_id": 0
    }
}

