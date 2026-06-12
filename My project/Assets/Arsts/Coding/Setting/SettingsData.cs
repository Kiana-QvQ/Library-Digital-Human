using System;
using UnityEngine;

/// <summary>
/// 主设置数据结构
/// </summary>
[System.Serializable]
public class SettingsData
{
    [Header("游戏设置")]
    public GameSettings game = new GameSettings();
    
    [Header("音频设置")]
    public AudioSettings audio = new AudioSettings();
    
    [Header("UI设置")]
    public UISettings ui = new UISettings();
    
    [Header("网络设置")]
    public NetworkSettings network = new NetworkSettings();
    
    [Header("开发者设置")]
    public DeveloperSettings developer = new DeveloperSettings();
    
    [Header("语音交互设置")]
    public VoiceSettings voice = new VoiceSettings();
    
    [Header("AI对话设置")]
    public AISettings ai = new AISettings();
    
    [Header("视觉模型设置")]
    public VisionSettings vision = new VisionSettings();
}

/// <summary>
/// 游戏主设置
/// </summary>
[System.Serializable]
public class GameSettings
{
    [Header("语言设置")]
    public string language = "zh-CN";
    
    [Header("主题模式")]
    public ThemeMode theme = ThemeMode.Light;
    
    [Header("性能设置")]
    public QualityLevel quality = QualityLevel.High;
    
    [Header("自动保存")]
    public bool autoSave = true;
    public float autoSaveInterval = 300f; // 5分钟
    
    [Header("调试模式")]
    public bool debugMode = false;
}

/// <summary>
/// 音频设置
/// </summary>
[System.Serializable]
public class AudioSettings
{
    [Header("音量控制")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    
    [Range(0f, 1f)]
    public float musicVolume = 0.8f;
    
    [Range(0f, 1f)]
    public float sfxVolume = 1f;
    
    [Range(0f, 1f)]
    public float ttsVolume = 1f;
    
    [Range(0f, 1f)]
    public float microphoneVolume = 1f;
    
    [Header("音频设备")]
    public string audioOutputDevice = "";
    public string microphoneDevice = "";
    
    [Header("音频质量")]
    public int sampleRate = 44100;
    public int bufferSize = 1024;
}

/// <summary>
/// UI设置
/// </summary>
[System.Serializable]
public class UISettings
{
    [Header("字体设置")]
    public int fontSize = 14;
    public string fontFamily = "Arial";
    
    [Header("聊天界面")]
    public int maxChatMessages = 100;
    public float messageDisplayDuration = 5f;
    public bool showTimestamp = true;
    
    [Header("动画效果")]
    public bool enableAnimations = true;
    public float animationSpeed = 1f;
    
    [Header("布局设置")]
    public float chatPanelWidth = 400f;
    public float chatPanelHeight = 600f;
}

/// <summary>
/// 网络设置
/// </summary>
[System.Serializable]
public class NetworkSettings
{
    [Header("Coze API配置")]
    public string cozeApiKey = "";
    public string cozeAgentId = "";
    public string cozeBaseUrl = "https://api.coze.cn/v3/chat";
    
    [Header("Doubao API配置")]
    public string doubaoAppId = "";
    public string doubaoToken = "";
    public string doubaoCluster = "";
    public string doubaoResourceId = "";
    
    [Header("超时设置")]
    public float requestTimeout = 30f;
    public int maxRetryAttempts = 3;
    public float retryDelay = 1f;
    
    [Header("代理设置")]
    public bool useProxy = false;
    public string proxyUrl = "";
    public int proxyPort = 8080;
}

/// <summary>
/// 开发者设置
/// </summary>
[System.Serializable]
public class DeveloperSettings
{
    [Header("调试日志")]
    public LogLevel logLevel = LogLevel.Info;
    public bool enableConsoleLog = true;
    public bool enableFileLog = false;
    
    [Header("性能监控")]
    public bool enablePerformanceMonitor = false;
    public bool showFPS = false;
    public bool showMemoryUsage = false;
    
    [Header("实验性功能")]
    public bool enableExperimentalFeatures = false;
    public bool enableBetaMode = false;
}

/// <summary>
/// 语音交互设置
/// </summary>
[System.Serializable]
public class VoiceSettings
{
    [Header("唤醒词设置")]
    public string wakeWord = "小美小美";
    public float wakeWordConfidence = 0.7f;
    public bool enableWakeWord = true;
    
