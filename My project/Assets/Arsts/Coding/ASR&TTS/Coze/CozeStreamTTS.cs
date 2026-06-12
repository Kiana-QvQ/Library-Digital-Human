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

/// <summary>
/// Coze 双向流式语音合成
/// 支持流式输入文本并实时合成语音
/// </summary>
public class CozeStreamTTS : MonoBehaviour
{
    // API配置（由 CozeSettings 统一管理，不在 Inspector 中单独配置）
    private string apiKey = "";
    
    [Header("连接配置")]
    [Tooltip("WebSocket连接地址")]
    [SerializeField] private string wsUrl = "wss://api.coze.cn/v1/audio/speech";
    
    [Header("语音合成配置")]
    [Tooltip("音色ID（留空则使用默认音色，不指定音色）")]
    [SerializeField] private string voiceId = "";
    
    [Tooltip("是否使用音色（如果为false，即使设置了voiceId也不会使用）")]
    [SerializeField] private bool useVoiceId = false;
    
    [Tooltip("音频编码格式 (pcm/opus)")]
    [SerializeField] private AudioCodec codec = AudioCodec.pcm;
    
    [Tooltip("PCM采样率")]
    [SerializeField] private int sampleRate = 24000;
    
    [Tooltip("语速 [-50, 100]，0为正常语速")]
    [SerializeField] private int speechRate = 0;
    
    [Tooltip("音量 [-50, 100]，0为正常音量（不支持复刻音色）")]
    [SerializeField] private int loudnessRate = 0;
    
    [Tooltip("辅助信息，用于控制情绪、方言、语气等")]
    [SerializeField] private string contextTexts = "";
    
    [Header("情感配置（多情感音色）")]
    [Tooltip("情感类型")]
    [SerializeField] private EmotionType emotion = EmotionType.neutral;
    
    [Tooltip("情感强度 [1.0-5.0]")]
    [SerializeField] private float emotionScale = 4.0f;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // WebSocket连接
    private ClientWebSocket webSocket;
    private bool isConnected = false;
    private bool isConnecting = false;
    
    // 协程
    private Coroutine receiveCoroutine;
    private Coroutine audioPlayCoroutine;
    
    // 音频数据队列
    private Queue<byte[]> audioDataQueue = new Queue<byte[]>();
    private bool isPlayingAudio = false;
    
    // 事件ID生成器
    private int eventIdCounter = 0;
    
    // 回调事件
    public System.Action OnConnected;                    // 连接成功
    public System.Action<string> OnDisconnected;          // 断开连接
    public System.Action<AudioClip> OnAudioReceived;     // 收到音频
    public System.Action<string> OnError;                 // 发生错误
    public System.Action OnSynthesisCompleted;           // 合成完成
    
    // 音频编码格式枚举
    public enum AudioCodec
    {
        pcm,
        opus
    }
    
    // 情感类型枚举
    public enum EmotionType
    {
        happy,      // 开心
        sad,        // 悲伤
        angry,      // 生气
        surprised,   // 惊讶
        fear,       // 恐惧
        hate,       // 厌恶
        excited,    // 激动
        coldness,   // 冷漠
        neutral     // 中性
    }
    
    // 事件数据结构
    [System.Serializable]
    private class SpeechEvent
    {
        public string id;
        public string event_type;
        public object data;
        public EventDetail detail;
    }
    
    [System.Serializable]
    private class EventDetail
    {
        public string logid;
    }
    
    [System.Serializable]
    private class SpeechUpdateData
    {
        public OutputAudio output_audio;
    }
    
    [System.Serializable]
    private class OutputAudio
    {
        public string codec;
        public PCMConfig pcm_config;
        public OpusConfig opus_config;
        public int speech_rate;
        public int loudness_rate;
        public string voice_id;
        public string context_texts;
        public EmotionConfig emotion_config;
    }
    
    [System.Serializable]
    private class PCMConfig
    {
        public int sample_rate;
        public float frame_size_ms;
        public LimitConfig limit_config;
    }
    
    [System.Serializable]
    private class OpusConfig
    {
        public int bitrate;
        public bool use_cbr;
        public float frame_size_ms;
        public LimitConfig limit_config;
    }
    
    [System.Serializable]
    private class LimitConfig
    {
        public int period;
        public int max_frame_num;
    }
    
    [System.Serializable]
    private class EmotionConfig
    {
        public string emotion;
        public float emotion_scale;
    }
    
    [System.Serializable]
    private class InputTextBufferAppendData
    {
        public string delta;
    }
    
    [System.Serializable]
    private class SpeechAudioUpdateData
    {
        public string delta;  // base64编码的音频
    }
    
