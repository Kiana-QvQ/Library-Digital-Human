#!/usr/bin/env python3
"""
启动减配版 Qt 管理台（编辑 data/app_config.json，无需重打包 Unity）。

用法：
  cd digital_human_backend
  python run_qt_admin.py
"""

from __future__ import annotations

import sys
from pathlib import Path

_root = Path(__file__).resolve().parent
if str(_root) not in sys.path:
    sys.path.insert(0, str(_root))

from qt_admin.main_window import run

if __name__ == "__main__":
    raise SystemExit(run())
