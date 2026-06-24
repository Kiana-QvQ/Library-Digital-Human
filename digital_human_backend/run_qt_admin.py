#!/usr/bin/env python3
"""
启动减配版 Qt 管理台（编辑 data/app_config.json，无需重打包 Unity）。

用法：
  cd digital_human_backend
  python run_qt_admin.py
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

_root = Path(__file__).resolve().parent
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))


def _configure_qt_plugin_path() -> None:
    """Windows/Anaconda 下常见：未设置插件路径导致 qwindows 无法加载。"""
    try:
        import PySide6
    except ImportError:
        return

    pyside_dir = Path(PySide6.__file__).resolve().parent
    plugins = pyside_dir / "plugins"
    platforms = plugins / "platforms"
    os.environ.setdefault("QT_PLUGIN_PATH", str(plugins))
    os.environ.setdefault("QT_QPA_PLATFORM_PLUGIN_PATH", str(platforms))
    os.environ.setdefault("QT_QPA_PLATFORM", "windows")


_configure_qt_plugin_path()

from qt_admin.main_window import run

if __name__ == "__main__":
    raise SystemExit(run())
