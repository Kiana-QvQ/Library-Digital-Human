"""
减配版 Qt 管理台：仅管理运行时 LLM / 后端地址，不含知识库、Neo4j、mem0。
"""

from __future__ import annotations

import asyncio
import json
import sys
from typing import Any, Dict, Optional

import requests
from PySide6.QtCore import Qt, QThread, Signal
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
    self.resize(720, 560)

    root = QWidget()
    self.setCentralWidget(root)
    layout = QVBoxLayout(root)

    hint = QLabel(
      "修改后保存至 data/app_config.json；后端运行中则下次对话立即生效，"
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
    btn_row.addWidget(self.btn_reload)
    btn_row.addWidget(self.btn_save)
    btn_row.addWidget(self.btn_test_llm)
    btn_row.addWidget(self.btn_test_chat)
    layout.addLayout(btn_row)

    self.log = QTextEdit()
    self.log.setReadOnly(True)
    self.log.setPlaceholderText("操作日志…")
    layout.addWidget(self.log)

    self.btn_reload.clicked.connect(self.load_from_store)
    self.btn_save.clicked.connect(self.save_config)
    self.btn_test_llm.clicked.connect(self.test_llm)
    self.btn_test_chat.clicked.connect(self.test_backend_chat)
    self.unity_host.textChanged.connect(self._refresh_chat_url_preview)
    self.backend_port.valueChanged.connect(self._refresh_chat_url_preview)

    self.load_from_store()

  def _append_log(self, text: str) -> None:
    self.log.append(text)

  def _refresh_chat_url_preview(self) -> None:
    host = self.unity_host.text().strip() or "127.0.0.1"
    port = self.backend_port.value()
    self.backend_chat_url.setText(f"http://{host}:{port}/api/chat")

  def load_from_store(self) -> None:
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
    self._append_log(f"已加载配置（Key 掩码: {masked or '未设置'}）")

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

  def save_config(self) -> None:
    try:
      updates = self._collect_updates()
      cfg = RuntimeConfigStore.save(updates)
      self.llm_api_key.clear()
      self._append_log(f"保存成功 → {cfg.backend_chat_url()}")
      self._try_push_via_http(updates)
    except Exception as exc:  # noqa: BLE001
      QMessageBox.critical(self, "保存失败", str(exc))

  def _try_push_via_http(self, updates: Dict[str, Any]) -> None:
    port = int(updates.get("backendPort", 8173))
    url = f"http://127.0.0.1:{port}/api/config/app"
    try:
      resp = requests.put(url, json=updates, timeout=3)
      if resp.status_code == 200:
        self._append_log("后端在线：已通过 HTTP 同步运行时配置")
      else:
        self._append_log(f"后端 HTTP 同步跳过（{resp.status_code}），文件已保存")
    except requests.RequestException:
      self._append_log("后端未启动：仅写入 app_config.json，启动后端后自动读取")

  def test_llm(self) -> None:
    try:
      RuntimeConfigStore.save(self._collect_updates())
      self.llm_api_key.clear()
    except Exception as exc:  # noqa: BLE001
      QMessageBox.critical(self, "保存失败", str(exc))
      return
    self._append_log("正在测试学校大模型连通性…")

    async def _run():
      if not RuntimeConfigStore.use_openai_llm():
        raise RuntimeError("请先填写并保存 llmBaseUrl")
      client = RuntimeConfigStore.create_openai_client()
      return await client.health_check()

    worker = _AsyncWorker(_run)
    worker.finished_ok.connect(self._on_llm_test_ok)
    worker.finished_err.connect(lambda e: self._append_log(f"测试失败: {e}"))
    worker.start()
    self._worker = worker  # keep reference

  def _on_llm_test_ok(self, ok: bool) -> None:
    if ok:
      self._append_log("学校大模型：可用")
      QMessageBox.information(self, "测试成功", "学校大模型连通正常")
    else:
      self._append_log("学校大模型：不可达（请确认在 172.16.59.x 内网）")
      QMessageBox.warning(self, "测试失败", "学校大模型不可达")

  def test_backend_chat(self) -> None:
    try:
      RuntimeConfigStore.save(self._collect_updates())
      self.llm_api_key.clear()
    except Exception as exc:  # noqa: BLE001
      QMessageBox.critical(self, "保存失败", str(exc))
      return
    port = int(self.backend_port.value())
    url = f"http://127.0.0.1:{port}/api/chat"
    payload = {
      "message": "连接测试",
      "user_id": "qt_admin",
      "memory_profile": 0,
    }
    self._append_log(f"POST {url} …")
    try:
      resp = requests.post(url, json=payload, timeout=60)
      if resp.status_code != 200:
        self._append_log(f"对话测试失败 HTTP {resp.status_code}: {resp.text[:200]}")
        QMessageBox.warning(self, "测试失败", resp.text[:300])
        return
      data = resp.json()
      preview = (data.get("response") or "")[:120]
      self._append_log(f"对话测试成功，回复预览: {preview}")
      QMessageBox.information(self, "测试成功", preview or "(空回复)")
    except requests.RequestException as exc:
      self._append_log(f"对话测试失败: {exc}")
      QMessageBox.warning(self, "测试失败", str(exc))


def run() -> int:
  app = QApplication(sys.argv)
  window = LiteAdminWindow()
  window.show()
  return app.exec()


if __name__ == "__main__":
  raise SystemExit(run())
