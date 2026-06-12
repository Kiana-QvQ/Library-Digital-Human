"""
工具函数模块
Utility functions
"""

import logging
import sys
from pathlib import Path
from typing import Any, Dict, Optional, Tuple

# 全局日志配置标志
_logging_configured = False

# 配置日志
def setup_logging(log_level: str = "INFO", log_file: Optional[str] = None):
    """
    配置日志系统
    
    Args:
        log_level: 日志级别
        log_file: 日志文件路径
    """
    global _logging_configured
    
    # 避免重复配置
    if _logging_configured:
        return
    
    # 创建日志目录
    if log_file:
        log_path = Path(log_file)
        log_path.parent.mkdir(parents=True, exist_ok=True)
    
    # 配置日志格式
    log_format = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    date_format = '%Y-%m-%d %H:%M:%S'
    
    handlers = [logging.StreamHandler(sys.stdout)]
    if log_file:
        handlers.append(logging.FileHandler(log_file, encoding='utf-8'))
    
    logging.basicConfig(
        level=getattr(logging, log_level.upper(), logging.INFO),
        format=log_format,
        datefmt=date_format,
        handlers=handlers,
        force=True  # 强制重新配置
    )
    
    _logging_configured = True

# 获取日志器（不在这里初始化，由调用方决定）
def get_logger(name: str = __name__) -> logging.Logger:
    """
    获取日志器
    
    Args:
        name: 日志器名称
        
    Returns:
        logging.Logger: 日志器实例
    """
    return logging.getLogger(name)

def format_response(data: Any, success: bool = True, message: str = "") -> Dict:
    """
    格式化API响应
    
    Args:
        data: 响应数据
        success: 是否成功
        message: 消息
        
    Returns:
        Dict: 格式化的响应
    """
    return {
        "success": success,
        "message": message,
        "data": data
    }

def validate_request(data: Dict, required_fields: list) -> Tuple[bool, Optional[str]]:
    """
    验证请求数据
    
    Args:
        data: 请求数据
        required_fields: 必需字段列表
        
    Returns:
        tuple: (是否有效, 错误消息)
    """
    for field in required_fields:
        if field not in data or data[field] is None:
            return False, f"缺少必需字段: {field}"
    return True, None

