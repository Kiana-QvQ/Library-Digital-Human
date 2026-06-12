using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using UnityEngine.Networking;

/// <summary>
/// Doubao（火山引擎）WebSocket V3 双向流式语音合成
/// 支持声音复刻/混音mix，使用V3版本的API协议
/// </summary>
public class DoubaoStreamTTSV3 : MonoBehaviour
{
    // API配置（由 DoubaoSettings 统一管理，不显示在 Inspector 中）
    private string appId = "";
    private string token = "";
    private string resourceId = "seed-tts-1.0"; // 默认使用TTS 1.0
    
    [Header("语音合成配置")]
    [Tooltip("是否使用音色（如果为false，即使设置了speakerId也不会使用，默认不使用）")]
    [SerializeField] private bool useSpeakerId = false;
    
    [Tooltip("说话人ID（使用克隆后的speaker_id或系统音色，仅在useSpeakerId为true时生效）")]
    [SerializeField] private string speakerId = "";
    
    [Tooltip("音频编码格式")]
    [SerializeField] private AudioFormat format = AudioFormat.mp3;
    
    [Tooltip("音频采样率")]
    [SerializeField] private int sampleRate = 24000;
    
    [Tooltip("语速 [-50, 100]，100代表2.0倍速，-50代表0.5倍速")]
    [SerializeField] private int speechRate = 0;
    
    [Tooltip("音量 [-50, 100]，100代表2.0倍音量，-50代表0.5倍音量")]
    [SerializeField] private int loudnessRate = 0;
    
    [Tooltip("情感类型（仅部分音色支持）")]
    [SerializeField] private string emotion = "";
    
    [Tooltip("情感值 [1-5]，默认4")]
    [SerializeField] private float emotionScale = 4.0f;
    
    [Header("流式会话设置")]
    [Tooltip("是否启用流式文本追加（多次发送 TaskRequest）")]
    [SerializeField] private bool enableStreamAppend = true;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // WebSocket连接
    private ClientWebSocket webSocket;
    private bool isConnected = false;
    private bool isConnecting = false;
    private string connectionId = "";
    private string currentSessionId = "";
    private bool isSessionActive = false;
    
    // 协程
    private Coroutine receiveCoroutine;
    private Coroutine audioPlayCoroutine;
    
    // 音频数据队列
    private Queue<byte[]> audioDataQueue = new Queue<byte[]>();
    private bool isPlayingAudio = false;
    
    // WebSocket地址
    private const string WS_URL = "wss://openspeech.bytedance.com/api/v3/tts/bidirection";
    
    // 回调事件
    public System.Action OnConnected;                    // 连接成功
    public System.Action<string> OnDisconnected;        // 断开连接
    public System.Action<AudioClip> OnAudioReceived;     // 收到音频
    public System.Action<string> OnError;                // 发生错误
    public System.Action OnSynthesisCompleted;           // 合成完成
    
    // 音频格式枚举
    public enum AudioFormat
    {
        mp3,
        ogg_opus,
        pcm,
        wav
    }
    
    // Event定义
    private const int EVENT_START_CONNECTION = 1;
    private const int EVENT_FINISH_CONNECTION = 2;
    private const int EVENT_CONNECTION_STARTED = 50;
    private const int EVENT_CONNECTION_FAILED = 51;
    private const int EVENT_CONNECTION_FINISHED = 52;
    private const int EVENT_START_SESSION = 100;
    private const int EVENT_CANCEL_SESSION = 101;
    private const int EVENT_FINISH_SESSION = 102;
    private const int EVENT_SESSION_STARTED = 150;
    private const int EVENT_SESSION_CANCELED = 151;
    private const int EVENT_SESSION_FINISHED = 152;
    private const int EVENT_SESSION_FAILED = 153;
    private const int EVENT_TASK_REQUEST = 200;
    private const int EVENT_TTS_SENTENCE_START = 350;
    private const int EVENT_TTS_SENTENCE_END = 351;
    private const int EVENT_TTS_RESPONSE = 352;
    