    [Header("语音识别")]
    public string speechLanguage = "zh-CN";
    public float speechTimeout = 5f;
    public bool enableRealTimeRecognition = true;
    
    [Header("TTS设置")]
    public string ttsVoice = "xiaoyan";
    public float ttsSpeed = 1f;
    public float ttsPitch = 1f;
    public bool enableStreamTTS = false;
    public float ttsBufferTime = 0.5f;
    public int minTextLength = 3;
}

/// <summary>
/// AI对话设置
/// </summary>
[System.Serializable]
public class AISettings
{
    [Header("对话配置")]
    public string systemPrompt = "你是一个友好的AI助手";
    public int maxHistoryLength = 15;
    public bool enableStreamResponse = false;
    
    [Header("响应设置")]
    public float responseTimeout = 30f;
    public bool enableAutoResponse = false;
    public float autoResponseDelay = 2f;
    
    [Header("内容过滤")]
    public bool enableContentFilter = true;
    public string[] blockedWords = new string[0];

    [Header("后端集成")]
    [Tooltip("是否通过后端 /api/chat 进行对话（关闭则直接调用前端配置的提供商，如 Coze）")]
    public bool useBackendForChat = false;

    [Tooltip("后端聊天接口地址，例如 http://127.0.0.1:8000/api/chat")]
    public string backendChatUrl = "http://127.0.0.1:8000/api/chat";

    [Tooltip("后端聊天模型选择（用于 digital_human_backend 的 Ollama 路线，例如：Qwen / Chatglm）")]
    public string backendChatModelKey = "Qwen";

    [Tooltip("走后端 /api/chat 时可选的知识库 UUID，与后端知识库 id 一致；空则使用后端默认知识库")]
    public string backendChatKnowledgeBaseId = "e879f25e-1f28-46d4-8629-4ad0a537a6d2";
}

/// <summary>
/// 主题模式枚举
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    Auto
}

/// <summary>
/// 质量等级枚举
/// </summary>
public enum QualityLevel
{
    Low,
    Medium,
    High,
    Ultra
}

/// <summary>
/// 视觉模型设置
/// </summary>
[System.Serializable]
public class VisionSettings
{
    [Header("GLM视觉模型配置")]
    [Tooltip("GLM API Key - 从 https://open.bigmodel.cn 获取")]
    public string glmApiKey = "0f7c17daae644dafab98e1e1422cd59c.bXwSTYvzitFJ1Ost";
    
    [Tooltip("GLM API 基础URL")]
    public string glmApiBaseUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    
    [Tooltip("GLM 模型名称")]
    public string glmModel = "glm-4.6v-flash";
    
    [Header("视觉分析设置")]
    [Tooltip("是否启用自动分析")]
    public bool enableAutoAnalyze = false;
    
    [Tooltip("自动分析间隔（秒）")]
    [Range(1f, 60f)]
    public float analyzeInterval = 5f;
    
    [Tooltip("最大图像宽度（压缩）")]
    [Range(256, 2048)]
    public int maxImageWidth = 1024;
    
    [Tooltip("最大图像高度（压缩）")]
    [Range(256, 2048)]
    public int maxImageHeight = 1024;
    
    [Tooltip("图像压缩质量（0-100）")]
    [Range(0, 100)]
    public int imageQuality = 80;
    
    [Tooltip("是否启用 thinking 模式")]
    public bool enableThinking = true;
    
    [Header("视觉分析提示词")]
    [TextArea(2, 4)]
    public string defaultPrompt = "请详细描述这张图片的内容，包括场景、人物、物体、动作等。";
    
    [Header("与聊天系统集成")]
    [Tooltip("是否在发送消息时自动附加视觉分析")]
    public bool autoAttachVision = false;
    
    [Tooltip("触发视觉分析的按键")]
    public KeyCode visionTriggerKey = KeyCode.V;

    [Header("路由与后端设置")]
    [Tooltip("视觉请求路由模式：直连 GLM 或 通过后端 API")]
    public VisionRouteMode routeMode = VisionRouteMode.BackendApi;

    [Tooltip("后端视觉分析接口地址")]
    public string backendVisionUrl = "http://127.0.0.1:8173/api/vision/analyze";
}

/// <summary>
/// 日志级别枚举
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// 视觉路由模式
/// </summary>
public enum VisionRouteMode
{
    /// <summary>直接调用 GLM 云端视觉 API</summary>
    DirectApi,

    /// <summary>通过本地/自建后端（digital_human_backend）转发</summary>
    BackendApi
}
