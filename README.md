# Library Digital Human

基于 Unity 的图书馆数字人：语音唤醒、百度 ASR/TTS、对话 UI，经本地后端转发至学校内网微调大模型。

## 架构

```
Unity SceneChat
  → 百度 ASR（语音转文字）
  → digital_human_backend  POST /api/chat
  → 学校 OpenAI 兼容网关
  → 百度 TTS（文字转语音）
```

- **Unity**：采集输入、展示回复、动画与 TTS
- **后端**：接收 Unity 对话请求，转发至学校大模型（不托管模型、不用数据库）
- **Qt 管理台**：改学校 API 地址、百度语音 Key、Unity 后端地址，无需重打包 Unity

## 仓库结构

```
Library Digital Human/
├── My project/                 # Unity 工程（Unity Hub 打开此目录）
├── digital_human_backend/      # FastAPI 转发后端 + Qt 减配管理台
│   └── requirements.txt          # Python 依赖（仅转发所需）
├── app_config.example.json     # 运行时配置示例（复制到 data/）
└── digital_human_backend/docs/ # API 对接文档
```

---

## 日常使用（推荐顺序）

每次运行按以下顺序启动：

| 步骤 | 做什么 | 命令 / 操作 |
|------|--------|-------------|
| 1 | 启动后端 | `cd digital_human_backend` → `python -m app.main` |
| 2 | 改 API（可选） | `python run_qt_admin.py` 或双击 `run_qt_admin.bat` |
| 3 | 启动 Unity | Unity Hub 打开 `My project` → Play |

**开发调试**可直接在 Unity 中打开 `SceneChat` 播放；**完整体验**从 Build Settings 第一个场景 `SceneAihasto` 开始。

---

## 首次部署

### 1. 后端环境

**要求：** Python 3.10+；运行后端的机器需能访问学校内网 `172.16.59.x`。

```bash
cd digital_human_backend
cp .env.example .env
pip install -r requirements.txt
```

编辑 `.env`（示例）：

```env
HOST=0.0.0.0
PORT=8173

LLM_BASE_URL=https://172.16.59.220/svc/kaDDxL1y-3/v1
LLM_API_KEY=学校提供的Key
LLM_DEFAULT_MODEL=qwen2.5-7b-lora-library
LLM_VERIFY_SSL=false
LLM_MAX_TOKENS=512
```

复制运行时配置（也可稍后在 Qt 里填写）：

```bash
cp app_config.example.json data/app_config.json
```

（在 `digital_human_backend` 目录下执行。）

启动后端：

```bash
python -m app.main
```

| 地址 | 用途 |
|------|------|
| http://127.0.0.1:8173/docs | API 文档 |
| http://127.0.0.1:8173/api/chat | Unity 对话 |
| http://127.0.0.1:8173/api/diagnostic | 连接诊断 |
| http://127.0.0.1:8173/api/config/app | 运行时配置（Unity 自动拉取） |

### 2. Qt 减配管理台

用于编辑学校大模型地址、API Key、Unity 访问后端的 Host 等，写入 `data/app_config.json`。

```bash
cd digital_human_backend
python run_qt_admin.py
```

Windows 也可双击：

```
digital_human_backend/run_qt_admin.bat
```

| 功能 | 说明 |
|------|------|
| 学校大模型 | Base URL、API Key、模型名、SSL、max_tokens |
| 百度语音 ASR/TTS | API Key、Secret Key（可选；未填时 Unity 用场景内默认值） |
| Unity 后端地址 | `unityBackendHost` + 端口（端口与 `.env` 的 `PORT` 一致，界面只读） |
| 测试学校模型 | 直连学校网关 |
| 测试百度语音 | 向百度 OAuth 验证凭证 |
| 测试后端对话 | 端到端 `POST /api/chat` |

保存后：**后端下次对话立即生效**；Unity 进入场景时会自动 `GET /api/config/app` 同步 `backendChatUrl` 与百度 Key（若已在 Qt 配置；运行中约每 120 秒刷新一次）。

日志：`digital_human_backend/logs/qt_admin.log`

> API Key / Secret 留空表示不修改已保存的值。也可直接编辑 `data/app_config.json`（该文件已在 `.gitignore`，不会提交 GitHub）。

### 百度 ASR / TTS 凭证

语音在 **Unity 客户端**直接调用百度接口；Qt 负责把 Key 写入 `data/app_config.json`，Unity 启动后自动拉取并覆盖场景内 `BaiduSettings`。

