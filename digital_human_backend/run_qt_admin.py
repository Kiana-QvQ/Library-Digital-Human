#!/usr/bin/env python3
"""
启动减配版 Qt 管理台（编辑 data/app_config.json，无需重打包 Unity）。

用法：
  cd digital_human_backend
  python run_qt_admin.py

Windows + Anaconda 若仍失败，可双击 run_qt_admin.bat
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

_root = Path(__file__).resolve().parent
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))


def _configure_qt_env() -> None:
    """
    Windows/Anaconda 下 PySide6 需要：
    1. QT_PLUGIN_PATH / QT_QPA_PLATFORM_PLUGIN_PATH
    2. 将 PySide6 目录加入 PATH（qwindows.dll 依赖 Qt6Core.dll 等）
    """
    try:
        import PySide6
    except ImportError:
        return

    pyside_dir = Path(PySide6.__file__).resolve().parent
    plugins = pyside_dir / "plugins"
    platforms = plugins / "platforms"

    os.environ["QT_PLUGIN_PATH"] = str(plugins)
    os.environ["QT_QPA_PLATFORM_PLUGIN_PATH"] = str(platforms)
    os.environ.setdefault("QT_QPA_PLATFORM", "windows")

    # 关键：必须把 PySide6 根目录加入 PATH，否则 qwindows.dll 加载失败
    pyside_str = str(pyside_dir)
    path_entries = os.environ.get("PATH", "").split(os.pathsep)
    if pyside_str not in path_entries:
        os.environ["PATH"] = pyside_str + os.pathsep + os.environ.get("PATH", "")


_configure_qt_env()

from qt_admin.main_window import run

if __name__ == "__main__":
    raise SystemExit(run())
