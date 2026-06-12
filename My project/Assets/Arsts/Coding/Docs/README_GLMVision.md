# GLM-4.6V-Flash 视觉模型集成说明

## 概述

本项目已集成 GLM-4.6V-Flash 视觉模型，可以通过摄像头捕获图像并进行视觉分析。

## 组件说明

### 1. GLMVisionSettings.cs
- **功能**：统一管理 GLM API 配置
- **位置**：`Coding/Opencv/GLMVisionSettings.cs`
- **配置项**：
  - `apiKey`: GLM API Key（默认已配置）
  - `apiBaseUrl`: API 基础URL
  - `model`: 模型名称（glm-4.6v-flash）

### 2. GLMVisionClient.cs
- **功能**：调用 GLM-4.6V-Flash API 进行视觉分析
- **位置**：`Coding/Opencv/GLMVisionClient.cs`
- **主要方法**：
  - `AnalyzeCurrentFrame(string customPrompt)`: 分析当前摄像头帧
  - `AnalyzeImage(Mat image, string customPrompt)`: 分析指定的图像
- **事件**：
  - `OnVisionAnalysisReceived`: 收到视觉分析结果
  - `OnVisionError`: 视觉分析错误

### 3. LookingEyes.cs (已修改)
- **新增方法**：
  - `GetCurrentFrame()`: 获取当前摄像头帧（供 VisionClient 使用）
  - `IsInitialized()`: 检查摄像头是否已初始化

## 使用步骤

### 步骤 1：配置组件

1. 在 Unity 场景中创建一个 GameObject，命名为 `VisionManager`
2. 添加以下组件：
   - `GLMVisionSettings`
   - `GLMVisionClient`
   - `EyesTrackingController`（如果还没有）

### 步骤 2：配置 API

1. 选择 `VisionManager` GameObject
2. 在 Inspector 中配置 `GLMVisionSettings`：
   - `API Key`: 已默认配置为 `0f7c17daae644dafab98e1e1422cd59c.bXwSTYvzitFJ1Ost`
   - `API Base Url`: `https://open.bigmodel.cn/api/paas/v4/chat/completions`
   - `Model`: `glm-4.6v-flash`

### 步骤 3：连接组件

1. 在 `GLMVisionClient` 组件中：
   - 将 `Eyes Tracking Controller` 拖拽到 `Eyes Controller` 字段
   - 或保持为空，组件会自动查找

### 步骤 4：使用示例

#### 方式 1：使用 VisionExample.cs

1. 在场景中添加 `VisionExample` 组件
2. 配置组件引用：
   - `Vision Client`: 拖拽 `GLMVisionClient` 组件
   - `Eyes Controller`: 拖拽 `EyesTrackingController` 组件
3. 运行场景，按 **空格键** 触发视觉分析

#### 方式 2：代码调用

```csharp
// 获取视觉客户端
GLMVisionClient visionClient = FindObjectOfType<GLMVisionClient>();

// 分析当前帧
visionClient.AnalyzeCurrentFrame("请描述这张图片");

// 订阅结果
visionClient.OnVisionAnalysisReceived += (description) => {
    Debug.Log($"视觉分析结果: {description}");
};
```

#### 方式 3：与聊天系统集成

使用 `VisionChatIntegration.cs` 组件：

1. 添加 `VisionChatIntegration` 组件到场景
2. 配置组件引用：
   - `Vision Client`: GLMVisionClient
   - `Chat Client`: CozeAgentClient
   - `Voice Control Manager`: VoiceControlManager（可选）
3. 按 **V 键** 触发视觉分析
4. 视觉分析结果会自动附加到后续的聊天消息中

## API 请求格式

根据 GLM-4.6V-Flash API 规范，请求格式如下：

```json
{
  "model": "glm-4.6v-flash",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "image_url",
          "image_url": {
            "url": "data:image/jpeg;base64,{base64_encoded_image}"
          }
        },
        {
          "type": "text",
          "text": "请详细描述这张图片的内容。"
        }
      ]
    }
  ],
  "thinking": {
    "type": "enabled"
  }
}
```

## 配置选项

### GLMVisionClient 配置

- **Auto Analyze**: 是否启用自动分析（定期分析摄像头画面）
- **Analyze Interval**: 自动分析间隔（秒）
- **Max Image Width/Height**: 最大图像尺寸（自动压缩）
- **Image Quality**: 图像压缩质量（0-100）
- **Enable Thinking**: 是否启用 thinking 模式

### GLMVisionSettings 配置

- **API Key**: GLM API 密钥
- **API Base Url**: API 基础URL
- **Model**: 模型名称

## 与现有系统集成

### 与 APIManager 集成

`APIManager` 已支持 `GLMVisionSettings`：

```csharp
// 获取 GLM 视觉配置
GLMVisionSettings settings = APIManager.Instance.GetGLMVisionSettings();

// 获取视觉客户端
GLMVisionClient client = settings.GetVisionClient();
```

### 与聊天系统集成

视觉分析结果可以作为上下文传递给聊天系统：

```csharp
// 1. 先分析图像
visionClient.AnalyzeCurrentFrame("请描述这张图片");

// 2. 在回调中获取结果并发送到聊天系统
visionClient.OnVisionAnalysisReceived += (description) => {
    string contextMessage = $"用户发送了一张图片，图片内容：{description}。用户说：你好";
    chatClient.SendMessageToAgent(contextMessage, OnResponse);
};
```

## 注意事项

1. **API Key 安全**：API Key 已硬编码在 `GLMVisionSettings.cs` 中，生产环境建议从配置文件或环境变量读取
2. **图像压缩**：大图像会自动压缩以提高传输效率
3. **网络请求**：确保网络连接正常，API 请求可能需要几秒钟
4. **摄像头权限**：确保 Unity 已获得摄像头访问权限

## 故障排除

### 问题：无法获取摄像头帧
- **解决**：检查 `EyesTrackingController` 是否已正确初始化
- **检查**：在 Inspector 中查看 `EyesTrackingController` 的 `Is Initialized` 状态

### 问题：API 请求失败
- **检查**：API Key 是否正确
- **检查**：网络连接是否正常
- **检查**：API URL 是否正确

### 问题：图像分析结果为空
- **检查**：摄像头是否正常工作
- **检查**：图像是否成功编码为 Base64
- **检查**：API 响应是否包含错误信息

## 更新日志

- **2025-12-30**: 初始版本，集成 GLM-4.6V-Flash 视觉模型