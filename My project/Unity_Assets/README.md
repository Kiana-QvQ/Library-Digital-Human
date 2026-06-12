# Unity-Ollama 聊天系统

这是一个Unity客户端与Python服务器通信的聊天系统，使用Ollama本地大模型进行对话。

## 系统架构

```
Unity客户端 (C#) ←→ Python服务器 ←→ Ollama本地大模型
```

## 文件结构

```
Unity_Assets/
├── Scripts/
│   ├── OllamaChatClient.cs      # 聊天客户端核心脚本
│   └── ChatUIManager.cs         # UI管理器脚本
└── README.md                    # 说明文档

digital_human_backend/
├── server.py                    # Python HTTP服务器
├── test_server.py              # 服务器测试脚本
└── app/brain/llm/
    └── ollama_chat.py          # Ollama聊天功能
```

## 使用步骤

### 1. 启动Ollama服务

确保Ollama已安装并运行：
```bash
# 启动Ollama服务
ollama serve

# 下载模型（如果还没有）
ollama pull gemma3:1b
```

### 2. 启动Python服务器

```bash
cd digital_human_backend
python server.py
```

服务器将在 `http://localhost:8080` 启动。

### 3. 在Unity中使用

1. 将 `OllamaChatClient.cs` 和 `ChatUIManager.cs` 脚本添加到Unity项目中
2. 创建UI界面，包含以下组件：
   - InputField (消息输入框)
   - Button (发送按钮)
   - Text (回复显示)
   - Text (状态显示)
   - ScrollRect (聊天滚动区域)
3. 将UI组件拖拽到脚本的对应字段中
4. 运行Unity项目

### 4. 测试功能

运行测试脚本验证服务器功能：
```bash
cd digital_human_backend
python test_server.py
```

## API接口

### 健康检查
- **URL**: `GET /health`
- **响应**: 
```json
{
  "status": "healthy",
  "ollama_available": true
}
```

### 聊天接口
- **URL**: `POST /chat`
- **请求体**:
```json
{
  "message": "用户消息",
  "system_prompt": "系统提示词（可选）"
}
```
- **响应**:
```json
{
  "response": "AI回复",
  "user_message": "用户消息"
}
```

## 功能特性

### Unity客户端功能
- ✅ 实时连接状态检测
- ✅ 消息发送和接收
- ✅ 聊天历史显示
- ✅ 错误处理和状态提示
- ✅ 可配置服务器地址
- ✅ 自动滚动到最新消息

### Python服务器功能
- ✅ HTTP RESTful API
- ✅ CORS跨域支持
- ✅ 健康检查接口
- ✅ 异步Ollama通信
- ✅ 错误处理和日志记录
- ✅ 可配置参数

## 配置选项

### 服务器配置
```bash
python server.py --host localhost --port 8080 --ollama-url http://localhost:11434 --model gemma3:1b
```

### Unity客户端配置
在Inspector面板中可以配置：
- 服务器URL
- 请求超时时间
- 调试日志开关

## 故障排除

### 常见问题

1. **服务器连接失败**
   - 检查Python服务器是否启动
   - 确认端口8080未被占用
   - 检查防火墙设置

2. **Ollama服务不可用**
   - 确认Ollama已安装并运行
   - 检查模型是否已下载
   - 验证Ollama服务地址

3. **Unity无法发送消息**
   - 检查网络连接
   - 确认服务器URL正确
   - 查看Unity控制台错误信息

### 调试模式

启用调试日志：
```csharp
// 在Unity Inspector中勾选 "Enable Debug Logs"
```

## 扩展功能

可以在此基础上添加：
- 语音输入/输出
- 多轮对话历史
- 用户认证
- 消息加密
- 文件上传
- 实时语音流

## 技术栈

- **Unity**: 2022.3 LTS 或更高版本
- **Python**: 3.8 或更高版本
- **Ollama**: 最新版本
- **依赖库**: 
  - Unity: UnityWebRequest
  - Python: asyncio, aiohttp, http.server
