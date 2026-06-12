# Coze API集成说明

## 概述

已完成Coze API的统一管理和集成，实现了以下功能：
1. **统一API管理**：通过`CozeAPIManager`统一管理所有Coze API Key和配置（可选）
2. **WebSocket流式TTS**：支持流式语音合成，提升响应速度（默认不使用音色，使用默认音色）
3. **语音克隆**：支持语音克隆功能（可独立使用，但可能因权限问题无法使用）
4. **API设置面板**：扩展了`CozeAPISettingsPanel`，支持API管理

**重要说明**：
- **功能独立性**：WebSocket和语音克隆功能可以独立使用，不依赖API管理器
- **音色默认关闭**：WebSocket中的音色参数默认不使用（`useVoiceId = false`）
- **语音克隆限制**：Coze语音克隆可能因权限问题无法使用，推荐使用Doubao
- **默认优先级**：系统默认优先使用Doubao TTS（支持音色），Coze TTS作为备选（不使用音色）

## 文件结构

### 新增文件

```
Coding/Main/
├── CozeAPIManager.cs          # Coze API统一管理器（单例）
└── Coze集成说明.md            # 本文档

Coding/ASR&TTS/Coze/
├── CozeTextToSpeech.cs        # Coze TTS实现（继承TTS基类）
├── CozeStreamTTS.cs           # 流式语音合成（WebSocket）
├── CozeVoiceAPI.cs            # 音色列表查询
├── CozeVoiceClone.cs          # 语音克隆
└── ...（其他文件）

Coding/Chat/Coze/
└── CozeAPISettingsPanel.cs    # API设置面板（已扩展）
```

### 修改文件

```
Coding/Main/
└── VoiceControlManager.cs     # 已集成Coze TTS支持
```

## 使用步骤

### 1. 在Unity中配置组件

#### 步骤1：创建CozeAPIManager
1. 在场景中创建一个空GameObject，命名为"CozeAPIManager"
2. 添加`CozeAPIManager`组件
3. 在Inspector中配置：
   - **API Key**：输入你的Coze API Key
   - **Current Voice ID**：输入要使用的音色ID（可选，后续可通过API查询）

#### 步骤2：配置CozeStreamTTS
1. 在场景中创建一个GameObject，命名为"CozeStreamTTS"
2. 添加`CozeStreamTTS`组件
3. 在Inspector中配置：
   - **API Key**：会自动从CozeAPIManager同步
   - **Voice ID**：会自动从CozeAPIManager同步
   - **Codec**：选择音频编码（pcm/opus）
   - **Sample Rate**：采样率（默认24000）
   - **Speech Rate**：语速（-50到100）

#### 步骤3：配置CozeTextToSpeech
1. 在"CozeStreamTTS" GameObject上添加`CozeTextToSpeech`组件
2. 在Inspector中配置：
   - **Stream TTS**：自动引用CozeStreamTTS组件
   - **API Manager**：自动引用CozeAPIManager（可选）
   - **Use Voice Id**：是否使用音色（默认false，不使用音色）
   - **Use Stream Synthesis**：是否使用流式合成（推荐开启）
   - **Stream Chunk Size**：流式发送的字符数（默认10）

#### 步骤4：配置VoiceControlManager
1. 找到场景中的`VoiceControlManager`（或`VoiceControlScript`）
2. 在Inspector中配置：
   - **Coze Text To Speech**：拖入`CozeTextToSpeech`组件
   - **Prefer Coze TTS**：勾选此选项，优先使用Coze TTS
   - **Coze API Manager**：拖入`CozeAPIManager`组件
   - **Coze Agent Client**：确保已配置

#### 步骤5：配置CozeVoiceAPI（可选，用于查询音色）
1. 在场景中创建一个GameObject，命名为"CozeVoiceAPI"
2. 添加`CozeVoiceAPI`组件
3. API Key会自动从CozeAPIManager同步

#### 步骤6：配置CozeAPISettingsPanel（可选，用于UI设置）
1. 找到场景中的`CozeAPISettingsPanel`
2. 在Inspector中配置：
   - **Coze Client**：拖入`CozeAgentClient`组件
   - **API Manager**：拖入`CozeAPIManager`组件
   - **Voice API**：拖入`CozeVoiceAPI`组件（如果已创建）

### 2. 关于音色使用

**注意**：当前Coze WebSocket语音合成**不使用音色**（使用默认音色）。原因：
- Coze的语音克隆功能需要特殊权限
- 如需使用音色，请使用Doubao（火山引擎）的语音克隆功能

