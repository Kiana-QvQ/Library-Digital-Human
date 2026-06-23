# Library Digital Human

基于 Unity 的图书馆数字人项目：语音唤醒、百度 ASR/TTS、对话 UI，经本地后端转发至学校内网微调大模型。

## 架构说明

当前推荐链路（**纯消息转发**，Unity 不负责大模型推理）：

```
Unity SceneChat
  → 百度 ASR（语音转文字）
  → digital_human_backend  POST /api/chat
  → 学校 OpenAI 兼容网关  POST /v1/chat/completions
  → 百度 TTS（文字转语音播报）
```

- Unity 只负责：**采集用户输入、展示回复、驱动动画与 TTS**
- 后端只负责：**会话管理、多轮 messages 拼接、转发学校 API**
- 学校侧负责：**模型推理**（Qwen2.5-7B-Instruct + 图书馆 LoRA）

> 当前默认**不启用**本地知识库 RAG、mem0 长期记忆、Ollama 对话；如需本地 Ollama 路线，清空 `.env` 中的 `LLM_BASE_URL` 即可回退。

## 仓库结构

```
Library-Digital-Human/
├── My project/              # Unity 工程（Unity Hub 打开此目录）
├── digital_human_backend/   # FastAPI 后端（/api/chat 转发学校大模型）
├── Embedding/               # 向量化脚本（可选，非主链路必需）
└── digital_human_backend/docs/   # 对接文档
```

## 快速开始

### 1. 后端

**环境要求：** Python 3.10+，运行后端机器需能访问学校内网 `172.16.59.x`。

```bash
cd digital_human_backend
cp .env.example .env
pip install -r requirements.txt
python -m app.main
```

`.env` 核心配置（示例）：

```env
HOST=0.0.0.0
PORT=8173

LLM_BASE_URL=https://172.16.59.220/svc/kaDDxL1y-3/v1
LLM_API_KEY=lib-llm-2026-secret-key
LLM_DEFAULT_MODEL=qwen2.5-7b-lora-library
LLM_VERIFY_SSL=false
LLM_MAX_TOKENS=512

# 纯转发模式留空
DEFAULT_CHAT_KB_ID=
```

| 地址 | 说明 |
|------|------|
| http://127.0.0.1:8173/docs | Swagger API 文档 |
| http://127.0.0.1:8173/health | 健康检查（含 LLM 连通状态） |
| http://127.0.0.1:8173/api/diagnostic | Unity 连接状态栏用的中文诊断 |
| http://127.0.0.1:8173/api/config/app | Unity/Qt 运行时配置（对话地址、模型等） |
| http://127.0.0.1:8173/api/chat | Unity 对话接口 |

**验证后端：**

```bash
curl -X POST "http://127.0.0.1:8173/api/chat" \
  -H "Content-Type: application/json" \
  -d "{\"message\":\"图书馆几点开门？\",\"user_id\":\"unity_user\"}"
```

**验证学校 API（需在内网）：**

```bash
curl -k "https://172.16.59.220/svc/kaDDxL1y-3/"
curl -k -X POST "https://172.16.59.220/svc/kaDDxL1y-3/v1/chat/completions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer lib-llm-2026-secret-key" \
  -d "{\"model\":\"qwen2.5-7b-lora-library\",\"messages\":[{\"role\":\"user\",\"content\":\"测试\"}]}"
```

### 2. Unity

1. 安装 **Unity 2022.3+**
2. Unity Hub → Add → 选择 `My project`
3. 场景流程：`SceneLoading` → `SceneMenu` → `SceneChat`
4. 在 Inspector 配置 **百度 ASR/TTS** API Key（`BaiduSettings` 等组件）
5. 确认 `SceneChat` 中 `VoiceControlManager`：
   - `useBackendForChat = true`
   - `backendChatUrl` 可留默认；**打包后**由后端 `data/app_config.json` 或 Qt 管理台配置，Unity 启动时会自动拉取

运行后 Console 应出现：

```
[API Settings] 后端聊天已启用: useBackendForChat=True, url=http://127.0.0.1:8173/api/chat
[BackendConnectionMonitor] 已应用运行时配置 backendChatUrl=...
```

### 2.1 Qt 减配管理台（推荐，免改 Unity 打包）

Unity 打包后改 API 地址很麻烦，可用 **减配版 Qt 后台** 编辑 `digital_human_backend/data/app_config.json`：

```bash
cd digital_human_backend
python run_qt_admin.py
```

管理台功能（**不含**知识库、Neo4j、mem0 等旧完全体）：

| 功能 | 说明 |
|------|------|
| 学校大模型 | Base URL、API Key、模型名、SSL、max_tokens |
| Unity 后端地址 | `unityBackendHost` + 端口 → 生成 `backendChatUrl` |
| 测试学校模型 | 直连学校网关健康检查 |
| 测试后端对话 | `POST /api/chat` 端到端探测 |

首次使用可复制示例配置：

```bash
cp data/app_config.example.json data/app_config.json
```

