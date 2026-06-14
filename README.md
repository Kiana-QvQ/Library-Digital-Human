# Library Digital Human

基于 Unity 的图书馆数字人项目：语音唤醒、ASR/TTS、对话 UI、后端知识库与 RAG。

## 仓库结构

```
Library-Digital-Human/
├── My project/              # Unity 工程（用 Unity Hub 打开此目录）
├── digital_human_backend/   # FastAPI 后端（聊天、知识库、RAG）
├── Embedding/               # 向量化相关脚本
└── docs/                    # 后端文档（见 digital_human_backend/docs/）
```

## 快速开始

### Unity

1. 安装 **Unity 2022.3+**
2. Unity Hub → Add → 选择 `My project`
3. 打开场景：`Assets/Scenes/SceneLoading.unity` → `SceneMenu` → `SceneChat`
4. 配置百度 ASR/TTS API Key（`BaiduSettings` 组件）

### 后端（可选）

```bash
cd digital_human_backend
cp .env.example .env   # 填入学校/网关 LLM 配置
pip install -r requirements.txt
python -m app.main
```

API 文档：http://127.0.0.1:8000/docs  
大模型对接说明：`digital_human_backend/docs/大模型API请求对接文档.md`

## 构建场景

| 场景 | 说明 |
|------|------|
| SceneLoading | 加载页 |
| SceneMenu | 主菜单 / 设置 |
| SceneChat | 数字人对话 |

## 大文件说明

本仓库 **不提交** 以下内容（见 `.gitignore`）：

- Unity `Library/`、`Temp/`、`Logs/`、`UserSettings/`
- 构建产物 `UnityOutput/`
- `*.unitypackage`、构建产物等大体积二进制
- 本地 `.env`、向量库数据、日志

克隆后直接在 Unity Hub 打开 `My project` 即可开发，无需额外安装 OpenCV 插件。

## 协作

```bash
git clone git@github.com:Kiana-QvQ/Library-Digital-Human.git
```

请勿将 API Key、密码写入仓库；使用 `.env` 或 Unity Inspector 本地配置。

## License

暂未指定，使用前请与仓库维护者确认。
