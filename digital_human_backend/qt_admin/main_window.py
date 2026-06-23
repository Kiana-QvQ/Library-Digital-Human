"""
减配版 Qt 管理台：仅管理运行时 LLM / 后端地址，不含知识库、Neo4j、mem0。
"""

from __future__ import annotations

import asyncio
import logging
import sys
from datetime import datetime
from typing import Any, Dict, Optional

import requests
from PySide6.QtCore import QThread, Signal
from PySide6.QtWidgets import (
    QApplication,
    QCheckBox,
    QFormLayout,
    QGroupBox,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QSpinBox,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.shared.runtime_config_store import RuntimeConfigStore
from qt_admin.log_handler import QtLogBridge, setup_qt_logging

logger = logging.getLogger("qt_admin")


class _AsyncWorker(QThread):
    finished_ok = Signal(object)
    finished_err = Signal(str)

    def __init__(self, coro_factory):
        super().__init__()
        self._coro_factory = coro_factory

    def run(self):
        try:
            result = asyncio.run(self._coro_factory())
            self.finished_ok.emit(result)
        except Exception as exc:  # noqa: BLE001
            self.finished_err.emit(str(exc))


class LiteAdminWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("数字人后端 · 减配管理台")
        self.resize(760, 600)

        self._log_bridge = QtLogBridge()
        setup_qt_logging(self._log_bridge)
        self._workers: list[_AsyncWorker] = []

        root = QWidget()
        self.setCentralWidget(root)
        layout = QVBoxLayout(root)

        hint = QLabel(
            "修改后保存至 data/app_config.json；后端运行中则下次对话立即生效。"
            "Unity 打包版启动时会自动拉取 backendChatUrl。"
        )
        hint.setWordWrap(True)
        layout.addWidget(hint)

        unity_box = QGroupBox("Unity 连接后端")
        unity_form = QFormLayout(unity_box)
        self.unity_host = QLineEdit("127.0.0.1")
        self.backend_port = QSpinBox()
        self.backend_port.setRange(1, 65535)
        self.backend_port.setValue(8173)
        self.backend_chat_url = QLineEdit()
        self.backend_chat_url.setReadOnly(True)
        unity_form.addRow("Unity 访问 Host", self.unity_host)
        unity_form.addRow("后端端口", self.backend_port)
        unity_form.addRow("对话地址（只读）", self.backend_chat_url)
        layout.addWidget(unity_box)

        llm_box = QGroupBox("学校大模型（OpenAI 兼容）")
        llm_form = QFormLayout(llm_box)
        self.llm_base_url = QLineEdit()
        self.llm_api_key = QLineEdit()
        self.llm_api_key.setEchoMode(QLineEdit.EchoMode.Password)
        self.llm_api_key.setPlaceholderText("留空表示不修改已保存的 Key")
        self.llm_model = QLineEdit("qwen2.5-7b-lora-library")
        self.llm_verify_ssl = QCheckBox("校验 SSL 证书（内网自签请取消勾选）")
        self.llm_max_tokens = QSpinBox()
        self.llm_max_tokens.setRange(64, 4096)
        self.llm_max_tokens.setValue(512)
        llm_form.addRow("Base URL", self.llm_base_url)
        llm_form.addRow("API Key", self.llm_api_key)
        llm_form.addRow("模型名", self.llm_model)
        llm_form.addRow("", self.llm_verify_ssl)
        llm_form.addRow("max_tokens", self.llm_max_tokens)
        layout.addWidget(llm_box)

        btn_row = QHBoxLayout()
        self.btn_reload = QPushButton("重新加载")
        self.btn_save = QPushButton("保存配置")
        self.btn_test_llm = QPushButton("测试学校模型")
        self.btn_test_chat = QPushButton("测试后端对话")
        self.btn_clear_log = QPushButton("清空日志")
        btn_row.addWidget(self.btn_reload)
        btn_row.addWidget(self.btn_save)
        btn_row.addWidget(self.btn_test_llm)
        btn_row.addWidget(self.btn_test_chat)
        btn_row.addWidget(self.btn_clear_log)
        layout.addLayout(btn_row)

        self.log = QTextEdit()
        self.log.setReadOnly(True)
        self.log.setPlaceholderText("操作日志（同时写入 logs/qt_admin.log）…")
        layout.addWidget(self.log)

        self._log_bridge.message.connect(self._append_ui_log)

        self.btn_reload.clicked.connect(self.load_from_store)
        self.btn_save.clicked.connect(self.save_config)
        self.btn_test_llm.clicked.connect(self.test_llm)
        self.btn_test_chat.clicked.connect(self.test_backend_chat)
        self.btn_clear_log.clicked.connect(self.log.clear)
        self.unity_host.textChanged.connect(self._refresh_chat_url_preview)
        self.backend_port.valueChanged.connect(self._refresh_chat_url_preview)

        self.load_from_store(clear_log=False)

    def _append_ui_log(self, text: str) -> None:
        self.log.append(text)

    def _set_busy(self, busy: bool) -> None:
        for btn in (
            self.btn_reload,
            self.btn_save,
            self.btn_test_llm,
            self.btn_test_chat,
        ):
            btn.setEnabled(not busy)

    def _refresh_chat_url_preview(self) -> None:
        host = self.unity_host.text().strip() or "127.0.0.1"
        port = self.backend_port.value()
        self.backend_chat_url.setText(f"http://{host}:{port}/api/chat")

    def _validate_form(self) -> Optional[str]:
        if not self.llm_base_url.text().strip():
            return "请填写学校大模型 Base URL"
        if not self.llm_model.text().strip():
            return "请填写模型名"
        return None

    def load_from_store(self, *, clear_log: bool = True) -> None:
        if clear_log:
            self.log.clear()
        cfg = RuntimeConfigStore.load()
        self.unity_host.setText(cfg.unity_backend_host)
        self.backend_port.setValue(int(cfg.backend_port))
        self.llm_base_url.setText(cfg.llm_base_url)
        self.llm_api_key.clear()
        self.llm_model.setText(cfg.llm_default_model)
        self.llm_verify_ssl.setChecked(cfg.llm_verify_ssl)
        self.llm_max_tokens.setValue(int(cfg.llm_max_tokens))
        self._refresh_chat_url_preview()
        masked = cfg.to_public_dict().get("llmApiKeyMasked", "")
        logger.info(
            "已加载配置 unity=%s:%s llm=%s model=%s key=%s",
            cfg.unity_backend_host,
            cfg.backend_port,
            cfg.llm_base_url or "(未设置)",
            cfg.llm_default_model,
            masked or "未设置",
        )

    def _collect_updates(self) -> Dict[str, Any]:
        updates: Dict[str, Any] = {
            "unityBackendHost": self.unity_host.text().strip() or "127.0.0.1",
            "backendPort": int(self.backend_port.value()),
            "llmBaseUrl": self.llm_base_url.text().strip(),
            "llmDefaultModel": self.llm_model.text().strip(),
            "llmVerifySsl": bool(self.llm_verify_ssl.isChecked()),
            "llmMaxTokens": int(self.llm_max_tokens.value()),
        }
        key = self.llm_api_key.text().strip()
        if key:
            updates["llmApiKey"] = key
        return updates

    def _persist_updates(self, action: str) -> bool:
        err = self._validate_form()
        if err:
            logger.warning("%s 校验失败: %s", action, err)
            QMessageBox.warning(self, "配置不完整", err)
            return False
        try:
            updates = self._collect_updates()
            cfg = RuntimeConfigStore.save(updates)
            self.llm_api_key.clear()
            logger.info(
                "%s 成功 chat_url=%s llm=%s model=%s",
                action,
                cfg.backend_chat_url(),
                cfg.llm_base_url,
                cfg.llm_default_model,
            )
            self._notify_running_backend(updates)
            return True
        except Exception as exc:  # noqa: BLE001
            logger.exception("%s 失败: %s", action, exc)
            QMessageBox.critical(self, f"{action}失败", str(exc))
            return False

    def save_config(self) -> None:
        self._persist_updates("保存配置")

    def _notify_running_backend(self, updates: Dict[str, Any]) -> None:
        """后端若在跑，PUT 一次以便 API 层记录；实际对话每次请求都会重读 json。"""
        port = int(updates.get("backendPort", 8173))
        url = f"http://127.0.0.1:{port}/api/config/app"
        try:
            resp = requests.put(url, json=updates, timeout=3)
            if resp.status_code == 200:
                logger.info("后端在线，HTTP 同步成功: %s", url)
            else:
                logger.warning(
                    "后端 HTTP 同步返回 %s（本地文件已保存）: %s",
                    resp.status_code,
                    resp.text[:200],
                )
        except requests.RequestException as exc:
            logger.info("后端未启动或不可达（%s），仅写入 app_config.json", exc)

    def test_llm(self) -> None:
        if not self._persist_updates("测试前保存"):
            return

        self._set_busy(True)
        logger.info("开始测试学校大模型连通性…")

        async def _run():
            RuntimeConfigStore.validate_llm_config()
            client = RuntimeConfigStore.create_openai_client()
            return await client.health_check()

        worker = _AsyncWorker(_run)
        worker.finished_ok.connect(self._on_llm_test_ok)
        worker.finished_err.connect(self._on_llm_test_err)
        worker.finished.connect(lambda: self._set_busy(False))
        worker.start()
        self._workers.append(worker)

    def _on_llm_test_ok(self, ok: bool) -> None:
        if ok:
            logger.info("学校大模型连通测试：成功")
            QMessageBox.information(self, "测试成功", "学校大模型连通正常")
        else:
            logger.warning("学校大模型连通测试：不可达（请确认在 172.16.59.x 内网）")
            QMessageBox.warning(self, "测试失败", "学校大模型不可达")

    def _on_llm_test_err(self, err: str) -> None:
        logger.error("学校大模型测试异常: %s", err)
        QMessageBox.critical(self, "测试失败", err)

    def test_backend_chat(self) -> None:
        if not self._persist_updates("对话测试前保存"):
            return

        port = int(self.backend_port.value())
        url = f"http://127.0.0.1:{port}/api/chat"
        payload = {
            "message": "连接测试",
            "user_id": "qt_admin",
            "memory_profile": 0,
        }
        self._set_busy(True)
        logger.info("POST %s payload=%s", url, payload)
        try:
            resp = requests.post(url, json=payload, timeout=60)
            if resp.status_code != 200:
                logger.error(
                    "对话测试失败 HTTP %s: %s",
                    resp.status_code,
                    resp.text[:300],
                )
                QMessageBox.warning(self, "测试失败", resp.text[:300])
                return
            data = resp.json()
            preview = (data.get("response") or "")[:120]
            session_id = data.get("session_id", "")
            logger.info(
                "对话测试成功 session=%s 回复预览=%s",
                session_id,
                preview,
            )
            QMessageBox.information(self, "测试成功", preview or "(空回复)")
        except requests.RequestException as exc:
            logger.error("对话测试网络错误: %s", exc)
            QMessageBox.warning(
                self,
                "测试失败",
                f"{exc}\n\n请确认后端已启动: python -m app.main",
            )
        finally:
            self._set_busy(False)


def run() -> int:
    app = QApplication(sys.argv)
    window = LiteAdminWindow()
    window.show()
    logger.info("Qt 管理台窗口已打开")
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(run())
