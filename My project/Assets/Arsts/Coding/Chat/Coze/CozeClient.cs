using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;

public class CozeAgentClient : MonoBehaviour
{
    [Tooltip("Coze API Key (Personal Access Token). 获取方式: https://www.coze.cn -> 开发者中心 -> API管理")]
    [SerializeField] private string apiKey = "pat_0KO37ZuDD8fV2pZK0en502QSPawHKN6kIjtpd0tpFJrfSfoXgT8TtUq44AwdooDm"; // 请在Unity Inspector中配置，或从环境变量/配置文件读取
    [SerializeField] private string agentId = "7510160526350614562";
    [SerializeField] private string apiBaseUrl = "https://api.coze.cn/v3/chat";
    [SerializeField] private string userId = "123";
    [SerializeField] private float pollInterval = 1f; // 轮询间隔时间
    [SerializeField] private int maxPollAttempts = 10; // 最大轮询次数
    
    [Header("响应模式设置")]
    [SerializeField] private bool useStreamResponse = false; // 是否使用流式响应
    [SerializeField] private bool enableStreamDebug = true; // 是否启用流式调试日志
    
    [Header("语音合成设置")]
    [Tooltip("TTS 输出的 AudioSource。若指定（建议拖入人物模型上的 AudioSource），TTS 会从此处播放，便于唇形同步；未指定则使用本物体上的 AudioSource")]
    public AudioSource outputAudioSource;
    [SerializeField] private TTS ttsComponent; // TTS语音合成组件
    [SerializeField] private bool enableStreamTTS = false; // 是否启用流式语音合成
    [SerializeField] private float ttsBufferTime = 0.5f; // TTS缓冲时间（秒）
    [SerializeField] private int minTextLength = 3; // 最小文本长度才进行TTS合成

    // 对话的修改后的正确端点
    [SerializeField] private string retrieveEndpoint = "https://api.coze.cn/v3/chat/retrieve";
    [SerializeField] private string messageListEndpoint = "https://api.coze.cn/v3/chat/message/list";

    // 存储对话历史
    private List<Message> conversationHistory = new List<Message>();
    private string currentChatId;
    private string currentConversationId;
    private bool isStreamResponse = false; // 标识是否为流式响应
    
    // 流式响应累积内容（用于处理流式事件）
    private string accumulatedStreamResponseContent = ""; // 累积的流式响应内容
    
    // 流式语音合成相关变量
    private string accumulatedStreamText = ""; // 累积的流式文本
    private Coroutine ttsBufferCoroutine; // TTS缓冲协程
    private Queue<AudioClip> audioClipQueue = new Queue<AudioClip>(); // 音频队列
    private bool isPlayingAudio = false; // 是否正在播放音频