    [System.Serializable]
    private class ErrorData
    {
        public int code;
        public string msg;
    }
    
    /// <summary>
    /// 连接WebSocket
    /// </summary>
    public void Connect()
    {
        if (isConnecting || isConnected)
        {
            Debug.LogWarning("CozeStreamTTS: 已连接或正在连接中");
            return;
        }
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("CozeStreamTTS: API Key未配置！");
            OnError?.Invoke("API Key未配置");
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
            Debug.Log($"CozeStreamTTS: 连接WebSocket: {wsUrl}");
        }
        
        webSocket = new ClientWebSocket();
        
        // 设置Authorization header
        webSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        
        var connectTask = webSocket.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
        
        // 等待连接完成
        while (!connectTask.IsCompleted)
        {
            yield return null;
        }
        
        if (connectTask.IsFaulted)
        {
            string errorMsg = $"WebSocket连接失败: {connectTask.Exception?.GetBaseException()?.Message}";
            Debug.LogError($"CozeStreamTTS: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        if (webSocket.State != WebSocketState.Open)
        {
            string errorMsg = $"WebSocket连接失败，状态: {webSocket.State}";
            Debug.LogError($"CozeStreamTTS: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        isConnected = true;
        isConnecting = false;
        
        if (enableDebug)
        {
            Debug.Log("CozeStreamTTS: WebSocket连接成功");
        }
        
        // 开始接收消息
        receiveCoroutine = StartCoroutine(ReceiveMessages());
        
        // 发送配置更新
        UpdateSpeechConfig();
        
        OnConnected?.Invoke();
    }
    
    /// <summary>
    /// 更新语音合成配置
    /// </summary>
    public void UpdateSpeechConfig()
    {
        if (!isConnected)
        {
            Debug.LogWarning("CozeStreamTTS: 未连接，无法更新配置");
            return;
        }
        
        string eventId = GenerateEventId();
        
        OutputAudio outputAudio = new OutputAudio
        {
            codec = codec.ToString(),
            speech_rate = speechRate
        };
        
        // 只有在启用音色且设置了voiceId时才使用
        if (useVoiceId && !string.IsNullOrEmpty(voiceId))
        {
            outputAudio.voice_id = voiceId;
        }
        
        // 设置PCM配置
        if (codec == AudioCodec.pcm)
        {
            outputAudio.pcm_config = new PCMConfig
            {
                sample_rate = sampleRate
            };
        }
        // 设置Opus配置
        else if (codec == AudioCodec.opus)
        {
            outputAudio.opus_config = new OpusConfig
            {
                bitrate = 48000,
                use_cbr = false,
                frame_size_ms = 10.0f
            };
        }
        
        // 设置音量（不支持复刻音色）
        if (loudnessRate != 0)
        {
            outputAudio.loudness_rate = loudnessRate;
        }
        
        // 设置辅助信息
        if (!string.IsNullOrEmpty(contextTexts))
        {
            outputAudio.context_texts = contextTexts;
        }
        
        // 设置情感配置（多情感音色）
        if (emotion != EmotionType.neutral)
        {
            outputAudio.emotion_config = new EmotionConfig
            {
                emotion = emotion.ToString(),
                emotion_scale = emotionScale
            };
        }
        
        SpeechEvent speechEvent = new SpeechEvent
        {
            id = eventId,
            event_type = "speech.update",
            data = new SpeechUpdateData
            {
                output_audio = outputAudio
            }
        };
        
        SendEvent(speechEvent);
    }
    
    /// <summary>
    /// 流式输入文本片段
    /// </summary>
    /// <param name="text">文本片段</param>
    public void AppendText(string text)
    {
        if (!isConnected)
        {
            Debug.LogWarning("CozeStreamTTS: 未连接，无法发送文本");
            return;
        }
        
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        
        string eventId = GenerateEventId();
        
        SpeechEvent speechEvent = new SpeechEvent
        {
            id = eventId,
            event_type = "input_text_buffer.append",
            data = new InputTextBufferAppendData
            {
                delta = text
            }
        };
        
        SendEvent(speechEvent);
        
        if (enableDebug)
        {
            Debug.Log($"CozeStreamTTS: 发送文本片段: {text}");
        }
    }
    
    /// <summary>
    /// 提交文本（完成输入）
    /// </summary>
    public void CompleteText()
    {
        if (!isConnected)
        {
            Debug.LogWarning("CozeStreamTTS: 未连接，无法提交文本");
            return;
        }
        
        string eventId = GenerateEventId();
        
        SpeechEvent speechEvent = new SpeechEvent
        {
            id = eventId,
            event_type = "input_text_buffer.complete"
        };
        
        SendEvent(speechEvent);
        
        if (enableDebug)
        {
            Debug.Log("CozeStreamTTS: 提交文本完成");
        }
    }
    
    /// <summary>
    /// 流式合成文本（自动处理append和complete）
    /// </summary>
    /// <param name="text">完整文本</param>
    /// <param name="chunkSize">每次发送的字符数</param>
    public void SynthesizeText(string text, int chunkSize = 10)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("CozeStreamTTS: 文本为空");
            return;
        }
        
        StartCoroutine(SynthesizeTextCoroutine(text, chunkSize));
    }
    
    /// <summary>
    /// 流式合成文本协程
    /// </summary>
    private IEnumerator SynthesizeTextCoroutine(string text, int chunkSize)
    {
        int index = 0;
        while (index < text.Length)
        {
            int length = Mathf.Min(chunkSize, text.Length - index);
            string chunk = text.Substring(index, length);
            AppendText(chunk);
            index += length;
            
            // 等待一小段时间再发送下一段
            yield return new WaitForSeconds(0.05f);
        }
        
        // 提交完成
        CompleteText();
    }
    
    /// <summary>
    /// 接收消息协程
    /// </summary>
    private IEnumerator ReceiveMessages()
    {
        byte[] buffer = new byte[4096 * 4]; // 增大缓冲区以处理音频数据
        
        while (isConnected && webSocket != null && webSocket.State == WebSocketState.Open)
        {
            var receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            while (!receiveTask.IsCompleted)
            {
                yield return null;
            }
            
            if (receiveTask.IsFaulted)
            {
                Debug.LogError($"CozeStreamTTS: 接收消息失败: {receiveTask.Exception?.GetBaseException()?.Message}");
                break;
            }
            
            var result = receiveTask.Result;
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ProcessMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                if (enableDebug)
                {
                    Debug.Log("CozeStreamTTS: WebSocket连接关闭");
                }
                break;
            }
        }
        
        // 清理连接
        Disconnect();
    }
    
    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private void ProcessMessage(string message)
    {
        if (enableDebug)
        {
            Debug.Log($"CozeStreamTTS: 收到消息: {message}");
        }
        
        try
        {
            SpeechEvent speechEvent = JsonConvert.DeserializeObject<SpeechEvent>(message);
            
            if (speechEvent == null || string.IsNullOrEmpty(speechEvent.event_type))
            {
                Debug.LogWarning("CozeStreamTTS: 无法解析消息");
                return;
            }
            
            switch (speechEvent.event_type)
            {
                case "speech.created":
                    HandleSpeechCreated(speechEvent);
                    break;
                    
                case "speech.updated":
                    HandleSpeechUpdated(speechEvent);
                    break;
                    
                case "input_text_buffer.completed":
                    HandleInputTextBufferCompleted(speechEvent);
                    break;
                    
                case "speech.audio.update":
                    HandleSpeechAudioUpdate(speechEvent);
                    break;
                    
                case "speech.audio.completed":
                    HandleSpeechAudioCompleted(speechEvent);
                    break;
                    
                case "error":
                    HandleError(speechEvent);
                    break;
                    
                default:
                    if (enableDebug)
                    {
                        Debug.LogWarning($"CozeStreamTTS: 未知事件类型: {speechEvent.event_type}");
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CozeStreamTTS: 处理消息异常: {e.Message}");
            OnError?.Invoke($"处理消息异常: {e.Message}");
        }
    }
    
    /// <summary>
    /// 处理连接成功事件
    /// </summary>
    private void HandleSpeechCreated(SpeechEvent speechEvent)
    {
        if (enableDebug)
        {
            Debug.Log($"CozeStreamTTS: 连接成功，ID: {speechEvent.id}");
            if (speechEvent.detail != null && !string.IsNullOrEmpty(speechEvent.detail.logid))
            {
                Debug.Log($"CozeStreamTTS: 日志ID: {speechEvent.detail.logid}");
            }
        }
    }
    
    /// <summary>
    /// 处理配置更新完成事件
    /// </summary>
    private void HandleSpeechUpdated(SpeechEvent speechEvent)
    {
        if (enableDebug)
        {
            Debug.Log("CozeStreamTTS: 配置更新完成");
        }
    }
    
    /// <summary>
    /// 处理文本提交完成事件
    /// </summary>
    private void HandleInputTextBufferCompleted(SpeechEvent speechEvent)
    {
        if (enableDebug)
        {
            Debug.Log("CozeStreamTTS: 文本提交完成");
        }
    }
    
    /// <summary>
    /// 处理音频更新事件
    /// </summary>
    private void HandleSpeechAudioUpdate(SpeechEvent speechEvent)
    {
        try
        {
            // 解析音频数据
            if (speechEvent.data != null)
            {
                string jsonData = JsonConvert.SerializeObject(speechEvent.data);
                SpeechAudioUpdateData audioData = JsonConvert.DeserializeObject<SpeechAudioUpdateData>(jsonData);
                
                if (audioData != null && !string.IsNullOrEmpty(audioData.delta))
                {
                    // 解码base64音频数据
                    byte[] audioBytes = Convert.FromBase64String(audioData.delta);
                    
                    if (enableDebug)
                    {
                        Debug.Log($"CozeStreamTTS: 收到音频数据，大小: {audioBytes.Length} 字节");
                    }
                    
                    // 添加到音频队列
                    lock (audioDataQueue)
                    {
                        audioDataQueue.Enqueue(audioBytes);
                    }
                    
                    // 开始播放音频（如果还没开始）
                    if (!isPlayingAudio)
                    {
                        audioPlayCoroutine = StartCoroutine(PlayAudioQueue());
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CozeStreamTTS: 处理音频更新异常: {e.Message}");
            OnError?.Invoke($"处理音频更新异常: {e.Message}");
        }
    }
    
    /// <summary>
    /// 处理音频合成完成事件
    /// </summary>
    private void HandleSpeechAudioCompleted(SpeechEvent speechEvent)
    {
        if (enableDebug)
        {
            Debug.Log("CozeStreamTTS: 音频合成完成");
            if (speechEvent.detail != null && !string.IsNullOrEmpty(speechEvent.detail.logid))
            {
                Debug.Log($"CozeStreamTTS: 日志ID: {speechEvent.detail.logid}");
            }
        }
        
        OnSynthesisCompleted?.Invoke();
    }
    
    /// <summary>
    /// 处理错误事件
    /// </summary>
    private void HandleError(SpeechEvent speechEvent)
    {
        try
        {
            if (speechEvent.data != null)
            {
                string jsonData = JsonConvert.SerializeObject(speechEvent.data);
                ErrorData errorData = JsonConvert.DeserializeObject<ErrorData>(jsonData);
                
                if (errorData != null)
                {
                    string errorMsg = $"错误 (Code: {errorData.code}): {errorData.msg}";
                    if (speechEvent.detail != null && !string.IsNullOrEmpty(speechEvent.detail.logid))
                    {
                        errorMsg += $"\n日志ID: {speechEvent.detail.logid}";
                    }
                    
                    Debug.LogError($"CozeStreamTTS: {errorMsg}");
                    OnError?.Invoke(errorMsg);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CozeStreamTTS: 处理错误事件异常: {e.Message}");
            OnError?.Invoke($"处理错误事件异常: {e.Message}");
        }
    }
    
    /// <summary>
    /// 播放音频队列
    /// </summary>
    private IEnumerator PlayAudioQueue()
    {
        isPlayingAudio = true;
        List<byte[]> accumulatedAudio = new List<byte[]>();
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        float lastPlayTime = 0f;
        const float PLAY_INTERVAL = 0.1f; // 每100ms检查一次并播放
        
        while (isConnected || audioDataQueue.Count > 0 || accumulatedAudio.Count > 0)
        {
            // 收集队列中的音频数据
            lock (audioDataQueue)
            {
                while (audioDataQueue.Count > 0)
                {
                    accumulatedAudio.Add(audioDataQueue.Dequeue());
                }
            }
            
            // 如果累积了足够的音频数据（约0.2秒）或连接已断开，则播放
            bool shouldPlay = false;
            if (!isConnected && accumulatedAudio.Count > 0)
            {
                // 连接断开，播放剩余数据
                shouldPlay = true;
            }
            else if (accumulatedAudio.Count > 0 && Time.time - lastPlayTime >= PLAY_INTERVAL)
            {
                // 累积了足够的数据或到了播放时间
                int totalBytes = 0;
                foreach (var chunk in accumulatedAudio)
                {
                    totalBytes += chunk.Length;
                }
                
                // 约0.2秒的音频数据（24000采样率，16位，单声道 = 24000 * 2 * 0.2 = 9600字节）
                if (totalBytes >= 9600 || !isConnected)
                {
                    shouldPlay = true;
                }
            }
            
            if (shouldPlay && accumulatedAudio.Count > 0)
            {
                // 合并音频数据
                int totalLength = 0;
                foreach (var chunk in accumulatedAudio)
                {
                    totalLength += chunk.Length;
                }
                
                byte[] combinedAudio = new byte[totalLength];
                int offset = 0;
                foreach (var chunk in accumulatedAudio)
                {
                    Array.Copy(chunk, 0, combinedAudio, offset, chunk.Length);
                    offset += chunk.Length;
                }
                
                accumulatedAudio.Clear();
                
                // 转换为AudioClip并播放
                AudioClip audioClip = ConvertPCMToAudioClip(combinedAudio, sampleRate);
                if (audioClip != null)
                {
                    OnAudioReceived?.Invoke(audioClip);
                    
                    // 如果当前没有播放音频，直接播放
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = audioClip;
                        audioSource.Play();
                        lastPlayTime = Time.time;
                    }
                    else
                    {
                        // 如果正在播放，将新音频加入队列等待
                        // 这里可以扩展为音频队列系统
                        // 暂时等待当前播放完成
                        yield return new WaitWhile(() => audioSource.isPlaying);
                        audioSource.clip = audioClip;
                        audioSource.Play();
                        lastPlayTime = Time.time;
                    }
                }
            }
            
            yield return new WaitForSeconds(0.05f); // 每50ms检查一次
        }
        
        isPlayingAudio = false;
    }
    
    /// <summary>
    /// 将PCM数据转换为AudioClip
    /// </summary>
    private AudioClip ConvertPCMToAudioClip(byte[] pcmData, int sampleRate)
    {
        try
        {
            // PCM数据是16位单声道
            int sampleCount = pcmData.Length / 2;
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                // 读取16位PCM样本（小端序）
                short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                samples[i] = sample / 32768.0f; // 归一化到[-1, 1]
            }
            
            AudioClip clip = AudioClip.Create("CozeTTS", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError($"CozeStreamTTS: 转换PCM到AudioClip失败: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 发送事件
    /// </summary>
    private void SendEvent(SpeechEvent speechEvent)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("CozeStreamTTS: WebSocket未连接，无法发送事件");
            return;
        }
        
        try
        {
            string json = JsonConvert.SerializeObject(speechEvent);
            byte[] data = Encoding.UTF8.GetBytes(json);
            
            var sendTask = webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            
            // 等待发送完成（在协程中）
            StartCoroutine(WaitForSendComplete(sendTask));
        }
        catch (Exception e)
        {
            Debug.LogError($"CozeStreamTTS: 发送事件失败: {e.Message}");
            OnError?.Invoke($"发送事件失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 等待发送完成
    /// </summary>
    private IEnumerator WaitForSendComplete(Task sendTask)
    {
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }
        
        if (sendTask.IsFaulted)
        {
            Debug.LogError($"CozeStreamTTS: 发送失败: {sendTask.Exception?.GetBaseException()?.Message}");
        }
    }
    
    /// <summary>
    /// 生成事件ID
    /// </summary>
    private string GenerateEventId()
    {
        return $"event_{++eventIdCounter}_{DateTime.Now.Ticks}";
    }
    
    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        if (isConnected && webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    var closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                    // 等待关闭完成（最多等待1秒）
                    float timeout = 1f;
                    float elapsed = 0f;
                    while (!closeTask.IsCompleted && elapsed < timeout)
                    {
                        elapsed += Time.deltaTime;
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CozeStreamTTS: 关闭连接异常: {e.Message}");
            }
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
        
        if (webSocket != null)
        {
            try
            {
                webSocket.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"CozeStreamTTS: 释放WebSocket异常: {e.Message}");
            }
            webSocket = null;
        }
        
        isConnected = false;
        isConnecting = false;
        isPlayingAudio = false;
        
        lock (audioDataQueue)
        {
            audioDataQueue.Clear();
        }
        
        OnDisconnected?.Invoke("手动断开");
    }
    
    /// <summary>
    /// 检查是否已连接
    /// </summary>
    public bool IsConnected()
    {
        return isConnected && webSocket != null && webSocket.State == WebSocketState.Open;
    }
    
    /// <summary>
    /// 设置API Key
    /// </summary>
    public void SetAPIKey(string key)
    {
        apiKey = key;
    }
    
    /// <summary>
    /// 设置音色ID
    /// </summary>
    public void SetVoiceId(string id)
    {
        voiceId = id;
        useVoiceId = !string.IsNullOrEmpty(id);
        if (isConnected)
        {
            UpdateSpeechConfig();
        }
    }
    
    /// <summary>
    /// 禁用音色（使用默认音色）
    /// </summary>
    public void DisableVoiceId()
    {
        useVoiceId = false;
        if (isConnected)
        {
            UpdateSpeechConfig();
        }
    }
    
    private void OnDestroy()
    {
        Disconnect();
    }
}