**申请步骤（[百度 AI 开放平台](https://ai.baidu.com/)）：**

1. 注册并登录 → **控制台** → **语音技术**
2. 创建应用，勾选 **语音识别** 与 **语音合成**
3. 在应用详情页复制 **API Key** 与 **Secret Key**
4. 在 Qt 管理台「百度语音 ASR / TTS」填写并保存，点击 **测试百度语音** 验证
5. 重启 Unity 或重新进入 `SceneChat` 使配置生效

**优先级：**

| 来源 | 何时生效 |
|------|----------|
| Qt / `app_config.json` 已配置 | Unity 拉取后覆盖场景默认值 |
| Qt 未配置 | 使用 Unity 场景 / Inspector 中 `BaiduSettings` 默认值 |

> 请勿将真实 Key 提交到 GitHub。示例见 `digital_human_backend/app_config.example.json`（空字段占位）。

### 3. Unity

1. Unity Hub → Add → 选择 **`My project`**
2. 版本建议 **Unity 2022.3+**
3. Inspector 配置百度 ASR/TTS（`BaiduSettings`；开发阶段可用，**打包后推荐改 Qt**）
4. `SceneChat` 中 `VoiceControlManager` 保持：
   - `useBackendForChat = true`
   - `backendChatUrl` 可留默认 `http://127.0.0.1:8173/api/chat`（打包后由 Qt / 后端配置覆盖）

---

## Unity 场景流程

Build Settings 中的场景顺序：

```
SceneAihasto → SceneLoading → SceneMenu → SceneChat
```

| 场景 | 说明 |
|------|------|
| **SceneAihasto** | 开场动画，结束后自动进入 SceneLoading |
| **SceneLoading** | 加载页，默认进入 SceneMenu |
| **SceneMenu** | 主菜单、设置（背景/模型、音量等） |
| **SceneChat** | 数字人对话主场景 |

进入 **SceneChat** 后：

- 左上角 **连接状态栏**（`BackendConnectionMonitor` 自动创建）
- 绿色 `● 已连接`：Unity → 后端 → 学校模型均通
- 支持语音唤醒 / 文字输入，回复经后端转发并由百度 TTS 播报

**测试模式**（无麦克风时）：勾选 `VoiceControlManager.enableTestMode`，测试内容默认只写日志、不进场景对话区。

---

## 打包后现场使用

1. Unity **打包一次**（`backendChatUrl` 可留默认本机地址）
2. 现场电脑安装 Python 依赖，配置 `.env` 与 `data/app_config.json`
3. 常驻运行：`python -m app.main`
4. 需要改学校 API 或百度语音 Key 时，打开 **Qt 管理台** 保存即可，**无需重打包 Unity**
5. 启动 Unity 打包程序，进入对话场景

---

## SceneChat 主链路组件

| 组件 | 作用 |
|------|------|
| `VoiceControlManager` | 语音/文字输入、后端对话、TTS |
| 百度 ASR / TTS | 语音识别与合成 |
| `BackendConnectionMonitor` | 连接状态栏、自动拉取运行时配置 |
| 角色 Animator | 待机 / 思考 / 回答动画 |

当前默认不走：Coze 直连、Ollama 本地模型。

**历史遗留代码（已不参与运行）：** `app/brain/memory/`、`Embedding/` 等为早期 RAG/知识库实验，数字人主链路不依赖，后续可删除。

---

## API 对接要点

学校大模型为 OpenAI 兼容格式，由后端统一对接，Unity 不直连。

| 项目 | 值 |
|------|-----|
| 对话地址 | `https://172.16.59.220/svc/kaDDxL1y-3/v1/chat/completions` |
| 鉴权 | `Authorization: Bearer <API Key>` |
| 模型名 | `qwen2.5-7b-lora-library` |
| 网络 | 仅 `172.16.59.x` 内网；HTTPS 自签证书，后端 `LLM_VERIFY_SSL=false` |

详细说明：`digital_human_backend/docs/大模型API请求对接文档.md`

### Unity ↔ 后端

**请求** `POST /api/chat`：

```json
{
  "message": "图书馆借阅规则是什么？",
  "user_id": "unity_user",
  "session_id": "可选",
  "memory_profile": 0
}
```

**响应**：

```json
{
  "response": "……",
  "user_message": "图书馆借阅规则是什么？",
  "session_id": "a1b2c3d4-..."
}
```

---

## 协作与仓库

本仓库不提交（见 `.gitignore`）：Unity `Library/`、`Temp/`、`.env`、`data/app_config.json`（含 Key）、构建产物等。

```bash
git clone git@github.com:Kiana-QvQ/Library-Digital-Human.git
```

请勿将 API Key 写入仓库；使用 `.env`、`data/app_config.json` 或 Unity Inspector 本地配置。

---

## 发布版本（Releases）

可运行程序通过 GitHub **Releases** 下载（页面右侧 **Releases**，不是 Packages）。

| 版本 | 内容 |
|------|------|
| [v1.0.0](https://github.com/Kiana-QvQ/Library-Digital-Human/releases/tag/v1.0.0) | Windows 独立运行包 + 使用说明 |

**使用者：** 下载 `Library-Digital-Human-v1.0.0-windows.zip` → 解压 → 运行 `My project.exe`，并另起 `digital_human_backend`（见上文「日常使用」）。

**维护者打包新版本：**

```powershell
# 1. Unity 构建到 My project/Output
# 2. 打 zip
powershell -ExecutionPolicy Bypass -File scripts/package_release.ps1
# 3. GitHub -> Releases -> Draft a new release -> 上传 dist/*.zip
```

发布说明模板见 `RELEASE_v1.0.0.md`。

## License

暂未指定，使用前请与仓库维护者确认。