如果将来需要使用Coze音色，可以：
1. 在`CozeStreamTTS`中设置`Use Voice Id = true`
2. 设置`Voice Id`字段
3. 在`CozeTextToSpeech`中设置`Use Voice Id = true`

### 3. 工作流程

1. **用户输入** → `VoiceControlManager.SendMessageToAI()`
2. **AI回复** → `CozeAgentClient.SendMessageToAgent()`
3. **收到文本** → `VoiceControlManager.OnAgentResponseReceived()`
4. **TTS合成**（按优先级）：
   - 优先级1：`DoubaoTextToSpeech`（如果`preferDoubaoTTS = true`，使用音色）
   - 优先级2：`CozeTextToSpeech`（如果`preferCozeTTS = true`，WebSocket流式，不使用音色）
   - 优先级3：`BaiduTextToSpeech`（后备方案）
5. **播放音频** → `AudioSource.Play()`

## 功能特性

### 1. 统一API管理
- **单例模式**：`CozeAPIManager`使用单例模式，全局唯一
- **自动同步**：API Key自动同步到所有Coze组件
- **音色管理**：统一管理当前使用的音色ID和名称

### 2. 流式语音合成
- **实时响应**：文本流式输入，音频流式输出
- **低延迟**：无需等待完整文本，边生成边播放
- **默认音色**：当前不使用指定音色，使用默认音色（如需音色请使用Doubao）

### 3. 自动降级
- **Coze TTS失败**：自动降级到百度TTS
- **TTS未配置**：显示文本回复，不播放音频

### 4. API设置面板扩展
- **统一管理**：通过`CozeAPIManager`统一管理API Key
- **音色选择**：可以查询和选择音色（需要UI扩展）

## 注意事项

1. **API Key配置**：
   - 优先在`CozeAPIManager`中配置API Key
   - 所有组件会自动同步API Key

2. **音色ID配置**：
   - 可以通过`CozeVoiceAPI`查询音色列表
   - 设置音色后，会自动同步到`CozeStreamTTS`

3. **流式TTS连接**：
   - `CozeStreamTTS`需要WebSocket连接
   - 首次使用时会自动连接
   - 连接失败时会自动降级到百度TTS

4. **性能优化**：
   - 流式合成可以显著降低延迟
   - 建议开启`useStreamSynthesis`

## 扩展建议

### 1. UI音色选择器
可以扩展`CozeAPISettingsPanel`，添加音色选择UI：
- 下拉菜单显示音色列表
- 支持播放预览音频
- 支持筛选和搜索

### 2. 音色收藏
- 保存常用音色到本地配置
- 快速切换音色

### 3. 多角色支持
- 根据对话内容自动选择不同音色
- 支持角色音色映射

## 故障排查

### 问题1：Coze TTS不工作
- 检查`CozeAPIManager`是否已配置API Key
- 检查`CozeStreamTTS`是否已连接（查看日志）
- 检查音色ID是否正确
- 查看是否有错误日志

### 问题2：自动降级到百度TTS
- 检查Coze API Key是否有效
- 检查网络连接
- 查看错误日志了解具体原因

### 问题3：音色查询失败
- 检查`CozeVoiceAPI`组件是否已配置
- 检查API Key是否有`listVoice`权限
- 查看错误日志

## 示例代码

### 完整集成示例
```csharp
// 在VoiceControlManager的Start方法中
void Start()
{
    // 初始化Coze API管理器
    if (cozeAPIManager == null)
    {
        cozeAPIManager = CozeAPIManager.Instance;
    }
    
    // 查询并设置音色
    if (cozeAPIManager != null)
    {
        cozeAPIManager.QueryVoiceList((success, message, voices) => {
            if (success && voices != null && voices.Count > 0)
            {
                // 选择第一个自定义音色
                var customVoice = voices.Find(v => !v.is_system_voice);
                if (customVoice != null)
                {
                    cozeAPIManager.SetVoiceId(customVoice.voice_id, customVoice.name);
                    Debug.Log($"已设置音色: {customVoice.name}");
                }
            }
        });
    }
}
```

## 总结

通过以上集成，现在可以实现：
- ✅ Coze WebSocket流式语音合成（不使用音色，使用默认音色）
- ✅ 统一的API Key管理
- ✅ 流式语音合成，低延迟
- ✅ 自动降级机制，保证稳定性
- ✅ 音色查询功能（保留，但当前不使用）

**如需使用音色**，请使用Doubao（火山引擎）的语音克隆和TTS功能，参考`Doubao集成说明.md`。

所有功能已集成到`VoiceControlManager`中，只需在Unity Inspector中配置组件即可使用。
