# DoubaoStreamTTSV3 使用说明

## 概述

`DoubaoStreamTTSV3` 是 Doubao（火山引擎）WebSocket V3 版本的双向流式语音合成实现，支持声音复刻/混音mix功能。

## 主要特性

1. **V3协议支持**：使用最新的双向流式WebSocket V3协议
2. **声音复刻支持**：支持使用克隆后的speaker_id进行语音合成
3. **混音支持**：支持多音色混音（mix_speaker）
4. **连接复用**：支持连接复用，一个连接可以处理多个Session
5. **流式合成**：支持流式文本输入和音频输出

## API配置

### 必需参数

- **AppID**：火山引擎 AppID
- **Token**：火山引擎 Access Token
- **Resource-Id**：资源ID，可选值：
  - `seed-tts-1.0`：豆包语音合成模型1.0（字符版）
  - `seed-tts-1.0-concurr`：豆包语音合成模型1.0（并发版）
  - `seed-tts-2.0`：豆包语音合成模型2.0（字符版）
  - `seed-icl-1.0`：声音复刻1.0（字符版）
  - `seed-icl-1.0-concurr`：声音复刻1.0（并发版）
  - `seed-icl-2.0`：声音复刻2.0（字符版）

### 语音合成参数

- **Speaker ID**：说话人ID（使用克隆后的speaker_id或系统音色）
- **Audio Format**：音频编码格式（mp3/ogg_opus/pcm/wav）
- **Sample Rate**：音频采样率（8000-48000）
- **Speech Rate**：语速 [-50, 100]
- **Loudness Rate**：音量 [-50, 100]
- **Emotion**：情感类型（仅部分音色支持）
- **Emotion Scale**：情感值 [1-5]

## 使用流程

### 1. 基本使用

```csharp
DoubaoStreamTTSV3 streamTTS = GetComponent<DoubaoStreamTTSV3>();

// 设置凭证
streamTTS.SetCredentials(appId, token, "seed-tts-1.0");

// 设置说话人ID（如果使用语音克隆）
streamTTS.SetSpeakerId("S_MyVoice");

// 连接WebSocket
streamTTS.Connect();

// 等待连接成功后，开始Session并合成文本
streamTTS.StartSession();
streamTTS.SendText("你好，这是测试文本");
streamTTS.FinishSession();

// 或者使用便捷方法（自动处理Session）
streamTTS.SynthesizeText("你好，这是测试文本");
```

### 2. 事件回调

```csharp
streamTTS.OnConnected += () => {
    Debug.Log("连接成功");
};

streamTTS.OnAudioReceived += (audioClip) => {
    // 播放音频
    audioSource.clip = audioClip;
    audioSource.Play();
};

streamTTS.OnSynthesisCompleted += () => {
    Debug.Log("合成完成");
};

streamTTS.OnError += (error) => {
    Debug.LogError($"错误: {error}");
};
```

### 3. 连接复用（推荐）

```csharp
// 建立连接
streamTTS.Connect();

// 等待ConnectionStarted事件后，可以多次使用Session
// Session 1
streamTTS.StartSession();
streamTTS.SendText("第一段文本");
streamTTS.FinishSession();

// 等待SessionFinished后，开始下一个Session
// Session 2
streamTTS.StartSession();
streamTTS.SendText("第二段文本");
streamTTS.FinishSession();

// 不再使用时，断开连接
streamTTS.Disconnect();
```

## 与旧版本的区别

### DoubaoStreamTTS（V1版本）

- 使用 `wss://openspeech.bytedance.com/api/v1/tts/ws_binary`
- 使用 `Authorization: Bearer; {token}` header
- 一次性发送完整文本
- 不支持连接复用

### DoubaoStreamTTSV3（V3版本）

- 使用 `wss://openspeech.bytedance.com/api/v3/tts/bidirection`
- 使用 `X-Api-App-Key`, `X-Api-Access-Key`, `X-Api-Resource-Id` headers
- 支持Connection和Session概念
- 支持连接复用
- 支持流式文本输入
- 支持声音复刻和混音

## 注意事项

1. **连接复用**：推荐使用连接复用方式，一个连接可以处理多个Session，提高效率
2. **Session管理**：必须等待SessionFinished事件后才能开始新的Session
3. **音频格式**：推荐使用PCM格式以获得最佳性能，MP3/OGG需要临时文件处理
4. **错误处理**：建议监听OnError事件，处理连接和合成错误
5. **资源ID选择**：根据使用的音色类型选择合适的Resource-Id

## 与DoubaoAPIManager集成

```csharp
DoubaoAPIManager apiManager = DoubaoAPIManager.Instance;

// 启用语音克隆功能（默认关闭）
apiManager.SetVoiceCloneEnabled(true);

// 设置说话人ID（仅在启用语音克隆时生效）
apiManager.SetSpeakerId("S_MyVoice", "我的克隆音色");

// 获取组件并设置凭证
DoubaoStreamTTSV3 streamTTS = GetComponent<DoubaoStreamTTSV3>();
streamTTS.SetCredentials(
    apiManager.GetAppId(), 
    apiManager.GetToken(), 
    "seed-icl-2.0" // 使用复刻2.0资源
);
streamTTS.SetSpeakerId(apiManager.GetSpeakerId());
```

## 故障排查

### 连接失败

- 检查AppID和Token是否正确
- 检查Resource-Id是否匹配音色类型
- 检查网络连接

### Session启动失败

- 确保已收到ConnectionStarted事件
- 检查speaker_id是否正确
- 检查音频参数是否在有效范围内

### 音频播放问题

- PCM格式：检查采样率设置
- MP3/OGG格式：检查临时文件权限
- 确保AudioSource组件已正确配置

## 相关文档

- [Doubao API文档](https://www.volcengine.com/docs/6561/10710)
- [Doubao集成说明.md](../Main/Doubao集成说明.md)
- [DoubaoAPIManager.cs](../Main/DoubaoAPIManager.cs)