保存后：**后端下次对话立即生效**；Unity 重新进入场景时会 `GET /api/config/app` 同步 `backendChatUrl`。

> 也可直接编辑 `data/app_config.json` 或调用 `PUT /api/config/app`（API Key 字段留空表示不修改已保存的 Key）。

### 3. 连接状态与测试（分开处理）

链路分三段，测试时建议分开看：

| 环节 | 怎么确认 |
|------|----------|
| Unity → 后端 | 屏幕左上角**连接状态栏**（自动创建）或 Console |
| 后端 → 学校大模型 | 状态栏显示「后端与学校大模型均可用」或黄色警告 |
| 端到端对话 | 正常语音/文字对话（走场景 UI） |

**连接状态栏 `BackendConnectionMonitor`**

- 挂在 `VoiceControlManager` 同一物体上（启动时自动添加）
- 每 12 秒请求 `GET /api/diagnostic`
- 状态含义：
  - **绿色** `● 已连接`：后端 + 学校模型都通
  - **黄色** `● 后端正常 / 模型不可用`：后端在跑，但内网大模型连不上
  - **红色** `○ 未连接`：后端没启动或地址/端口错误

**静默测试（不污染场景对话区）**

- 勾选 `VoiceControlManager.enableTestMode` 后，测试发送的内容**只写日志**，不显示在场景 `resultText` 对话区，默认也不播 TTS
- 详细记录位置：
  - Unity Console
  - 本地文件：`%AppData%/../LocalLow/<公司名>/<产品名>/操作日志.log`
- `BackendConnectionMonitor` 的「对话探测」同样只写日志，不更新场景 UI

**手动诊断命令：**

```bash
curl http://127.0.0.1:8173/api/diagnostic
```

### 4. SceneChat 减配建议

主链路只需保留：

| 组件 | 作用 |
|------|------|
| `VoiceControlManager` | 语音/文字输入、后端对话、TTS 播报 |
| 百度 ASR / TTS | 语音识别与合成 |
| 角色 Animator | 待机 / 思考 / 回答动画 |

可禁用或忽略（当前不走）：

- `CozeAgentClient` / Coze 流式 TTS
- Memory1/2/3 开关
- 本地知识库 UUID
- GLM 视觉模块

## 场景说明

| 场景 | 说明 |
|------|------|
| SceneLoading | 加载页 |
| SceneMenu | 主菜单 / 设置 |
| SceneChat | 数字人对话（主场景） |

## API 对接要点

学校大模型为 **OpenAI 兼容格式**，由后端统一对接，Unity 无需直连。

| 项目 | 值 |
|------|-----|
| 对话地址 | `https://172.16.59.220/svc/kaDDxL1y-3/v1/chat/completions` |
| 鉴权 | `Authorization: Bearer lib-llm-2026-secret-key` |
| 模型名 | `qwen2.5-7b-lora-library` |
| 回复提取 | `choices[0].message.content` |
| 网络 | 仅 `172.16.59.x` 内网；HTTPS 为自签证书，后端已关闭校验 |
| Token 限制 | 输入 + 输出共约 1024 token |

详细说明见：`digital_human_backend/docs/大模型API请求对接文档.md`

## Unity ↔ 后端请求格式

**请求** `POST /api/chat`：

```json
{
  "message": "图书馆借阅规则是什么？",
  "user_id": "unity_user",
  "session_id": "可选，多轮对话时回传",
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

## 常见问题

| 现象 | 排查 |
|------|------|
| Unity 返回本地占位提示 | 确认 `VoiceControlManager` 未禁用 LLM，`useBackendForChat=true` |
| 后端 503 / 无法连接模型 | 是否在 `172.16.59.x` 内网；学校 API 是否启动 |
| 404 知识库不存在 | `.env` 中 `DEFAULT_CHAT_KB_ID` 留空；Unity 不传 `kb_id` |
| 端口连不上 | Unity `backendChatUrl` 与后端 `PORT` 一致；或用 Qt 改 `unityBackendHost`/端口后重启场景 |
| 打包后改 API 麻烦 | 用 `python run_qt_admin.py` 改 `data/app_config.json`，无需重打包 Unity |
| 状态栏一直黄色 | 机器不在 `172.16.59.x` 内网，或学校模型服务未启动 |
| 测试内容出现在对话区 | 开启 `enableTestMode` 且 `hideChatUiInTestMode=true`（默认） |
| 多轮对话 500 | 缩短单条消息；调小 `.env` 中 `LLM_MAX_HISTORY_TURNS` |

## 大文件与协作

本仓库 **不提交**（见 `.gitignore`）：

- Unity `Library/`、`Temp/`、`Logs/`、`UserSettings/`
- 构建产物 `UnityOutput/`
- 本地 `.env`、向量库数据、日志

```bash
git clone git@github.com:Kiana-QvQ/Library-Digital-Human.git
```

请勿将 API Key、密码写入仓库；使用 `.env` 或 Unity Inspector 本地配置。

## License

暂未指定，使用前请与仓库维护者确认。
