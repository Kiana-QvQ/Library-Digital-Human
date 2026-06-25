"""
Shared utilities and configuration.
"""

from .config import Config
from .utils import get_logger

logger = get_logger(__name__)

__all__ = ["Config", "logger", "get_logger"]
