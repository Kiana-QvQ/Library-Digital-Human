"""
Qt 管理台日志：同时输出到界面与 logs/qt_admin.log。
"""

from __future__ import annotations

import logging
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Optional

from PySide6.QtCore import QObject, Signal


def _log_file() -> Path:
    root = Path(__file__).resolve().parents[1]
    log_dir = root / "logs"
    log_dir.mkdir(parents=True, exist_ok=True)
    return log_dir / "qt_admin.log"


class QtLogBridge(QObject):
    """将 logging 记录转发到 QTextEdit。"""

    message = Signal(str)


class QtSignalLogHandler(logging.Handler):
    def __init__(self, bridge: QtLogBridge) -> None:
        super().__init__()
        self._bridge = bridge

    def emit(self, record: logging.LogRecord) -> None:
        try:
            msg = self.format(record)
            self._bridge.message.emit(msg)
        except Exception:  # noqa: BLE001
            self.handleError(record)


def setup_qt_logging(ui_bridge: Optional[QtLogBridge] = None) -> logging.Logger:
    """初始化 Qt 管理台 logger（文件 + 控制台 + 可选 UI）。"""
    logger = logging.getLogger("qt_admin")
    logger.setLevel(logging.DEBUG)
    logger.propagate = False

    if logger.handlers:
        return logger

    fmt = logging.Formatter(
        "%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )

    file_handler = RotatingFileHandler(
        _log_file(),
        maxBytes=2 * 1024 * 1024,
        backupCount=3,
        encoding="utf-8",
    )
    file_handler.setFormatter(fmt)
    logger.addHandler(file_handler)

    console = logging.StreamHandler()
    console.setFormatter(fmt)
    logger.addHandler(console)

    if ui_bridge is not None:
        ui_handler = QtSignalLogHandler(ui_bridge)
        ui_handler.setLevel(logging.INFO)
        ui_handler.setFormatter(fmt)
        logger.addHandler(ui_handler)

    logger.info("Qt 管理台日志已初始化 → %s", _log_file())
    return logger