    // Message Type定义
    private const byte MSG_TYPE_FULL_CLIENT_REQUEST = 0x14; // 0001 0100
    private const byte MSG_TYPE_FULL_SERVER_RESPONSE = 0x94; // 1001 0100
    private const byte MSG_TYPE_AUDIO_ONLY_RESPONSE = 0xB4;  // 1011 0100
    private const byte MSG_TYPE_ERROR = 0xF0;                // 1111 0000
    
    /// <summary>
    /// 连接WebSocket
    /// </summary>
    public void Connect()
    {
        if (isConnecting || isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 已连接或正在连接中");
            return;
        }
        
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("DoubaoStreamTTSV3: AppID或Token未配置！");
            OnError?.Invoke("AppID或Token未配置");
            return;
        }
        
        StartCoroutine(ConnectCoroutine());
    }
    
    /// <summary>
    /// 连接协程
    /// </summary>
    private IEnumerator ConnectCoroutine()
    {
        isConnecting = true;
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoStreamTTSV3: 连接WebSocket: {WS_URL}");
        }
        
        webSocket = new ClientWebSocket();
        
        // 设置HTTP请求头（V3版本使用这些header进行鉴权）
        webSocket.Options.SetRequestHeader("X-Api-App-Key", appId);
        webSocket.Options.SetRequestHeader("X-Api-Access-Key", token);
        webSocket.Options.SetRequestHeader("X-Api-Resource-Id", resourceId);
        
        // 生成连接ID
        connectionId = Guid.NewGuid().ToString();
        webSocket.Options.SetRequestHeader("X-Api-Connect-Id", connectionId);
        
        var connectTask = webSocket.ConnectAsync(new Uri(WS_URL), CancellationToken.None);
        
        // 等待连接完成
        while (!connectTask.IsCompleted)
        {
            yield return null;
        }
        
        if (connectTask.IsFaulted)
        {
            string errorMsg = $"WebSocket连接失败: {connectTask.Exception?.GetBaseException()?.Message}";
            Debug.LogError($"DoubaoStreamTTSV3: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        if (webSocket.State != WebSocketState.Open)
        {
            string errorMsg = $"WebSocket连接失败，状态: {webSocket.State}";
            Debug.LogError($"DoubaoStreamTTSV3: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        isConnected = true;
        isConnecting = false;
        
        if (enableDebug)
        {
            Debug.Log("DoubaoStreamTTSV3: WebSocket连接成功");
        }
        
        // 开始接收消息
        receiveCoroutine = StartCoroutine(ReceiveMessages());
        
        // 发送StartConnection事件
        SendStartConnection();
    }
    
    /// <summary>
    /// 发送StartConnection事件
    /// </summary>
    private void SendStartConnection()
    {
        if (!isConnected)
        {
            return;
        }
        
        // 构建StartConnection包
        // Header: v1, 4-byte header, Full-client request with event, JSON, no compression
        byte[] header = new byte[4];
        header[0] = 0x11; // v1, 4-byte header
        header[1] = MSG_TYPE_FULL_CLIENT_REQUEST; // Full-client request with event
        header[2] = 0x10; // JSON, no compression
        header[3] = 0x00; // Reserved
        
        // Event number (int32, big-endian)
        byte[] eventBytes = BitConverter.GetBytes(EVENT_START_CONNECTION);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(eventBytes);
        }
        
        // Payload JSON (空JSON对象)
        string payloadJson = "{}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        byte[] payloadLengthBytes = BitConverter.GetBytes((uint)payloadBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(payloadLengthBytes);
        }
        
        // 构建完整消息
        byte[] message = new byte[header.Length + eventBytes.Length + payloadLengthBytes.Length + payloadBytes.Length];
        int offset = 0;
        Array.Copy(header, 0, message, offset, header.Length);
        offset += header.Length;
        Array.Copy(eventBytes, 0, message, offset, eventBytes.Length);
        offset += eventBytes.Length;
        Array.Copy(payloadLengthBytes, 0, message, offset, payloadLengthBytes.Length);
        offset += payloadLengthBytes.Length;
        Array.Copy(payloadBytes, 0, message, offset, payloadBytes.Length);
        
        SendBinaryMessage(message);
    }
    
    /// <summary>
    /// 开始新的Session
    /// </summary>
    public void StartSession()
    {
        if (!isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 未连接，无法开始Session");
            return;
        }
        
        if (isSessionActive)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: Session已激活，请先完成当前Session");
            return;
        }
        
        currentSessionId = Guid.NewGuid().ToString();
        StartCoroutine(SendStartSessionCoroutine());
    }
    
    /// <summary>
    /// 发送StartSession协程
    /// </summary>
    private IEnumerator SendStartSessionCoroutine()
    {
        // 构建StartSession请求
        TTSV3SessionRequest sessionRequest = new TTSV3SessionRequest
        {
            user = new TTSV3User
            {
                uid = SystemInfo.deviceUniqueIdentifier
            },
            req_params = new TTSV3ReqParams
            {
                speaker = (useSpeakerId && !string.IsNullOrEmpty(speakerId)) ? speakerId : "",
                audio_params = new TTSV3AudioParams
                {
                    format = format.ToString(),
                    sample_rate = sampleRate,
                    speech_rate = speechRate,
                    loudness_rate = loudnessRate
                }
            }
        };
        
        // 添加情感参数（如果设置）
        if (!string.IsNullOrEmpty(emotion))
        {
            sessionRequest.req_params.audio_params.emotion = emotion;
            sessionRequest.req_params.audio_params.emotion_scale = emotionScale;
        }
        
        string sessionJson = JsonConvert.SerializeObject(sessionRequest);
        byte[] sessionBytes = Encoding.UTF8.GetBytes(sessionJson);
        
        // 构建StartSession包
        byte[] header = new byte[4];
        header[0] = 0x11;
        header[1] = MSG_TYPE_FULL_CLIENT_REQUEST;
        header[2] = 0x10; // JSON, no compression
        header[3] = 0x00;
        
        byte[] eventBytes = BitConverter.GetBytes(EVENT_START_SESSION);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(eventBytes);
        }
        
        byte[] sessionIdBytes = Encoding.UTF8.GetBytes(currentSessionId);
        byte[] sessionIdLengthBytes = BitConverter.GetBytes((uint)sessionIdBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(sessionIdLengthBytes);
        }
        
        byte[] payloadLengthBytes = BitConverter.GetBytes((uint)sessionBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(payloadLengthBytes);
        }
        
        // 构建完整消息
        byte[] message = new byte[header.Length + eventBytes.Length + sessionIdLengthBytes.Length + 
                                  sessionIdBytes.Length + payloadLengthBytes.Length + sessionBytes.Length];
        int offset = 0;
        Array.Copy(header, 0, message, offset, header.Length);
        offset += header.Length;
        Array.Copy(eventBytes, 0, message, offset, eventBytes.Length);
        offset += eventBytes.Length;
        Array.Copy(sessionIdLengthBytes, 0, message, offset, sessionIdLengthBytes.Length);
        offset += sessionIdLengthBytes.Length;
        Array.Copy(sessionIdBytes, 0, message, offset, sessionIdBytes.Length);
        offset += sessionIdBytes.Length;
        Array.Copy(payloadLengthBytes, 0, message, offset, payloadLengthBytes.Length);
        offset += payloadLengthBytes.Length;
        Array.Copy(sessionBytes, 0, message, offset, sessionBytes.Length);
        
        SendBinaryMessage(message);
        yield return null;
    }
    
    /// <summary>
    /// 发送文本进行合成（TaskRequest）
    /// </summary>
    public void SendText(string text)
    {
        if (!isConnected || !isSessionActive)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 未连接或Session未激活");
            return;
        }
        
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        
        StartCoroutine(SendTaskRequestCoroutine(text));
    }
    
    /// <summary>
    /// 发送TaskRequest协程
    /// </summary>
    private IEnumerator SendTaskRequestCoroutine(string text)
    {
        // 构建TaskRequest（文本数据）
        TTSV3TaskRequest taskRequest = new TTSV3TaskRequest
        {
            req_params = new TTSV3ReqParams
            {
                text = text
            }
        };
        
        string taskJson = JsonConvert.SerializeObject(taskRequest);
        byte[] taskBytes = Encoding.UTF8.GetBytes(taskJson);
        
        // 构建TaskRequest包（文本，含Event）
        byte[] header = new byte[4];
        header[0] = 0x11;
        header[1] = MSG_TYPE_FULL_CLIENT_REQUEST;
        header[2] = 0x10; // JSON, no compression
        header[3] = 0x00;
        
        byte[] eventBytes = BitConverter.GetBytes(EVENT_TASK_REQUEST);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(eventBytes);
        }
        
        byte[] sessionIdBytes = Encoding.UTF8.GetBytes(currentSessionId);
        byte[] sessionIdLengthBytes = BitConverter.GetBytes((uint)sessionIdBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(sessionIdLengthBytes);
        }
        
        byte[] payloadLengthBytes = BitConverter.GetBytes((uint)taskBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(payloadLengthBytes);
        }
        
        // 构建完整消息
        byte[] message = new byte[header.Length + eventBytes.Length + sessionIdLengthBytes.Length + 
                                  sessionIdBytes.Length + payloadLengthBytes.Length + taskBytes.Length];
        int offset = 0;
        Array.Copy(header, 0, message, offset, header.Length);
        offset += header.Length;
        Array.Copy(eventBytes, 0, message, offset, eventBytes.Length);
        offset += eventBytes.Length;
        Array.Copy(sessionIdLengthBytes, 0, message, offset, sessionIdLengthBytes.Length);
        offset += sessionIdLengthBytes.Length;
        Array.Copy(sessionIdBytes, 0, message, offset, sessionIdBytes.Length);
        offset += sessionIdBytes.Length;
        Array.Copy(payloadLengthBytes, 0, message, offset, payloadLengthBytes.Length);
        offset += payloadLengthBytes.Length;
        Array.Copy(taskBytes, 0, message, offset, taskBytes.Length);
        
        SendBinaryMessage(message);
        yield return null;
    }
    
    /// <summary>
    /// 完成Session
    /// </summary>
    public void FinishSession()
    {
        if (!isConnected || !isSessionActive)
        {
            return;
        }
        
        StartCoroutine(SendFinishSessionCoroutine());
    }
    
    /// <summary>
    /// 发送FinishSession协程
    /// </summary>
    private IEnumerator SendFinishSessionCoroutine()
    {
        byte[] header = new byte[4];
        header[0] = 0x11;
        header[1] = MSG_TYPE_FULL_CLIENT_REQUEST;
        header[2] = 0x10;
        header[3] = 0x00;
        
        byte[] eventBytes = BitConverter.GetBytes(EVENT_FINISH_SESSION);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(eventBytes);
        }
        
        byte[] sessionIdBytes = Encoding.UTF8.GetBytes(currentSessionId);
        byte[] sessionIdLengthBytes = BitConverter.GetBytes((uint)sessionIdBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(sessionIdLengthBytes);
        }
        
        string payloadJson = "{}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        byte[] payloadLengthBytes = BitConverter.GetBytes((uint)payloadBytes.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(payloadLengthBytes);
        }
        
        byte[] message = new byte[header.Length + eventBytes.Length + sessionIdLengthBytes.Length + 
                                  sessionIdBytes.Length + payloadLengthBytes.Length + payloadBytes.Length];
        int offset = 0;
        Array.Copy(header, 0, message, offset, header.Length);
        offset += header.Length;
        Array.Copy(eventBytes, 0, message, offset, eventBytes.Length);
        offset += eventBytes.Length;
        Array.Copy(sessionIdLengthBytes, 0, message, offset, sessionIdLengthBytes.Length);
        offset += sessionIdLengthBytes.Length;
        Array.Copy(sessionIdBytes, 0, message, offset, sessionIdBytes.Length);
        offset += sessionIdBytes.Length;
        Array.Copy(payloadLengthBytes, 0, message, offset, payloadLengthBytes.Length);
        offset += payloadLengthBytes.Length;
        Array.Copy(payloadBytes, 0, message, offset, payloadBytes.Length);
        
        SendBinaryMessage(message);
        yield return null;
    }
    
    /// <summary>
    /// 合成完整文本（自动处理Session）
    /// </summary>
    public void SynthesizeText(string text)
    {
        if (!isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 未连接，无法合成文本");
            return;
        }
        
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 文本为空");
            return;
        }
        
        StartCoroutine(SynthesizeTextCoroutine(text));
    }

    /// <summary>
    /// 开始流式会话（复用连接）
    /// </summary>
    public void BeginStream()
    {
        if (!isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 未连接，无法开始流式会话");
            return;
        }

        if (!isSessionActive)
        {
            StartSession();
        }
    }

    /// <summary>
    /// 追加流式文本片段（多次 TaskRequest）
    /// </summary>
    public void AppendStreamText(string delta)
    {
        if (!enableStreamAppend)
        {
            return;
        }

        if (!isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTSV3: 未连接");
            return;
        }

        if (!isSessionActive)
        {
            StartSession();
        }

        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        // 直接发送 TaskRequest 文本片段，不结束 Session
        StartCoroutine(SendTaskRequestCoroutine(delta));
    }

    /// <summary>
    /// 完成流式会话（FinishSession）
    /// </summary>
    public void CompleteStream()
    {
        if (!isSessionActive)
        {
            return;
        }

        FinishSession();
    }
    
    /// <summary>
    /// 合成文本协程
    /// </summary>
    private IEnumerator SynthesizeTextCoroutine(string text)
    {
        // 如果Session未激活，先开始Session
        if (!isSessionActive)
        {
            StartSession();
            // 等待SessionStarted事件
            float timeout = 5f;
            float elapsed = 0f;
            while (!isSessionActive && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (!isSessionActive)
            {
                Debug.LogError("DoubaoStreamTTSV3: Session启动超时");
                OnError?.Invoke("Session启动超时");
                yield break;
            }
        }
        
        // 发送文本
        SendText(text);
        
        // 完成Session
        FinishSession();
    }
    
    /// <summary>
    /// 发送二进制消息
    /// </summary>
    private void SendBinaryMessage(byte[] message)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            return;
        }
        
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(message), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        // 异步发送，不等待完成（实际项目中可能需要处理发送失败的情况）
        if (enableDebug)
        {
            Debug.Log($"DoubaoStreamTTSV3: 发送消息，长度: {message.Length}");
        }
    }
    
    /// <summary>
    /// 接收消息协程
    /// </summary>
    private IEnumerator ReceiveMessages()
    {
        byte[] buffer = new byte[4096 * 4];
        
        while (isConnected && webSocket != null && webSocket.State == WebSocketState.Open)
        {
            var receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            while (!receiveTask.IsCompleted)
            {
                yield return null;
            }
            
            if (receiveTask.IsFaulted)
            {
                Debug.LogError($"DoubaoStreamTTSV3: 接收消息失败: {receiveTask.Exception?.GetBaseException()?.Message}");
                break;
            }
            
            var result = receiveTask.Result;
            
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                ProcessBinaryMessage(buffer, result.Count);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                if (enableDebug)
                {
                    Debug.Log("DoubaoStreamTTSV3: WebSocket连接关闭");
                }
                break;
            }
        }
        
        // 清理连接
        Disconnect();
    }
    
    /// <summary>
    /// 处理二进制消息
    /// </summary>
    private void ProcessBinaryMessage(byte[] data, int length)
    {
        if (length < 4)
        {
            return;
        }
        
        // 解析Header
        byte protocolVersion = (byte)((data[0] >> 4) & 0x0F);
        byte headerSize = (byte)((data[0] & 0x0F) * 4);
        byte messageType = data[1];
        byte serializationMethod = (byte)((data[2] >> 4) & 0x0F);
        byte compressionMethod = (byte)(data[2] & 0x0F);
        
        int offset = 4;
        
        // 检查是否有Event number
        bool hasEvent = (messageType & 0x04) != 0;
        int eventNumber = 0;
        
        if (hasEvent && offset + 4 <= length)
        {
            byte[] eventBytes = new byte[4];
            Array.Copy(data, offset, eventBytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(eventBytes);
            }
            eventNumber = BitConverter.ToInt32(eventBytes, 0);
            offset += 4;
        }
        
        // 处理不同的事件类型
        switch (eventNumber)
        {
            case EVENT_CONNECTION_STARTED:
                HandleConnectionStarted(data, offset, length);
                break;
            case EVENT_CONNECTION_FAILED:
                HandleConnectionFailed(data, offset, length);
                break;
            case EVENT_SESSION_STARTED:
                HandleSessionStarted();
                break;
            case EVENT_SESSION_FINISHED:
                HandleSessionFinished();
                break;
            case EVENT_SESSION_FAILED:
                HandleSessionFailed(data, offset, length);
                break;
            case EVENT_TTS_RESPONSE:
                HandleTTSResponse(data, offset, length, serializationMethod, compressionMethod);
                break;
            default:
                // 处理音频数据（无Event的音频帧）
                if (messageType == 0xB0 || messageType == 0xB4) // Audio-only response
                {
                    HandleAudioData(data, offset, length);
                }
                break;
        }
    }
    
    /// <summary>
    /// 处理ConnectionStarted事件
    /// </summary>
    private void HandleConnectionStarted(byte[] data, int offset, int length)
    {
        if (enableDebug)
        {
            Debug.Log("DoubaoStreamTTSV3: ConnectionStarted");
        }
        OnConnected?.Invoke();
    }
    
    /// <summary>
    /// 处理ConnectionFailed事件
    /// </summary>
    private void HandleConnectionFailed(byte[] data, int offset, int length)
    {
        string errorMsg = "连接失败";
        if (offset + 4 <= length)
        {
            // 解析错误信息
            byte[] payloadLengthBytes = new byte[4];
            Array.Copy(data, offset, payloadLengthBytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(payloadLengthBytes);
            }
            uint payloadLength = BitConverter.ToUInt32(payloadLengthBytes, 0);
            offset += 4;
            
            if (offset + payloadLength <= length)
            {
                byte[] payloadBytes = new byte[payloadLength];
                Array.Copy(data, offset, payloadBytes, 0, (int)payloadLength);
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);
                try
                {
                    var errorData = JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadJson);
                    if (errorData.ContainsKey("message"))
                    {
                        errorMsg = errorData["message"].ToString();
                    }
                }
                catch { }
            }
        }
        
        Debug.LogError($"DoubaoStreamTTSV3: {errorMsg}");
        OnError?.Invoke(errorMsg);
    }
    
    /// <summary>
    /// 处理SessionStarted事件
    /// </summary>
    private void HandleSessionStarted()
    {
        isSessionActive = true;
        if (enableDebug)
        {
            Debug.Log($"DoubaoStreamTTSV3: SessionStarted - {currentSessionId}");
        }
    }
    
    /// <summary>
    /// 处理SessionFinished事件
    /// </summary>
    private void HandleSessionFinished()
    {
        isSessionActive = false;
        if (enableDebug)
        {
            Debug.Log($"DoubaoStreamTTSV3: SessionFinished - {currentSessionId}");
        }
        OnSynthesisCompleted?.Invoke();
    }
    
    /// <summary>
    /// 处理SessionFailed事件
    /// </summary>
    private void HandleSessionFailed(byte[] data, int offset, int length)
    {
        string errorMsg = "Session失败";
        // 解析错误信息（类似ConnectionFailed）
        Debug.LogError($"DoubaoStreamTTSV3: {errorMsg}");
        OnError?.Invoke(errorMsg);
        isSessionActive = false;
    }
    
    /// <summary>
    /// 处理TTSResponse事件
    /// </summary>
    private void HandleTTSResponse(byte[] data, int offset, int length, byte serializationMethod, byte compressionMethod)
    {
        // 解析Session ID
        if (offset + 4 > length)
        {
            return;
        }
        
        byte[] sessionIdLengthBytes = new byte[4];
        Array.Copy(data, offset, sessionIdLengthBytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(sessionIdLengthBytes);
        }
        uint sessionIdLength = BitConverter.ToUInt32(sessionIdLengthBytes, 0);
        offset += 4;
        
        if (offset + sessionIdLength > length)
        {
            return;
        }
        
        // 跳过Session ID
        offset += (int)sessionIdLength;
        
        // 解析Payload
        if (offset + 4 > length)
        {
            return;
        }
        
        byte[] payloadLengthBytes = new byte[4];
        Array.Copy(data, offset, payloadLengthBytes, 0, 4);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(payloadLengthBytes);
        }
        uint payloadLength = BitConverter.ToUInt32(payloadLengthBytes, 0);
        offset += 4;
        
        if (offset + payloadLength > length)
        {
            return;
        }
        
        byte[] payloadBytes = new byte[payloadLength];
        Array.Copy(data, offset, payloadBytes, 0, (int)payloadLength);
        
        // 解压缩（如果需要）
        if (compressionMethod == 0x01) // gzip
        {
            payloadBytes = DecompressGZip(payloadBytes);
        }
        
        // 解析JSON
        if (serializationMethod == 0x01) // JSON
        {
            try
            {
                string payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var response = JsonConvert.DeserializeObject<TTSV3Response>(payloadJson);
                
                if (response != null && response.data != null && response.data.Length > 0)
                {
                    // 处理音频数据（base64编码）
                    foreach (var audioData in response.data)
                    {
                        if (!string.IsNullOrEmpty(audioData))
                        {
                            byte[] audioBytes = Convert.FromBase64String(audioData);
                            HandleAudioData(audioBytes, 0, audioBytes.Length);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"DoubaoStreamTTSV3: 解析TTSResponse失败: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// 处理音频数据（无Event的音频帧）
    /// </summary>
    private void HandleAudioData(byte[] data, int offset, int length)
    {
        if (offset >= length)
        {
            return;
        }
        
        // 解析Session ID（如果存在）
        if (offset + 4 <= length)
        {
            byte[] sessionIdLengthBytes = new byte[4];
            Array.Copy(data, offset, sessionIdLengthBytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(sessionIdLengthBytes);
            }
            uint sessionIdLength = BitConverter.ToUInt32(sessionIdLengthBytes, 0);
            offset += 4;
            
            if (offset + sessionIdLength <= length)
            {
                // 跳过Session ID
                offset += (int)sessionIdLength;
            }
        }
        
        // 提取音频数据
        int audioLength = length - offset;
        if (audioLength <= 0)
        {
            return;
        }
        
        byte[] audioData = new byte[audioLength];
        Array.Copy(data, offset, audioData, 0, audioLength);
        
        // 根据格式处理音频
        if (format == AudioFormat.pcm || format == AudioFormat.wav)
        {
            audioDataQueue.Enqueue(audioData);
            if (!isPlayingAudio)
            {
                audioPlayCoroutine = StartCoroutine(PlayAudioQueue());
            }
        }
        else if (format == AudioFormat.mp3 || format == AudioFormat.ogg_opus)
        {
            // MP3/OGG需要保存到临时文件后加载
            StartCoroutine(ProcessCompressedAudio(audioData));
        }
    }
    
    /// <summary>
    /// 播放音频队列（PCM格式）
    /// </summary>
    private IEnumerator PlayAudioQueue()
    {
        isPlayingAudio = true;
        List<byte[]> audioChunks = new List<byte[]>();
        
        while (audioDataQueue.Count > 0 || isSessionActive)
        {
            // 收集音频块
            while (audioDataQueue.Count > 0)
            {
                audioChunks.Add(audioDataQueue.Dequeue());
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        if (audioChunks.Count > 0)
        {
            // 合并所有音频块
            int totalLength = 0;
            foreach (var chunk in audioChunks)
            {
                totalLength += chunk.Length;
            }
            
            byte[] combinedAudio = new byte[totalLength];
            int currentOffset = 0;
            foreach (var chunk in audioChunks)
            {
                Array.Copy(chunk, 0, combinedAudio, currentOffset, chunk.Length);
                currentOffset += chunk.Length;
            }
            
            // 转换为AudioClip
            AudioClip audioClip = ConvertPCMToAudioClip(combinedAudio, sampleRate);
            if (audioClip != null)
            {
                OnAudioReceived?.Invoke(audioClip);
            }
        }
        
        isPlayingAudio = false;
    }
    
    /// <summary>
    /// 处理压缩音频（MP3/OGG）
    /// </summary>
    private IEnumerator ProcessCompressedAudio(byte[] audioData)
    {
        // 保存到临时文件
        string tempFile = System.IO.Path.Combine(Application.temporaryCachePath, $"doubao_audio_{Guid.NewGuid()}.{(format == AudioFormat.mp3 ? "mp3" : "ogg")}");
        System.IO.File.WriteAllBytes(tempFile, audioData);
        
        // 使用UnityWebRequest加载音频
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempFile}", 
            format == AudioFormat.mp3 ? AudioType.MPEG : AudioType.OGGVORBIS))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                if (audioClip != null)
                {
                    OnAudioReceived?.Invoke(audioClip);
                }
            }
            else
            {
                Debug.LogError($"DoubaoStreamTTSV3: 加载音频失败: {www.error}");
            }
        }
        
        // 删除临时文件
        try
        {
            if (System.IO.File.Exists(tempFile))
            {
                System.IO.File.Delete(tempFile);
            }
        }
        catch { }
    }
    
    /// <summary>
    /// 转换PCM为AudioClip
    /// </summary>
    private AudioClip ConvertPCMToAudioClip(byte[] pcmData, int sampleRate)
    {
        if (pcmData == null || pcmData.Length == 0)
        {
            return null;
        }
        
        // 假设16位PCM，单声道
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(pcmData, i * 2);
            samples[i] = sample / 32768.0f;
        }
        
        AudioClip clip = AudioClip.Create("DoubaoTTS", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        
        return clip;
    }
    
    /// <summary>
    /// 解压缩GZip
    /// </summary>
    private byte[] DecompressGZip(byte[] compressedData)
    {
        using (MemoryStream inputStream = new MemoryStream(compressedData))
        using (GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        using (MemoryStream outputStream = new MemoryStream())
        {
            gzipStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
    
    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        if (isSessionActive)
        {
            FinishSession();
        }
        
        if (receiveCoroutine != null)
        {
            StopCoroutine(receiveCoroutine);
            receiveCoroutine = null;
        }
        
        if (audioPlayCoroutine != null)
        {
            StopCoroutine(audioPlayCoroutine);
            audioPlayCoroutine = null;
        }
        
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client close", CancellationToken.None);
        }
        
        isConnected = false;
        isSessionActive = false;
        currentSessionId = "";
        
        OnDisconnected?.Invoke("已断开连接");
    }
    
    /// <summary>
    /// 检查是否已连接
    /// </summary>
    public bool IsConnected()
    {
        return isConnected && webSocket != null && webSocket.State == WebSocketState.Open;
    }
    
    /// <summary>
    /// 设置凭证
    /// </summary>
    public void SetCredentials(string appId, string token, string resourceId = "seed-tts-1.0")
    {
        this.appId = appId;
        this.token = token;
        this.resourceId = resourceId;
    }
    
    /// <summary>
    /// 设置说话人ID（会自动启用音色使用）
    /// </summary>
    public void SetSpeakerId(string speakerId)
    {
        this.speakerId = speakerId;
        this.useSpeakerId = !string.IsNullOrEmpty(speakerId);
    }
    
    /// <summary>
    /// 启用/禁用音色使用
    /// </summary>
    public void SetUseSpeakerId(bool use)
    {
        this.useSpeakerId = use;
    }
    
    /// <summary>
    /// 获取是否使用音色
    /// </summary>
    public bool GetUseSpeakerId()
    {
        return useSpeakerId;
    }
    
    // 数据类定义
    [System.Serializable]
    private class TTSV3SessionRequest
    {
        public TTSV3User user;
        public TTSV3ReqParams req_params;
    }
    
    [System.Serializable]
    private class TTSV3User
    {
        public string uid;
    }
    
    [System.Serializable]
    private class TTSV3ReqParams
    {
        public string text;
        public string speaker;
        public TTSV3AudioParams audio_params;
    }
    
    [System.Serializable]
    private class TTSV3AudioParams
    {
        public string format;
        public int sample_rate;
        public int speech_rate;
        public int loudness_rate;
        public string emotion;
        public float emotion_scale;
    }
    
    [System.Serializable]
    private class TTSV3TaskRequest
    {
        public TTSV3ReqParams req_params;
    }
    
    [System.Serializable]
    private class TTSV3Response
    {
        public string[] data;
    }
}
