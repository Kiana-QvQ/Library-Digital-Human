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
/// Doubao（火山引擎）WebSocket流式语音合成
/// 支持使用克隆后的speaker_id进行语音合成
/// </summary>
public class DoubaoStreamTTS : MonoBehaviour
{
    // API配置（由 DoubaoSettings 统一管理，不显示在 Inspector 中）
    private string appId = "";
    private string token = "";
    private string cluster = "volcano_icl";
    
    [Header("语音合成配置")]
    [Tooltip("是否使用音色（如果为false，即使设置了speakerId也不会使用，默认不使用）")]
    [SerializeField] private bool useSpeakerId = false;
    
    [Tooltip("说话人ID（使用克隆后的speaker_id或系统音色，仅在useSpeakerId为true时生效）")]
    [SerializeField] private string speakerId = "";
    
    [Tooltip("音频编码格式（推荐使用pcm，mp3需要额外解码库）")]
    [SerializeField] private AudioEncoding encoding = AudioEncoding.pcm;
    
    [Tooltip("语速比例 [0.5-2.0]")]
    [SerializeField] private float speedRatio = 1.0f;
    
    [Tooltip("音量比例 [0.0-2.0]")]
    [SerializeField] private float volumeRatio = 1.0f;
    
    [Tooltip("音调比例 [0.5-2.0]")]
    [SerializeField] private float pitchRatio = 1.0f;
    
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
    
    // WebSocket地址
    private string wsUrl = "wss://openspeech.bytedance.com/api/v1/tts/ws_binary";
    
    // 默认协议头
    private readonly byte[] defaultHeader = new byte[] { 0x11, 0x10, 0x11, 0x00 };
    
    // 回调事件
    public System.Action OnConnected;                    // 连接成功
    public System.Action<string> OnDisconnected;          // 断开连接
    public System.Action<AudioClip> OnAudioReceived;     // 收到音频
    public System.Action<string> OnError;                 // 发生错误
    public System.Action OnSynthesisCompleted;           // 合成完成
    
    // 音频编码格式枚举
    public enum AudioEncoding
    {
        mp3,
        pcm
    }
    
    /// <summary>
    /// 连接WebSocket
    /// </summary>
    public void Connect()
    {
        if (isConnecting || isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTS: 已连接或正在连接中");
            return;
        }
        
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("DoubaoStreamTTS: AppID或Token未配置！");
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
            Debug.Log($"DoubaoStreamTTS: 连接WebSocket: {wsUrl}");
        }
        
        webSocket = new ClientWebSocket();
        
        // 设置Authorization header
        webSocket.Options.SetRequestHeader("Authorization", $"Bearer; {token}");
        
        var connectTask = webSocket.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
        
        // 等待连接完成
        while (!connectTask.IsCompleted)
        {
            yield return null;
        }
        
        if (connectTask.IsFaulted)
        {
            string errorMsg = $"WebSocket连接失败: {connectTask.Exception?.GetBaseException()?.Message}";
            Debug.LogError($"DoubaoStreamTTS: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        if (webSocket.State != WebSocketState.Open)
        {
            string errorMsg = $"WebSocket连接失败，状态: {webSocket.State}";
            Debug.LogError($"DoubaoStreamTTS: {errorMsg}");
            OnError?.Invoke(errorMsg);
            isConnecting = false;
            yield break;
        }
        
        isConnected = true;
        isConnecting = false;
        
        if (enableDebug)
        {
            Debug.Log("DoubaoStreamTTS: WebSocket连接成功");
        }
        
        // 开始接收消息
        receiveCoroutine = StartCoroutine(ReceiveMessages());
        
        OnConnected?.Invoke();
    }
    
    /// <summary>
    /// 合成文本（一次性发送完整文本）
    /// </summary>
    /// <param name="text">要合成的文本</param>
    public void SynthesizeText(string text)
    {
        if (!isConnected)
        {
            Debug.LogWarning("DoubaoStreamTTS: 未连接，无法合成文本");
            return;
        }
        
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("DoubaoStreamTTS: 文本为空");
            return;
        }
        
        StartCoroutine(SynthesizeTextCoroutine(text));
    }
    
    /// <summary>
    /// 合成文本协程
    /// </summary>
    private IEnumerator SynthesizeTextCoroutine(string text)
    {
        // 构建请求JSON
        TTSRequest request = new TTSRequest
        {
            app = new AppConfig
            {
                appid = appId,
                token = token,
                cluster = cluster
            },
            user = new UserConfig
            {
                uid = SystemInfo.deviceUniqueIdentifier
            },
            audio = new AudioConfig
            {
                voice_type = (useSpeakerId && !string.IsNullOrEmpty(speakerId)) ? speakerId : "",
                encoding = encoding.ToString(),
                speed_ratio = speedRatio,
                volume_ratio = volumeRatio,
                pitch_ratio = pitchRatio
            },
            request = new RequestConfig
            {
                reqid = Guid.NewGuid().ToString(),
                text = text,
                text_type = "plain",
                operation = "submit"
            }
        };
        
        string jsonRequest = JsonConvert.SerializeObject(request);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonRequest);
        
