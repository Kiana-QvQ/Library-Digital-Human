"""
共享模块
Shared utilities and configurations
"""

from .config import Config, MODEL_CONFIGS
from .utils import get_logger

# 提供一个模块级默认 logger，方便其他模块直接 from app.shared import logger
logger = get_logger(__name__)

__all__ = ['Config', 'MODEL_CONFIGS', 'logger', 'get_logger']