    [Header("后端 Coze 配置刷新")]
    [Tooltip("后端 FastAPI 基础地址，用于从 /api/config/coze 拉取最新 Coze 配置（与 QT 配置页保存的地址一致）")]
    [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8000";
    [Tooltip("勾选后，场景加载时会从后端拉取最新 Coze 配置（仅当后端返回有效 apiKey 时才覆盖，否则保留当前配置）")]
    [SerializeField] private bool refreshFromBackendOnStart = true;

    private bool _isRefreshingConfig;

    private void Awake()
    {
        // LLM 链路已断开：不再从后端拉取 Coze 配置
        refreshFromBackendOnStart = false;
    }

    private string GetSanitizedApiKey()
    {
        return string.IsNullOrEmpty(apiKey) ? string.Empty : apiKey.Trim();
    }

    private void Start()
    {
        // 先从 PlayerPrefs 恢复上次有效配置，避免启动瞬间无配置
        string savedApiKey = PlayerPrefs.GetString("CozeApi.ApiKey", "");
        if (!string.IsNullOrEmpty(savedApiKey))
            apiKey = savedApiKey;

        string savedAgentId = PlayerPrefs.GetString("CozeApi.AgentId", "");
        if (!string.IsNullOrEmpty(savedAgentId))
            agentId = savedAgentId;

        string savedBaseUrl = PlayerPrefs.GetString("CozeApi.BaseUrl", "");
        if (!string.IsNullOrEmpty(savedBaseUrl))
            apiBaseUrl = savedBaseUrl;

        string masked = string.IsNullOrEmpty(apiKey)
            ? "(empty)"
            : (apiKey.Length > 8
                ? apiKey.Substring(0, 4) + "****" + apiKey.Substring(apiKey.Length - 4)
                : "****");

        Debug.Log($"[CozeAgentClient] Initialized: apiKey='{masked}', agentId='{agentId}', baseUrl='{apiBaseUrl}'", this);

        // 启动时从后端刷新：仅在后端返回有效配置时覆盖，失败或空数据时保留当前配置
        if (refreshFromBackendOnStart && !string.IsNullOrEmpty(backendBaseUrl))
            RefreshConfigFromBackend();
    }

    /// <summary>
    /// 从后端 /api/config/coze 刷新 Coze 配置。仅当后端返回有效 cozeApiKey 时才覆盖本地配置；
    /// 请求失败、解析失败或后端返回空/无效数据时不做任何覆盖。可在 Inspector 中挂到按钮或事件上调用。
    /// </summary>
    public void RefreshConfigFromBackend()
    {
        if (_isRefreshingConfig)
        {
            Debug.Log("[CozeAgentClient] 正在刷新配置，忽略重复调用");
            return;
        }
        StartCoroutine(RefreshConfigFromBackendCoroutine());
    }

    [System.Serializable]
    private class BackendCozeConfig
    {
        public string cozeApiKey;
        public string cozeAgentId;
        public string cozeBaseUrl;
        public string version;
        public string expireAt;
        public string lastRotatedAt;
        public int? daysToExpire;
        public string status;
    }

    private IEnumerator RefreshConfigFromBackendCoroutine()
    {
        _isRefreshingConfig = true;

        if (string.IsNullOrEmpty(backendBaseUrl))
        {
            Debug.LogWarning("[CozeAgentClient] backendBaseUrl 未配置，跳过从后端刷新，保留当前配置");
            _isRefreshingConfig = false;
            yield break;
        }

        string url = backendBaseUrl.TrimEnd('/') + "/api/config/coze";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.timeout = 5;

            Debug.Log($"[CozeAgentClient] 请求后端 Coze 配置: {url}");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CozeAgentClient] 从后端拉取配置失败（{www.error}），保留当前配置，不覆盖");
                _isRefreshingConfig = false;
                yield break;
            }