        // 压缩数据
        byte[] compressedBytes = CompressGZip(payloadBytes);
        
        // 构建完整请求
        byte[] fullRequest = new byte[defaultHeader.Length + 4 + compressedBytes.Length];
        Array.Copy(defaultHeader, 0, fullRequest, 0, defaultHeader.Length);
        Array.Copy(BitConverter.GetBytes(compressedBytes.Length), 0, fullRequest, defaultHeader.Length, 4);
        // 注意：需要转换为大端序
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(fullRequest, defaultHeader.Length, 4);
        }
        Array.Copy(compressedBytes, 0, fullRequest, defaultHeader.Length + 4, compressedBytes.Length);
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoStreamTTS: 发送合成请求，文本长度: {text.Length}");
        }
        
        // 发送请求
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(fullRequest), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        // 等待发送完成
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }
        
        if (sendTask.IsFaulted)
        {
            Debug.LogError($"DoubaoStreamTTS: 发送请求失败: {sendTask.Exception?.GetBaseException()?.Message}");
            OnError?.Invoke($"发送请求失败: {sendTask.Exception?.GetBaseException()?.Message}");
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
                Debug.LogError($"DoubaoStreamTTS: 接收消息失败: {receiveTask.Exception?.GetBaseException()?.Message}");
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
                    Debug.Log("DoubaoStreamTTS: WebSocket连接关闭");
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
        
        // 解析协议头
        byte protocolVersion = (byte)(data[0] >> 4);
        byte headerSize = (byte)(data[0] & 0x0F);
        byte messageType = (byte)(data[1] >> 4);
        byte messageFlags = (byte)(data[1] & 0x0F);
        byte serializationMethod = (byte)(data[2] >> 4);
        byte compression = (byte)(data[2] & 0x0F);
        
        if (headerSize * 4 > length)
        {
            return;
        }
        
        byte[] payload = new byte[length - headerSize * 4];
        Array.Copy(data, headerSize * 4, payload, 0, payload.Length);
        
        // 处理音频响应 (0x0B)
        if (messageType == 0x0B)
        {
            if (messageFlags == 0)
            {
                return; // 无序列号
            }
            
            // 读取序列号（4字节，大端序）
            int sequenceNumber = 0;
            for (int i = 0; i < 4; i++)
            {
                sequenceNumber = (sequenceNumber << 8) | payload[i];
            }
            if ((sequenceNumber & 0x80000000) != 0)
            {
                sequenceNumber = (int)((uint)sequenceNumber | 0x80000000);
            }
            
            // 跳过8字节（序列号4字节 + 保留4字节）
            if (payload.Length > 8)
            {
                byte[] audioData = new byte[payload.Length - 8];
                Array.Copy(payload, 8, audioData, 0, audioData.Length);
                
                if (enableDebug)
                {
                    Debug.Log($"DoubaoStreamTTS: 收到音频数据，大小: {audioData.Length} 字节，序列号: {sequenceNumber}");
                }
                
                // 根据编码格式处理音频数据
                if (encoding == AudioEncoding.mp3)
                {
                    // MP3数据直接加入MP3队列
                    lock (mp3DataQueue)
                    {
                        mp3DataQueue.Enqueue(audioData);
                    }
                    
                    // 开始处理MP3队列（如果还没开始）
                    if (mp3LoadCoroutine == null)
                    {
                        mp3LoadCoroutine = StartCoroutine(ProcessMP3Queue());
                    }
                }
                else if (encoding == AudioEncoding.pcm)
                {
                    // PCM数据加入音频队列
                    lock (audioDataQueue)
                    {
                        audioDataQueue.Enqueue(audioData);
                    }
                    
                    // 开始播放音频（如果还没开始）
                    if (!isPlayingAudio)
                    {
                        audioPlayCoroutine = StartCoroutine(PlayAudioQueue());
                    }
                }
                
                // 序列号小于0表示最后一段
                if (sequenceNumber < 0)
                {
                    if (enableDebug)
                    {
                        Debug.Log("DoubaoStreamTTS: 音频合成完成");
                    }
                    OnSynthesisCompleted?.Invoke();
                }
            }
        }
        // 处理错误消息 (0x0F)
        else if (messageType == 0x0F)
        {
            if (payload.Length >= 8)
            {
                // 读取错误码（4字节，大端序）
                uint errorCode = 0;
                for (int i = 0; i < 4; i++)
                {
                    errorCode = (errorCode << 8) | payload[i];
                }
                
                // 读取错误消息长度（4字节，大端序）
                uint msgSize = 0;
                for (int i = 4; i < 8; i++)
                {
                    msgSize = (msgSize << 8) | payload[i];
                }
                
                // 读取错误消息
                if (payload.Length >= 8 + msgSize)
                {
                    byte[] errorMsgBytes = new byte[msgSize];
                    Array.Copy(payload, 8, errorMsgBytes, 0, (int)msgSize);
                    
                    // 如果压缩了，解压
                    if (compression == 1)
                    {
                        try
                        {
                            errorMsgBytes = DecompressGZip(errorMsgBytes);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"DoubaoStreamTTS: 解压错误消息失败: {e.Message}");
                        }
                    }
                    
                    string errorMsg = Encoding.UTF8.GetString(errorMsgBytes);
                    string fullErrorMsg = $"合成错误 (Code: {errorCode}): {errorMsg}";
                    
                    Debug.LogError($"DoubaoStreamTTS: {fullErrorMsg}");
                    OnError?.Invoke(fullErrorMsg);
                }
            }
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
        
        int sampleRate = 24000; // 默认采样率（PCM格式）
        
        // 根据编码格式确定采样率
        if (encoding == AudioEncoding.pcm)
        {
            sampleRate = 24000; // PCM默认采样率
        }
        
        while (isConnected || (encoding == AudioEncoding.pcm && (audioDataQueue.Count > 0 || accumulatedAudio.Count > 0)))
        {
            // 收集队列中的音频数据
            lock (audioDataQueue)
            {
                while (audioDataQueue.Count > 0)
                {
                    accumulatedAudio.Add(audioDataQueue.Dequeue());
                }
            }
            
            // 如果累积了足够的音频数据或连接已断开，则播放
            bool shouldPlay = false;
            if (!isConnected && accumulatedAudio.Count > 0)
            {
                shouldPlay = true;
            }
            else if (accumulatedAudio.Count > 0)
            {
                int totalBytes = 0;
                foreach (var chunk in accumulatedAudio)
                {
                    totalBytes += chunk.Length;
                }
                
                // 约0.2秒的音频数据（PCM格式）
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
                
                // PCM格式直接转换并播放
                AudioClip audioClip = ConvertPCMToAudioClip(combinedAudio, sampleRate);
                
                if (audioClip != null)
                {
                    OnAudioReceived?.Invoke(audioClip);
                    
                    // 如果当前没有播放音频，直接播放
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = audioClip;
                        audioSource.Play();
                    }
                    else
                    {
                        // 如果正在播放，等待当前播放完成
                        yield return new WaitWhile(() => audioSource.isPlaying);
                        audioSource.clip = audioClip;
                        audioSource.Play();
                    }
                }
            }
            
            yield return new WaitForSeconds(0.05f);
        }
        
        isPlayingAudio = false;
    }
    
    // MP3音频数据队列（用于合并多个MP3片段）
    private Queue<byte[]> mp3DataQueue = new Queue<byte[]>();
    private Coroutine mp3LoadCoroutine;
    
    /// <summary>
    /// 将MP3数据转换为AudioClip（通过临时文件加载）
    /// 注意：Unity原生不支持MP3解码，需要通过文件加载
    /// 更好的方案是使用PCM格式或集成MP3解码库（如NAudio）
    /// </summary>
    private void ConvertMP3ToAudioClip(byte[] mp3Data)
    {
        // 将MP3数据加入队列，等待合并后加载
        lock (mp3DataQueue)
        {
            mp3DataQueue.Enqueue(mp3Data);
        }
        
        // 如果没有正在加载，开始加载协程
        if (mp3LoadCoroutine == null)
        {
            mp3LoadCoroutine = StartCoroutine(ProcessMP3Queue());
        }
    }

    /// <summary>
    /// 处理MP3队列（合并后加载）
    /// </summary>
    /// <summary>
    /// 处理MP3队列（合并后加载）
    /// </summary>
    private IEnumerator ProcessMP3Queue()
    {
        List<byte[]> accumulatedMP3 = new List<byte[]>();

        while (isConnected || mp3DataQueue.Count > 0 || accumulatedMP3.Count > 0)
        {
            // 收集队列中的MP3数据
            lock (mp3DataQueue)
            {
                while (mp3DataQueue.Count > 0)
                {
                    accumulatedMP3.Add(mp3DataQueue.Dequeue());
                }
            }

            // 如果累积了足够的MP3数据或连接已断开，则加载
            bool shouldLoad = false;
            if (!isConnected && accumulatedMP3.Count > 0)
            {
                shouldLoad = true;
            }
            else if (accumulatedMP3.Count > 0)
            {
                int totalBytes = 0;
                foreach (var chunk in accumulatedMP3)
                {
                    totalBytes += chunk.Length;
                }

                // 约0.5秒的MP3数据（约12KB）或连接断开
                if (totalBytes >= 12000 || !isConnected)
                {
                    shouldLoad = true;
                }
            }

            if (shouldLoad && accumulatedMP3.Count > 0)
            {
                // 合并MP3数据
                int totalLength = 0;
                foreach (var chunk in accumulatedMP3)
                {
                    totalLength += chunk.Length;
                }

                byte[] combinedMP3 = new byte[totalLength];
                int offset = 0;
                foreach (var chunk in accumulatedMP3)
                {
                    Array.Copy(chunk, 0, combinedMP3, offset, chunk.Length);
                    offset += chunk.Length;
                }

                accumulatedMP3.Clear();

                // 保存到临时文件并加载
                string tempPath = Path.Combine(Application.temporaryCachePath, $"doubao_tts_{Guid.NewGuid()}.mp3");

                // 将yield return移到try块外部
                bool fileSaved = false;
                try
                {
                    File.WriteAllBytes(tempPath, combinedMP3);
                    fileSaved = true;

                    if (enableDebug)
                    {
                        Debug.Log($"DoubaoStreamTTS: MP3数据已保存到临时文件，大小: {combinedMP3.Length} 字节");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"DoubaoStreamTTS: 保存MP3文件失败: {e.Message}");
                }

                // 在try/catch外部执行yield操作
                if (fileSaved)
                {
                    // 使用UnityWebRequest加载MP3
                    yield return StartCoroutine(LoadMP3FromFile(tempPath));
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        mp3LoadCoroutine = null;
    }

    /// <summary>
    /// 从文件加载MP3（协程）
    /// </summary>
    private IEnumerator LoadMP3FromFile(string filePath)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    OnAudioReceived?.Invoke(clip);
                    
                    // 播放音频
                    AudioSource audioSource = GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = gameObject.AddComponent<AudioSource>();
                    }
                    
                    if (!audioSource.isPlaying)
                    {
                        audioSource.clip = clip;
                        audioSource.Play();
                    }
                    else
                    {
                        yield return new WaitWhile(() => audioSource.isPlaying);
                        audioSource.clip = clip;
                        audioSource.Play();
                    }
                }
            }
            else
            {
                Debug.LogError($"DoubaoStreamTTS: 加载MP3失败: {www.error}");
                OnError?.Invoke($"加载MP3失败: {www.error}");
            }
        }
        
        // 清理临时文件
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"DoubaoStreamTTS: 删除临时文件失败: {e.Message}");
        }
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
            
            AudioClip clip = AudioClip.Create("DoubaoTTS", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError($"DoubaoStreamTTS: 转换PCM到AudioClip失败: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// GZip压缩
    /// </summary>
    private byte[] CompressGZip(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
    
    /// <summary>
    /// GZip解压
    /// </summary>
    private byte[] DecompressGZip(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (GZipStream gzip = new GZipStream(ms, CompressionMode.Decompress))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return output.ToArray();
                }
            }
        }
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
                Debug.LogWarning($"DoubaoStreamTTS: 关闭连接异常: {e.Message}");
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
        
        if (mp3LoadCoroutine != null)
        {
            StopCoroutine(mp3LoadCoroutine);
            mp3LoadCoroutine = null;
        }
        
        lock (mp3DataQueue)
        {
            mp3DataQueue.Clear();
        }
        
        if (webSocket != null)
        {
            try
            {
                webSocket.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"DoubaoStreamTTS: 释放WebSocket异常: {e.Message}");
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
    /// 设置凭证
    /// </summary>
    public void SetCredentials(string appId, string token, string cluster = "volcano_icl")
    {
        this.appId = appId;
        this.token = token;
        this.cluster = cluster;
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
    
    private void OnDestroy()
    {
        Disconnect();
    }
    
    // 请求数据结构
    [System.Serializable]
    private class TTSRequest
    {
        public AppConfig app;
        public UserConfig user;
        public AudioConfig audio;
        public RequestConfig request;
    }
    
    [System.Serializable]
    private class AppConfig
    {
        public string appid;
        public string token;
        public string cluster;
    }
    
    [System.Serializable]
    private class UserConfig
    {
        public string uid;
    }
    
    [System.Serializable]
    private class AudioConfig
    {
        public string voice_type;
        public string encoding;
        public float speed_ratio;
        public float volume_ratio;
        public float pitch_ratio;
    }
    
    [System.Serializable]
    private class RequestConfig
    {
        public string reqid;
        public string text;
        public string text_type;
        public string operation;
    }
}
