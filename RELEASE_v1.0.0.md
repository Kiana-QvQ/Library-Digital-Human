# v1.0.0 — 首个可运行版本

**发布日期：** 2026-06-24

图书馆数字人 Windows 独立运行包 + 后端源码同仓库。Unity 经本地后端转发至学校内网大模型，支持语音/文字对话、百度 ASR/TTS。

## 下载

| 文件 | 说明 |
|------|------|
| `Library-Digital-Human-v1.0.0-windows.zip` | Windows x64 独立运行包（约 270 MB） |

> 在 GitHub 仓库页点击 **Releases**（不是 Packages）下载。

## 运行 Unity 客户端（Windows）

1. 解压 zip 到任意目录（路径尽量不含中文空格）
2. 双击 **`My project.exe`** 启动
3. 流程：`SceneAihasto` 开场 → `SceneMenu` 主菜单 → 进入对话场景

## 运行后端（必须，对话依赖）

Unity 客户端**不内置**大模型，需在本机或局域网另起后端：

```bash
cd digital_human_backend
cp .env.example .env
cp app_config.example.json data/app_config.json
pip install -r requirements.txtpython环境版本有要求吗
python -m app.main
```

配置学校 API（`.env` 或 Qt 管理台 `python run_qt_admin.py`）。

## 系统要求

- Windows 10/11 x64
- 后端：Python 3.10+
- 学校大模型：运行后端机器需在 `172.16.59.x` 内网
- 百度 ASR/TTS：需在 Unity 构建前已配置 Key（已打入包内 StreamingAssets/Inspector 配置）

## 本版本包含

- Unity 四场景完整流程（Aihasto → Loading → Menu → Chat）
- FastAPI 后端纯转发 `/api/chat`
- Qt 减配管理台（改 API 无需重打包 Unity）
- Unity 启动自动拉取 `backendChatUrl`

## 已知说明

- 首次启动后端请复制 `app_config.example.json` → `data/app_config.json`
- 修改学校 API：用 Qt 管理台保存即可，Unity 重进场景生效
- 源码开发见仓库根目录 `README.md`