            string json = www.downloadHandler.text;
            BackendCozeConfig cfg = null;
            try
            {
                cfg = JsonConvert.DeserializeObject<BackendCozeConfig>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CozeAgentClient] 解析后端返回失败: {e.Message}，保留当前配置，不覆盖");
                _isRefreshingConfig = false;
                yield break;
            }

            if (cfg == null)
            {
                Debug.LogWarning("[CozeAgentClient] 后端返回的配置为空，保留当前配置，不覆盖");
                _isRefreshingConfig = false;
                yield break;
            }

            if (string.IsNullOrEmpty(cfg.cozeApiKey))
            {
                Debug.LogWarning("[CozeAgentClient] 后端返回的 cozeApiKey 为空，视为无效配置，保留当前配置，不覆盖");
                _isRefreshingConfig = false;
                yield break;
            }

            // 仅在此处覆盖：后端返回了有效的 apiKey
            apiKey = cfg.cozeApiKey;
            if (!string.IsNullOrEmpty(cfg.cozeAgentId))
                agentId = cfg.cozeAgentId;
            if (!string.IsNullOrEmpty(cfg.cozeBaseUrl))
                apiBaseUrl = cfg.cozeBaseUrl;

            PlayerPrefs.SetString("CozeApi.ApiKey", apiKey);
            PlayerPrefs.SetString("CozeApi.AgentId", agentId);
            PlayerPrefs.SetString("CozeApi.BaseUrl", apiBaseUrl);
            PlayerPrefs.Save();

            string maskedAPI = apiKey.Length > 8
                ? apiKey.Substring(0, 4) + "****" + apiKey.Substring(apiKey.Length - 4)
                : apiKey;

            Debug.Log($"[CozeAgentClient] 已从后端刷新并应用 Coze 配置: apiKey={maskedAPI}, agentId={agentId}, baseUrl={apiBaseUrl}, version={cfg.version}");

            if (cfg.status == "expired")
                Debug.LogWarning("[CozeAgentClient] 后端标记 Coze 配置已过期，请尽快在 QT 配置页轮换新 Key");
            else if (cfg.status == "expiring" && cfg.daysToExpire.HasValue)
                Debug.LogWarning($"[CozeAgentClient] Coze 配置即将过期，剩余天数: {cfg.daysToExpire}");
        }

        _isRefreshingConfig = false;
    }

    // 消息结构
    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
        public string content_type = "text";
    }

    // API 请求结构
    [System.Serializable]
    private class ApiRequest
    {
        public string bot_id;
        public string user_id;
        public bool stream = false; // 默认为非流式
        public bool auto_save_history = true;
        public List<Message> additional_messages;
    }

    // SSE 响应结构（流式）
    [System.Serializable]
    private class SSEChatResponse
    {
        public string id; // chat_id
        public string conversation_id;
        public string bot_id;
        public string status;
        public LastError last_error;
    }

    // 普通响应结构（非流式）
    [System.Serializable]
    private class NormalChatResponse
    {
        public ChatData data;
        public int code;
        public string msg;
        public ErrorDetail detail; // 错误详情
    }

    // 错误详情结构
    [System.Serializable]
    private class ErrorDetail
    {
        public string logid;
    }

    // 对话数据结构
    [System.Serializable]
    private class ChatData
    {
        public string id;
        public string conversation_id;
        public string bot_id;
        public string status;
        public LastError last_error;
    }

    // 错误结构
    [System.Serializable]
    public class LastError
    {
        public int code;
        public string msg;
    }

    // 对话状态响应结构
    [System.Serializable]
    private class ChatResponse
    {
        public ChatData data;
        public int code;
        public string msg;
    }

    // 消息列表响应结构
    [System.Serializable]
    private class MessageListResponse
    {
        public MessageData[] data;
        public int code;
        public string msg;
    }

    // 消息数据结构
    [System.Serializable]
    public class MessageData
    {
        public string id;
        public string conversation_id;
        public string chat_id;
        public string role;
        public string content;
        public string content_type;
        public string type; // "user", "answer"
    }

    // 流式响应事件数据结构
    [System.Serializable]
    public class StreamEventData
    {
        public string id;
        public string conversation_id;
        public string bot_id;
        public string role;
        public string type;
        public string content;
        public string content_type;
        public string chat_id;
        public long completed_at;
        public LastError last_error;
        public string status;
        public Usage usage;
    }

    // 使用情况数据结构
    [System.Serializable]
    public class Usage
    {
        public int token_count;
        public int output_count;
        public int input_count;
    }

    // 流式响应事件类型
    public enum StreamEventType
    {
        ConversationChatCreated,
        ConversationChatInProgress,
        ConversationMessageDelta,
        ConversationMessageCompleted,
        ConversationChatCompleted,
        Done
    }

    // 流式响应回调事件
    public System.Action<string> OnStreamContentReceived; // 接收到流式内容时触发
    public System.Action OnStreamCompleted; // 流式响应完成时触发
    public System.Action<string> OnStreamError; // 流式响应错误时触发

    // 发送消息到 Coze 代理
    public IEnumerator SendMessageToAgent(string userMessage, System.Action<string> onResponseReceived, bool useStream = false)
    {
        // 验证API Key是否已配置
        string sanitizedApiKey = GetSanitizedApiKey();
        if (string.IsNullOrEmpty(sanitizedApiKey))
        {
            string errorMsg = "API Key未配置！请在Unity Inspector中设置apiKey字段，或访问 https://www.coze.cn 获取API Key";
            Debug.LogError(errorMsg);
            onResponseReceived?.Invoke(errorMsg);
            yield break;
        }
        if (!sanitizedApiKey.StartsWith("pat_"))
        {
            Debug.LogWarning("[CozeAgentClient] API Key 格式看起来不是 PAT（应以 pat_ 开头），请检查是否填错 Token 类型");
        }

        // 使用开关控制响应模式
        bool actualUseStream = useStream || useStreamResponse;
        
        // 添加用户消息到对话历史
        conversationHistory.Add(new Message { role = "user", content = userMessage });
        isStreamResponse = actualUseStream; // 记录响应模式

        if (enableStreamDebug)
        {
            Debug.Log($"发送消息模式: {(actualUseStream ? "流式响应" : "普通响应")}");
        }

        // 构建请求
        ApiRequest request = new ApiRequest
        {
            bot_id = agentId,
            user_id = userId,
            stream = actualUseStream, // 流式响应标志
            auto_save_history = true,
            additional_messages = conversationHistory
        };

        string jsonRequest = JsonConvert.SerializeObject(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        // 发送初始请求
        using (UnityWebRequest www = new UnityWebRequest(apiBaseUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {sanitizedApiKey}");

            // 发送请求
            yield return www.SendWebRequest();

            // 处理响应
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"请求失败: {www.error}");
                onResponseReceived?.Invoke($"错误: {www.error}");
                yield break;
            }

            string responseText = www.downloadHandler.text;
            Debug.Log($"原始响应: \n{responseText}"); // 调试用，可以删除

            // 执行解析协议
            IEnumerator parseCoroutine;
            if (actualUseStream)
            {
                parseCoroutine = ParseSSEResponse(responseText, onResponseReceived);
            }
            else
            {
                parseCoroutine = ParseNormalResponse(responseText, onResponseReceived);
            }

            yield return StartCoroutine(parseCoroutine);

            // 开始轮询对话状态（非流式需要）
            if (!actualUseStream)
            {
                yield return StartCoroutine(CheckChatStatus(onResponseReceived));

                // 获取最终消息
                yield return StartCoroutine(GetChatMessages(onResponseReceived));
            }
        }
    }

    // 发送流式消息到 Coze 代理（新增方法）
    public IEnumerator SendStreamMessageToAgent(string userMessage, System.Action<string> onStreamContentReceived, System.Action<string> onStreamCompleted, System.Action<string> onStreamError)
    {
        // 设置流式响应回调
        OnStreamContentReceived = onStreamContentReceived;
        OnStreamCompleted = () => onStreamCompleted?.Invoke("流式响应完成");
        OnStreamError = onStreamError;

        // 调用原有的发送方法，启用流式模式
        yield return StartCoroutine(SendMessageToAgent(userMessage, onStreamCompleted, true));
    }

    // 解析SSE流式响应
    private IEnumerator ParseSSEResponse(string responseText, System.Action<string> onResponseReceived)
    {
        // 首先检查是否是错误响应（JSON格式而非SSE格式）
        if (responseText.Trim().StartsWith("{"))
        {
            try
            {
                NormalChatResponse errorResponse = JsonConvert.DeserializeObject<NormalChatResponse>(responseText);
                if (errorResponse != null && (errorResponse.code != 0 && errorResponse.code != 200))
                {
                    string errorMsg = $"API错误 (Code: {errorResponse.code}): {errorResponse.msg}";
                    if (errorResponse.detail != null && !string.IsNullOrEmpty(errorResponse.detail.logid))
                    {
                        errorMsg += $"\nLogID: {errorResponse.detail.logid}";
                    }
                    Debug.LogError(errorMsg);
                    onResponseReceived?.Invoke(errorMsg);
                    OnStreamError?.Invoke(errorMsg);
                    yield break;
                }
            }
            catch
            {
                // 如果不是JSON错误响应，继续解析SSE
            }
        }

        string[] lines = responseText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        string currentEvent = "";
        string currentData = "";
        accumulatedStreamResponseContent = ""; // 重置累积内容

        // 解析SSE流式数据
        foreach (string line in lines)
        {
            if (line.StartsWith("event:"))
            {
                currentEvent = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:"))
            {
                currentData = line.Substring(5).Trim();
                
                // 处理不同的流式事件
                yield return StartCoroutine(ProcessStreamEvent(currentEvent, currentData, onResponseReceived));
            }
        }

        // 如果流式响应完成，返回累积的内容
        if (!string.IsNullOrEmpty(accumulatedStreamResponseContent))
        {
            onResponseReceived?.Invoke(accumulatedStreamResponseContent);
            OnStreamCompleted?.Invoke();
        }
    }

    // 处理流式事件
    private IEnumerator ProcessStreamEvent(string eventType, string data, System.Action<string> onResponseReceived)
    {
        // 更稳健的 [DONE] 和空数据判断（避免解析异常）
        string trimmedData = data?.Trim() ?? "";
        if (string.IsNullOrEmpty(trimmedData) || trimmedData == "[DONE]" || trimmedData.StartsWith("[DONE]"))
        {
            yield break;
        }

        // 只对 JSON 对象进行反序列化（StreamEventData 对应 {...} 格式）
        if (!trimmedData.StartsWith("{"))
        {
            if (enableStreamDebug)
                Debug.Log($"跳过非 JSON 数据: {trimmedData}");
            yield break;
        }

        try
        {
            StreamEventData eventData = JsonConvert.DeserializeObject<StreamEventData>(data);
            
            switch (eventType)
            {
                case "conversation.chat.created":
                    currentChatId = eventData.id;
                    currentConversationId = eventData.conversation_id;
                    Debug.Log($"对话创建 - ChatID: {currentChatId}, ConversationID: {currentConversationId}");
                    break;

                case "conversation.chat.in_progress":
                    Debug.Log("对话处理中...");
                    break;

                case "conversation.message.delta":
                    // 累积流式内容
                    if (!string.IsNullOrEmpty(eventData.content))
                    {
                        accumulatedStreamResponseContent += eventData.content;
                        OnStreamContentReceived?.Invoke(eventData.content);
                        Debug.Log($"流式内容: {eventData.content}");
                        
                        // 流式语音合成处理
                        if (enableStreamTTS && ttsComponent != null)
                        {
                            ProcessStreamTTS(eventData.content);
                        }
                    }
                    break;

                case "conversation.message.completed":
                    Debug.Log($"消息完成: {eventData.content}");
                    // 将完整的消息添加到对话历史
                    if (!string.IsNullOrEmpty(eventData.content))
                    {
                        conversationHistory.Add(new Message { role = "assistant", content = eventData.content });
                    }
                    break;

                case "conversation.chat.completed":
                    Debug.Log("对话完成");
                    OnStreamCompleted?.Invoke();
                    break;

                default:
                    Debug.Log($"未知事件类型: {eventType}");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析流式事件异常: {e.Message}");
            OnStreamError?.Invoke($"解析流式事件错误: {e.Message}");
        }

        yield return null;
    }

    // 解析普通响应
    private IEnumerator ParseNormalResponse(string responseText, System.Action<string> onResponseReceived = null)
    {
        try
        {
            NormalChatResponse normalResponse = JsonConvert.DeserializeObject<NormalChatResponse>(responseText);
            
            if (normalResponse == null)
            {
                Debug.LogError("普通响应解析失败：响应为null");
                onResponseReceived?.Invoke("响应解析失败：响应格式异常");
                yield break;
            }

            // 检查错误码（通常成功时code为0或200，错误时为非0值）
            if (normalResponse.code != 0 && normalResponse.code != 200)
            {
                string errorMsg = $"API错误 (Code: {normalResponse.code}): {normalResponse.msg}";
                if (normalResponse.detail != null && !string.IsNullOrEmpty(normalResponse.detail.logid))
                {
                    errorMsg += $"\nLogID: {normalResponse.detail.logid}";
                }
                Debug.LogError(errorMsg);
                onResponseReceived?.Invoke(errorMsg);
                yield break;
            }

            // 检查数据是否存在
            if (normalResponse.data == null)
            {
                Debug.LogError("普通响应解析失败：data字段为null");
                onResponseReceived?.Invoke("响应解析失败：缺少data字段");
                yield break;
            }

            currentChatId = normalResponse.data.id;
            currentConversationId = normalResponse.data.conversation_id;

            Debug.Log($"普通响应解析成功 - ChatID: {currentChatId}, ConversationID: {currentConversationId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析普通数据异常: {e.Message}");
            onResponseReceived?.Invoke($"解析响应错误: {e.Message}");
            yield break;
        }

        yield return null;
    }

    // 轮询对话状态
    private IEnumerator CheckChatStatus(System.Action<string> onResponseReceived)
    {
        int pollAttempts = 0;
        bool isCompleted = false;

        while (!isCompleted && pollAttempts < maxPollAttempts)
        {
            pollAttempts++;
            yield return new WaitForSeconds(pollInterval);

            string pollUrl = $"{retrieveEndpoint}?chat_id={currentChatId}&conversation_id={currentConversationId}";
            Debug.Log($"轮询状态 {pollAttempts}: {pollUrl}");

            using (UnityWebRequest www = new UnityWebRequest(pollUrl, "GET"))
            {
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {GetSanitizedApiKey()}");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"状态查询失败: {www.error}");
                    onResponseReceived?.Invoke($"查询错误: {www.error}");
                    yield break;
                }

                string responseText = www.downloadHandler.text;
                Debug.Log($"状态响应: {responseText}");

                ChatResponse response = null;
                try
                {
                    response = JsonConvert.DeserializeObject<ChatResponse>(responseText);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"状态解析异常: {e.Message}");
                    continue;
                }

                if (response == null)
                {
                    Debug.LogError("状态响应格式异常：响应为null");
                    continue;
                }

                // 检查错误码
                if (response.code != 0 && response.code != 200)
                {
                    string errorMsg = $"状态查询错误 (Code: {response.code}): {response.msg}";
                    Debug.LogError(errorMsg);
                    onResponseReceived?.Invoke(errorMsg);
                    yield break;
                }

                if (response.data == null)
                {
                    Debug.LogError("状态响应格式异常：data字段为null");
                    continue;
                }

                // 检查状态
                switch (response.data.status)
                {
                    case "completed":
                        isCompleted = true;
                        Debug.Log("对话处理完成");
                        break;

                    case "error":
                        Debug.LogError($"处理错误: {response.data.last_error?.msg ?? "未知错误"}");
                        onResponseReceived?.Invoke($"处理错误: {response.data.last_error?.msg ?? "未知错误"}");
                        yield break;

                    default:
                        Debug.Log($"处理中，状态: {response.data.status}");
                        break;
                }
            }
        }

        if (!isCompleted)
        {
            Debug.LogError($"轮询超时，{maxPollAttempts}次尝试");
            onResponseReceived?.Invoke("轮询超时，请重试");
        }
    }

    // 获取最终消息
    private IEnumerator GetChatMessages(System.Action<string> onResponseReceived)
    {
        string messageUrl = $"{messageListEndpoint}?chat_id={currentChatId}&conversation_id={currentConversationId}";
        Debug.Log($"获取消息: {messageUrl}");

        using (UnityWebRequest www = new UnityWebRequest(messageUrl, "GET"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {GetSanitizedApiKey()}");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"获取消息失败: {www.error}");
                onResponseReceived?.Invoke($"获取消息错误: {www.error}");
                yield break;
            }

            string responseText = www.downloadHandler.text;
            Debug.Log($"消息响应: {responseText}");

            try
            {
                MessageListResponse response = JsonConvert.DeserializeObject<MessageListResponse>(responseText);
                
                if (response == null)
                {
                    Debug.LogError("消息响应解析失败：响应为null");
                    onResponseReceived?.Invoke("消息响应解析失败");
                    yield break;
                }

                // 检查错误码
                if (response.code != 0 && response.code != 200)
                {
                    string errorMsg = $"获取消息错误 (Code: {response.code}): {response.msg}";
                    Debug.LogError(errorMsg);
                    onResponseReceived?.Invoke(errorMsg);
                    yield break;
                }

                if (response.data == null || response.data.Length == 0)
                {
                    Debug.LogError("未找到消息数据");
                    onResponseReceived?.Invoke("未找到AI回复");
                    yield break;
                }

                // 查找类型为answer的消息（获取最新的，即最后一个匹配的消息）
                string aiResponse = "";
                // 倒序遍历，找到第一个匹配的就是最新的答案
                for (int i = response.data.Length - 1; i >= 0; i--)
                {
                    MessageData message = response.data[i];
                    if (message.type == "answer" && message.role == "assistant")
                    {
                        aiResponse = message.content;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(aiResponse))
                {
                    Debug.LogError("未找到AI回复内容");
                    onResponseReceived?.Invoke("AI未返回有效回复");
                    yield break;
                }

                // 添加到对话历史
                conversationHistory.Add(new Message { role = "assistant", content = aiResponse });
                onResponseReceived?.Invoke(aiResponse);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析消息异常: {e.Message}");
                onResponseReceived?.Invoke($"解析消息错误: {e.Message}");
            }
        }
    }

    // 清空对话历史
    public void ClearConversation()
    {
        conversationHistory.Clear();
        currentChatId = string.Empty;
        currentConversationId = string.Empty;
    }

    // 公共方法：设置流式响应开关
    public void SetStreamResponse(bool enabled)
    {
        useStreamResponse = enabled;
        if (enableStreamDebug)
        {
            Debug.Log($"流式响应开关已{(enabled ? "启用" : "禁用")}");
        }
    }

    // 公共方法：设置调试日志开关
    public void SetStreamDebug(bool enabled)
    {
        enableStreamDebug = enabled;
        Debug.Log($"流式调试日志已{(enabled ? "启用" : "禁用")}");
    }

    // 公共方法：获取当前流式响应状态
    public bool IsStreamResponseEnabled()
    {
        return useStreamResponse;
    }

    // 公共方法：获取当前调试状态
    public bool IsStreamDebugEnabled()
    {
        return enableStreamDebug;
    }

    // 流式语音合成处理方法
    private void ProcessStreamTTS(string newContent)
    {
        accumulatedStreamText += newContent;
        
        // 停止之前的缓冲协程
        if (ttsBufferCoroutine != null)
        {
            StopCoroutine(ttsBufferCoroutine);
        }
        
        // 开始新的缓冲协程
        ttsBufferCoroutine = StartCoroutine(TTSBufferCoroutine());
    }

    // TTS缓冲协程
    private IEnumerator TTSBufferCoroutine()
    {
        yield return new WaitForSeconds(ttsBufferTime);
        
        // 检查文本长度是否足够
        if (accumulatedStreamText.Length >= minTextLength)
        {
            string textToSynthesize = accumulatedStreamText;
            accumulatedStreamText = ""; // 清空累积文本
            
            if (enableStreamDebug)
            {
                Debug.Log($"开始TTS合成: {textToSynthesize}");
            }
            
            // 调用TTS合成
            if (ttsComponent != null)
            {
                ttsComponent.Speak(textToSynthesize, OnTTSCompleted);
            }
        }
    }

    // TTS合成完成回调
    private void OnTTSCompleted(AudioClip audioClip, string text)
    {
        if (audioClip != null)
        {
            audioClipQueue.Enqueue(audioClip);
            
            if (enableStreamDebug)
            {
                Debug.Log($"TTS合成完成，音频长度: {audioClip.length}秒");
            }
            
            // 开始播放音频队列
            if (!isPlayingAudio)
            {
                StartCoroutine(PlayAudioQueue());
            }
        }
    }

    // 播放音频队列
    private IEnumerator PlayAudioQueue()
    {
        isPlayingAudio = true;
        
        while (audioClipQueue.Count > 0)
        {
            AudioClip clip = audioClipQueue.Dequeue();
            
            if (clip != null)
            {
                // 播放音频（优先使用指定的 outputAudioSource，便于唇形同步）
                AudioSource audioSource = outputAudioSource != null ? outputAudioSource : GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                
                audioSource.clip = clip;
                audioSource.Play();
                
                if (enableStreamDebug)
                {
                    Debug.Log($"播放TTS音频，长度: {clip.length}秒");
                }
                
                // 等待音频播放完成
                yield return new WaitForSeconds(clip.length);
            }
        }
        
        isPlayingAudio = false;
    }

    // 公共方法：设置流式TTS开关
    public void SetStreamTTS(bool enabled)
    {
        enableStreamTTS = enabled;
        if (enableStreamDebug)
        {
            Debug.Log($"流式TTS已{(enabled ? "启用" : "禁用")}");
        }
    }

    // 公共方法：设置TTS组件
    public void SetTTSComponent(TTS tts)
    {
        ttsComponent = tts;
        if (enableStreamDebug)
        {
            Debug.Log($"TTS组件已设置: {tts?.GetType().Name}");
        }
    }

    // 公共方法：停止当前TTS播放
    public void StopTTSPlayback()
    {
        if (isPlayingAudio)
        {
            AudioSource audioSource = outputAudioSource != null ? outputAudioSource : GetComponent<AudioSource>();
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
        
        // 清空音频队列
        audioClipQueue.Clear();
        isPlayingAudio = false;
        
        // 停止TTS缓冲
        if (ttsBufferCoroutine != null)
        {
            StopCoroutine(ttsBufferCoroutine);
            ttsBufferCoroutine = null;
        }
        
        accumulatedStreamText = "";
        
        if (enableStreamDebug)
        {
            Debug.Log("TTS播放已停止");
        }
    }

    #region API Key管理方法

    /// <summary>
    /// 获取当前API Key（用于显示，隐藏敏感信息）
    /// </summary>
    public string GetCurrentAPIKey()
    {
        return apiKey;
    }

    /// <summary>
    /// 测试API Key是否有效
    /// </summary>
    /// <param name="testAPIKey">要测试的API Key</param>
    /// <param name="onTestComplete">测试完成回调 (success, errorMessage)</param>
    public IEnumerator TestAPIKey(string testAPIKey, System.Action<bool, string> onTestComplete)
    {
        if (string.IsNullOrEmpty(testAPIKey))
        {
            onTestComplete?.Invoke(false, "API Key为空");
            yield break;
        }

        // 保存原始API Key（用于测试失败时恢复）
        string originalAPI = apiKey;
        
        // 临时使用测试API Key进行测试
        // 注意：测试完成后，如果测试成功，UpdateAPIKey会更新apiKey
        // 如果测试失败，我们需要恢复原始API Key
        apiKey = testAPIKey;

        // 构建一个简单的测试请求
        // 注意：当auto_save_history=false时，stream字段是必需的
        // 为了简化测试，我们设置auto_save_history=true，但使用简单的测试消息
        ApiRequest testRequest = new ApiRequest
        {
            bot_id = agentId,
            user_id = userId,
            stream = false, // 使用非流式响应，更快
            auto_save_history = true, // 设置为true以避免stream字段要求
            additional_messages = new List<Message> { new Message { role = "user", content = "test" } }
        };

        string jsonRequest = JsonConvert.SerializeObject(testRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        // 发送测试请求
        using (UnityWebRequest www = new UnityWebRequest(apiBaseUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {testAPIKey}");

            // 设置超时时间（5秒）
            www.timeout = 5;

            yield return www.SendWebRequest();

            // 检查请求结果
            if (www.result != UnityWebRequest.Result.Success)
            {
                // 测试失败，恢复原始API Key
                apiKey = originalAPI;
                
                string errorMsg = www.error;
                // 尝试解析错误响应
                if (!string.IsNullOrEmpty(www.downloadHandler?.text))
                {
                    try
                    {
                        NormalChatResponse errorResponse = JsonConvert.DeserializeObject<NormalChatResponse>(www.downloadHandler.text);
                        if (errorResponse != null && errorResponse.code != 0 && errorResponse.code != 200)
                        {
                            errorMsg = $"API错误 (Code: {errorResponse.code}): {errorResponse.msg}";
                        }
                    }
                    catch
                    {
                        // 解析失败，使用原始错误
                    }
                }
                
                onTestComplete?.Invoke(false, errorMsg);
                yield break;
            }

            string responseText = www.downloadHandler.text;
            
            // 检查响应是否包含错误
            if (responseText.Contains("\"code\"") && !responseText.Contains("\"code\":0") && !responseText.Contains("\"code\":200"))
            {
                try
                {
                    NormalChatResponse errorResponse = JsonConvert.DeserializeObject<NormalChatResponse>(responseText);
                    if (errorResponse != null && errorResponse.code != 0 && errorResponse.code != 200)
                    {
                        // 测试失败，恢复原始API Key
                        apiKey = originalAPI;
                        
                        string errorMsg = $"API错误 (Code: {errorResponse.code}): {errorResponse.msg}";
                        onTestComplete?.Invoke(false, errorMsg);
                        yield break;
                    }
                }
                catch
                {
                    // 解析失败，继续检查
                }
            }

            // 测试成功，保持使用测试API Key（UpdateAPIKey会更新它）
            // 注意：不要在这里恢复原始API Key，因为UpdateAPIKey会更新apiKey
            onTestComplete?.Invoke(true, "API Key有效");
        }
    }

    /// <summary>
    /// 更新API Key（仅在测试成功后调用）
    /// </summary>
    /// <param name="newAPIKey">新的API Key</param>
    public void UpdateAPIKey(string newAPIKey)
    {
        if (string.IsNullOrEmpty(newAPIKey))
        {
            Debug.LogError("无法更新：API Key为空");
            return;
        }

        // 去除首尾空白字符
        newAPIKey = newAPIKey.Trim();
        
        apiKey = newAPIKey;
        
        // 显示部分API Key用于确认（前4位和后4位）
        string maskedAPI = newAPIKey.Length > 8 
            ? newAPIKey.Substring(0, 4) + "****" + newAPIKey.Substring(newAPIKey.Length - 4)
            : newAPIKey;
        
        Debug.Log($"API Key已更新为: {maskedAPI}");
        
        // 可以在这里添加保存到PlayerPrefs或配置文件的逻辑
        // PlayerPrefs.SetString("CozeAPIKey", newAPIKey);
        // PlayerPrefs.Save();
    }

    #endregion
}