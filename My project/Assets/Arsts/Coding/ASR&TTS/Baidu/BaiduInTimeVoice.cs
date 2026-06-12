using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

[RequireComponent(typeof(BaiduSettings))]
public class BaiduInTimeVoice : STT
{
    #region 实时语音识别
    /// <summary>
    /// token配置
    /// </summary>
    [SerializeField] private BaiduSettings m_Settings;
    
    /// <summary>
    /// WebSocket连接
    /// </summary>
    private ClientWebSocket webSocket;
    
    /// <summary>
    /// 是否正在连接
    /// </summary>
    private bool isConnecting = false;
    
    /// <summary>
    /// 是否已连接
    /// </summary>
    private bool isConnected = false;
    
    /// <summary>
    /// 是否正在发送音频
    /// </summary>
    private bool isSendingAudio = false;
    
    /// <summary>
    /// 实时识别回调
    /// </summary>
    private Action<string> realTimeCallback;
    
    /// <summary>
    /// 音频数据队列
    /// </summary>
    private Queue<byte[]> audioDataQueue = new Queue<byte[]>();
    
    /// <summary>
    /// 连接建立前的音频数据缓存（限制大小，避免内存溢出）
    /// </summary>
    private Queue<byte[]> pendingAudioDataQueue = new Queue<byte[]>();
    
    /// <summary>
    /// 连接建立前缓存的最大音频块数（约2秒的音频数据）
    /// </summary>
    private const int MAX_PENDING_AUDIO_BLOCKS = 12; // 12 * 160ms ≈ 2秒
    
    /// <summary>
    /// 发送音频的协程
    /// </summary>
    private Coroutine sendAudioCoroutine;
    
    /// <summary>
    /// 接收消息的协程
    /// </summary>
    private Coroutine receiveCoroutine;
    
    /// <summary>
    /// 百度实时语音识别WebSocket地址
    /// </summary>
    private string wsUrl = "wss://vop.baidu.com/realtime_asr";
    
    /// <summary>
    /// 音频采样率
    /// </summary>
    private const int SAMPLE_RATE = 16000;
    
    /// <summary>
    /// 音频块大小（160ms）
    /// </summary>
    private const int CHUNK_SIZE = 16000 * 2 / 1000 * 160; // 160ms的音频数据

    private void Awake()
    {
        m_Settings = this.GetComponent<BaiduSettings>();
    }
    
    /// <summary>
    /// 开始实时语音识别
    /// </summary>
    /// <param name="_callback">识别结果回调</param>
    public void StartRealTimeRecognition(Action<string> _callback)
    {
        if (isConnected || isConnecting)
        {
            Debug.LogWarning("实时语音识别已启动或正在连接中");
            return;
        }
        
        realTimeCallback = _callback;
        StartCoroutine(ConnectAndStartRecognition());
    }
    
    /// <summary>
    /// 停止实时语音识别
    /// </summary>
    public void StopRealTimeRecognition()
    {
        if (sendAudioCoroutine != null)
        {
            StopCoroutine(sendAudioCoroutine);
            sendAudioCoroutine = null;
        }

        if (receiveCoroutine != null)
        {
            StopCoroutine(receiveCoroutine);
            receiveCoroutine = null;
        }

        isSendingAudio = false;
        lock (audioDataQueue)
        {
            audioDataQueue.Clear();
        }
        lock (pendingAudioDataQueue)
        {
            pendingAudioDataQueue.Clear();
        }

        if (isConnected && webSocket != null)
        {
            // 发送取消帧，与Python版本一致
            StartCoroutine(SendCancelFrame());
        }
        else
        {
            // 如果没有连接，直接清理
            CleanupWebSocket();
        }
    }
    
    /// <summary>
    /// 发送取消帧（与Python版本一致）
    /// </summary>
    private IEnumerator SendCancelFrame()
    {
        var cancelParams = new
        {
            type = "CANCEL" // 与Python版本完全一致
        };

        string json = JsonConvert.SerializeObject(cancelParams);
        byte[] data = Encoding.UTF8.GetBytes(json);
        
        Debug.Log($"发送取消帧: {json}");
        
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"发送取消帧失败: {sendTask.Exception}");
        }
        else
        {
            Debug.Log("发送取消帧成功");
        }

        // 关闭连接
        if (webSocket != null)
        {
            var closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cancelled", CancellationToken.None);
            while (!closeTask.IsCompleted)
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// 添加音频数据到队列
    /// </summary>
    /// <param name="audioData">音频数据</param>
    public void AddAudioData(byte[] audioData)
    {
        if (isConnected && isSendingAudio)
        {
            // 连接已建立，直接添加到发送队列
            lock (audioDataQueue)
            {
                audioDataQueue.Enqueue(audioData);
            }
        }
        else if (isConnecting || realTimeCallback != null)
        {
            // 连接建立中或已启动识别，缓存音频数据（限制大小）
            lock (pendingAudioDataQueue)
            {
                if (pendingAudioDataQueue.Count < MAX_PENDING_AUDIO_BLOCKS)
                {
                    pendingAudioDataQueue.Enqueue(audioData);
                }
                else
                {
                    // 队列已满，移除最旧的数据，添加新数据（保持最新的音频）
                    pendingAudioDataQueue.Dequeue();
                    pendingAudioDataQueue.Enqueue(audioData);
                }
            }
        }
        // 如果既没有连接也没有回调，说明识别还没启动，但我们也缓存一些数据
        // 这样在唤醒后启动识别时，可以立即有数据发送
        else
        {
            lock (pendingAudioDataQueue)
            {
                if (pendingAudioDataQueue.Count < MAX_PENDING_AUDIO_BLOCKS)
                {
                    pendingAudioDataQueue.Enqueue(audioData);
                }
                else
                {
                    // 队列已满，移除最旧的数据，添加新数据
                    pendingAudioDataQueue.Dequeue();
                    pendingAudioDataQueue.Enqueue(audioData);
                }
            }
        }
    }

    /// <summary>
    /// 连接并开始识别
    /// </summary>
    private IEnumerator ConnectAndStartRecognition()
    {
        yield return m_Settings.RefreshToken();
        
        if (string.IsNullOrEmpty(m_Settings.m_Token))
        {
            Debug.LogError("Token为空，无法开始实时语音识别");
            realTimeCallback?.Invoke("Token无效");
            yield break;
        }

        isConnecting = true;
        
        // 生成WebSocket URL
        string url = $"{wsUrl}?sn={System.Guid.NewGuid()}";
        Debug.Log($"连接WebSocket: {url}");

        webSocket = new ClientWebSocket();
        var connectTask = webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
        
        // 等待连接完成
        while (!connectTask.IsCompleted)
        {
            yield return null;
        }

        if (connectTask.IsFaulted)
        {
            Debug.LogError($"WebSocket连接失败: {connectTask.Exception}");
            realTimeCallback?.Invoke("连接失败");
            isConnecting = false;
            yield break;
        }

        isConnected = true;
        isConnecting = false;
        Debug.Log("WebSocket连接成功");

        // 发送开始参数
        yield return StartCoroutine(SendStartParams());
        
        // 将连接建立前缓存的音频数据转移到发送队列
        int transferredCount = 0;
        lock (pendingAudioDataQueue)
        {
            while (pendingAudioDataQueue.Count > 0)
            {
                byte[] cachedAudio = pendingAudioDataQueue.Dequeue();
                lock (audioDataQueue)
                {
                    audioDataQueue.Enqueue(cachedAudio);
                }
                transferredCount++;
            }
        }
        if (transferredCount > 0)
        {
            Debug.Log($"已转移 {transferredCount} 个缓存的音频块到发送队列");
        }
        
        // 开始接收消息
        receiveCoroutine = StartCoroutine(ReceiveMessages());
        
        // 开始发送音频数据（必须在START后立即开始）
        isSendingAudio = true;
        sendAudioCoroutine = StartCoroutine(SendAudioData());
        
        // 立即发送第一个音频块（避免超时）
        // 如果队列为空，发送静音数据；如果有数据，立即处理
        yield return StartCoroutine(SendFirstAudioChunk());
    }
    
    /// <summary>
    /// 发送开始参数帧
    /// </summary>
    private IEnumerator SendStartParams()
    {
        // 使用硬编码的AppID，避免配置问题
        int appid = 120396107; // 您的AppID
        string appkey = "iWPyqah9l8g6odkj6zUxlWgv"; // 正确的API Key
        
        Debug.Log($"使用AppID: {appid}");
        Debug.Log($"使用API Key: {appkey}");

        var startParams = new
        {
            type = "START",
            data = new
            {
                appid = appid, // 数字格式的appid，符合百度API要求
                appkey = appkey, // 正确的API Key
                dev_pid = 15372, // 使用15372模型（加强标点）
                cuid = SystemInfo.deviceUniqueIdentifier, // 设备唯一ID
                sample = SAMPLE_RATE, // 16000采样率
                format = "pcm", // PCM格式
                token = m_Settings.m_Token // 添加token参数，符合百度API要求
            }
        };

        string json = JsonConvert.SerializeObject(startParams);
        byte[] data = Encoding.UTF8.GetBytes(json);
        
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"发送开始参数失败: {sendTask.Exception}");
        }
        else
        {
            Debug.Log($"发送开始参数: {json}");
        }
    }
    
    /// <summary>
    /// 发送第一个音频块（在START后立即发送，避免超时）
    /// </summary>
    private IEnumerator SendFirstAudioChunk()
    {
        const int chunkLen = 16000 * 2 / 1000 * 160; // 5120 bytes
        
        // 检查队列中是否有数据
        byte[] firstChunk = null;
        lock (audioDataQueue)
        {
            if (audioDataQueue.Count > 0)
            {
                // 累积队列中的数据直到达到5120字节
                List<byte> buffer = new List<byte>(chunkLen);
                while (audioDataQueue.Count > 0 && buffer.Count < chunkLen)
                {
                    byte[] audioData = audioDataQueue.Dequeue();
                    if (audioData != null && audioData.Length > 0)
                    {
                        buffer.AddRange(audioData);
                    }
                }
                
                if (buffer.Count >= chunkLen)
                {
                    firstChunk = new byte[chunkLen];
                    buffer.CopyTo(0, firstChunk, 0, chunkLen);
                    // 将剩余数据放回队列
                    if (buffer.Count > chunkLen)
                    {
                        byte[] remaining = new byte[buffer.Count - chunkLen];
                        buffer.CopyTo(chunkLen, remaining, 0, remaining.Length);
                        audioDataQueue.Enqueue(remaining);
                    }
                }
                else if (buffer.Count > 0)
                {
                    // 如果不够5120字节，用静音填充
                    firstChunk = new byte[chunkLen];
                    buffer.CopyTo(0, firstChunk, 0, buffer.Count);
                    // 剩余部分保持为0（静音）
                }
            }
        }
        
        // 如果没有数据，发送静音数据
        if (firstChunk == null)
        {
            firstChunk = new byte[chunkLen]; // 全0，静音数据
            Debug.Log("发送第一个音频块（静音数据，避免超时）");
        }
        else
        {
            Debug.Log($"发送第一个音频块（实际数据）: {firstChunk.Length} bytes");
        }
        
        // 立即发送第一个音频块
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(firstChunk), WebSocketMessageType.Binary, true, CancellationToken.None);
        
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"发送第一个音频块失败: {sendTask.Exception}");
        }
        else
        {
            Debug.Log($"第一个音频块发送成功: {firstChunk.Length} bytes");
        }
    }
    
    /// <summary>
    /// 发送音频数据（符合百度API规范）
    /// </summary>
    private IEnumerator SendAudioData()
    {
        const int chunkMs = 160; // 160ms的录音，符合百度API推荐
        const int chunkLen = 16000 * 2 / 1000 * chunkMs; // 5120 bytes，符合百度API规范
        
        Debug.Log($"开始发送音频数据，块大小: {chunkLen} bytes");
        
        // 累积缓冲区，用于合并多个小块直到达到目标大小
        List<byte> buffer = new List<byte>(chunkLen);
        
        // 用于跟踪没有数据的时间
        float noDataTime = 0f;
        const float MAX_NO_DATA_TIME = 0.5f; // 如果0.5秒没有数据，发送静音数据避免超时
        
        // 标记是否是第一次循环（第一次不等待）
        bool isFirstLoop = true;
        
        while (isSendingAudio && isConnected)
        {
            // 从队列中取出所有可用的音频数据
            List<byte[]> audioChunks = new List<byte[]>();
            lock (audioDataQueue)
            {
                while (audioDataQueue.Count > 0)
                {
                    audioChunks.Add(audioDataQueue.Dequeue());
                }
            }

            // 将音频数据添加到缓冲区
            bool hasNewData = false;
            foreach (var audioData in audioChunks)
            {
                if (audioData != null && audioData.Length > 0)
                {
                    buffer.AddRange(audioData);
                    hasNewData = true;
                }
            }

            // 重置无数据计时器
            if (hasNewData)
            {
                noDataTime = 0f;
            }
            else
            {
                noDataTime += chunkMs / 1000.0f;
            }

            // 当缓冲区达到或超过目标大小时，发送数据
            if (buffer.Count >= chunkLen)
            {
                // 取出正好chunkLen大小的数据
                byte[] dataToSend = new byte[chunkLen];
                buffer.CopyTo(0, dataToSend, 0, chunkLen);
                
                // 移除已发送的数据
                buffer.RemoveRange(0, chunkLen);

                // 按照百度API规范发送音频数据（Binary帧）
                var sendTask = webSocket.SendAsync(new ArraySegment<byte>(dataToSend), WebSocketMessageType.Binary, true, CancellationToken.None);
                
                while (!sendTask.IsCompleted)
                {
                    yield return null;
                }

                if (sendTask.IsFaulted)
                {
                    Debug.LogError($"发送音频数据失败: {sendTask.Exception}");
                    break;
                }
                
                Debug.Log($"发送音频数据长度: {dataToSend.Length} bytes (缓冲区剩余: {buffer.Count} bytes)");
            }
            // 如果长时间没有数据，发送静音数据避免超时
            else if (noDataTime >= MAX_NO_DATA_TIME && buffer.Count == 0)
            {
                // 生成静音数据（全0）
                byte[] silenceData = new byte[chunkLen];
                
                var sendTask = webSocket.SendAsync(new ArraySegment<byte>(silenceData), WebSocketMessageType.Binary, true, CancellationToken.None);
                
                while (!sendTask.IsCompleted)
                {
                    yield return null;
                }

                if (sendTask.IsFaulted)
                {
                    Debug.LogError($"发送静音数据失败: {sendTask.Exception}");
                    break;
                }
                
                Debug.Log($"发送静音数据避免超时: {silenceData.Length} bytes");
                noDataTime = 0f; // 重置计时器
            }
            // 如果缓冲区有部分数据但不够完整块，等待更多数据
            else if (buffer.Count > 0 && buffer.Count < chunkLen)
            {
                // 如果等待时间过长，也发送现有数据（用静音填充）
                if (noDataTime >= MAX_NO_DATA_TIME)
                {
                    // 用静音数据填充到完整块大小
                    byte[] dataToSend = new byte[chunkLen];
                    buffer.CopyTo(0, dataToSend, 0, buffer.Count);
                    // 剩余部分保持为0（静音）
                    buffer.Clear();

                    var sendTask = webSocket.SendAsync(new ArraySegment<byte>(dataToSend), WebSocketMessageType.Binary, true, CancellationToken.None);
                    
                    while (!sendTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (sendTask.IsFaulted)
                    {
                        Debug.LogError($"发送音频数据失败: {sendTask.Exception}");
                        break;
                    }
                    
                    Debug.Log($"发送部分音频数据（用静音填充）: {dataToSend.Length} bytes");
                    noDataTime = 0f;
                }
            }

            // 等待160ms，符合百度API推荐间隔
            // 第一次循环不等待（因为已经发送了第一个块）
            if (isFirstLoop)
            {
                isFirstLoop = false;
                yield return null; // 只等待一帧
            }
            else
            {
                yield return new WaitForSeconds(chunkMs / 1000.0f);
            }
        }
        
        // 清理缓冲区
        buffer.Clear();
    }
    
    /// <summary>
    /// 接收消息
    /// </summary>
    private IEnumerator ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (isConnected && webSocket.State == WebSocketState.Open)
        {
            var receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            while (!receiveTask.IsCompleted)
            {
                yield return null;
            }

            if (receiveTask.IsFaulted)
            {
                Debug.LogError($"接收消息失败: {receiveTask.Exception}");
                break;
            }

            var result = receiveTask.Result;
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.Log($"收到消息: {message}");
                ProcessMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                Debug.Log("WebSocket连接关闭");
                break;
            }
        }

        // 清理连接
        if (webSocket != null)
        {
            webSocket.Dispose();
            webSocket = null;
        }
        
        isConnected = false;
        isSendingAudio = false;
    }

    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    /// <param name="message">消息内容</param>
    private void ProcessMessage(string message)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<RealTimeResponse>(message);
            
            if (response.type == "MID_TEXT")
            {
                // 临时识别结果
                string recognizedText = GetResultText(response.result);
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    Debug.Log($"临时识别结果: {recognizedText}");
                    // 可以选择是否显示临时结果
                    // realTimeCallback?.Invoke(recognizedText);
                }
            }
            else if (response.type == "FIN_TEXT")
            {
                // 最终识别结果
                if (response.err_no == "0")
                {
                    string recognizedText = GetResultText(response.result);
                    if (!string.IsNullOrEmpty(recognizedText))
                    {
                        Debug.Log($"最终识别结果: {recognizedText}");
                        realTimeCallback?.Invoke(recognizedText);
                    }
                }
                else
                {
                    Debug.LogError($"识别错误: {response.err_msg} (错误码: {response.err_no})");
                    string errorSuggestion = GetErrorSuggestion(response.err_no);
                    Debug.LogError($"解决建议: {errorSuggestion}");
                    
                    // 对于-3101错误（等待音频超时），尝试自动恢复
                    if (response.err_no == "-3101")
                    {
                        Debug.LogWarning("检测到音频超时错误(-3101)，尝试继续接收音频数据");
                        // 不调用回调，让系统继续运行，等待后续音频数据
                        // 这样可以避免错误信息被当作识别结果
                    }
                    else
                    {
                        // 其他错误才通知回调
                        realTimeCallback?.Invoke($"识别错误: {response.err_msg}");
                    }
                }
            }
            else if (response.type == "HEARTBEAT")
            {
                // 心跳帧，忽略
                Debug.Log("收到心跳帧");
            }
            else if (response.type == "ERROR")
            {
                Debug.LogError($"识别错误: {response.err_msg} (错误码: {response.err_no})");
                string errorSuggestion = GetErrorSuggestion(response.err_no);
                Debug.LogError($"解决建议: {errorSuggestion}");
                
                // 对于-3101错误（等待音频超时），尝试自动恢复
                if (response.err_no == "-3101")
                {
                    Debug.LogWarning("检测到音频超时错误(-3101)，尝试继续接收音频数据");
                    // 不调用回调，让系统继续运行，等待后续音频数据
                }
                else
                {
                    // 其他错误才通知回调
                    realTimeCallback?.Invoke($"识别错误: {response.err_msg}");
                }
            }
            else
            {
                Debug.Log($"收到其他类型消息: {response.type}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析消息异常: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取识别结果文本
    /// </summary>
    /// <param name="result">结果对象</param>
    /// <returns>识别文本</returns>
    private string GetResultText(object result)
    {
        if (result == null)
            return string.Empty;
            
        if (result is string)
        {
            return result.ToString();
        }
        else if (result is Newtonsoft.Json.Linq.JArray jArray)
        {
            return string.Join("", jArray.ToObject<string[]>());
        }
        else if (result is string[] stringArray)
        {
            return string.Join("", stringArray);
        }
        
        return result.ToString();
    }
    
    /// <summary>
    /// 根据错误码获取解决建议
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <returns>解决建议</returns>
    private string GetErrorSuggestion(string errorCode)
    {
        switch (errorCode)
        {
            case "-3101":
                return "等待音频超时：实时识别启动后没有及时收到音频数据。可能原因：1) 唤醒成功后用户未及时说话；2) 音频数据发送延迟；3) 网络延迟导致连接建立缓慢。建议：确保唤醒成功后立即开始说话，或检查网络连接。";
            case "-3008":
                return "AppID格式错误，应该是数字格式。请检查百度AI开放平台中的AppID是否正确，格式如：10512317";
            case "-3007":
                return "API Key格式错误，请检查百度AI开放平台中的API Key是否正确";
            case "-3006":
                return "API Key或Secret Key未配置，请在BaiduSettings中设置正确的API凭证";
            case "-3005":
                return "网络连接问题，请检查网络连接";
            case "-3004":
                return "认证失败，请检查API Key和Secret Key是否正确";
            case "-3003":
                return "请求参数错误，请检查参数格式";
            case "-3002":
                return "音频格式错误，请检查音频格式是否为PCM 16kHz";
            case "-3001":
                return "音频数据错误，请检查音频数据是否有效";
            default:
                return "请查看百度AI开放平台文档获取详细错误信息";
        }
    }
    
    /// <summary>
    /// 备用方案：使用语音识别API
    /// </summary>
    private IEnumerator FallbackToSpeechAPI()
    {
        Debug.Log("使用备用方案：语音识别API");
        
        // 关闭WebSocket连接
        if (webSocket != null)
        {
            try
            {
                webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fallback to API", CancellationToken.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"关闭WebSocket异常: {ex.Message}");
            }
            webSocket = null;
        }
        
        isConnected = false;
        isSendingAudio = false;
        
        // 提示用户使用正确的API Key
        Debug.LogError("实时语音识别需要数字格式的AppID");
        Debug.LogError("请按以下步骤获取正确的AppID：");
        Debug.LogError("1. 登录百度AI开放平台 (https://ai.baidu.com/)");
        Debug.LogError("2. 进入控制台 → 语音技术 → 实时语音识别");
        Debug.LogError("3. 创建应用，获取数字格式的AppID（如：10512317）");
        Debug.LogError("4. 在BaiduSettings中更新API Key为数字格式的AppID");
        Debug.LogError("5. 确保API Key字段填入的是AppID（数字），Secret Key字段填入的是API Key（字符串）");
        
        realTimeCallback?.Invoke("实时语音识别需要数字格式的AppID，请按照控制台提示获取正确的AppID");
        
        yield return null;
    }
    
    /// <summary>
    /// 发送结束帧（与Python版本一致）
    /// </summary>
    private IEnumerator SendFinishFrame()
    {
        var finishParams = new
        {
            type = "FINISH" // 与Python版本完全一致
        };

        string json = JsonConvert.SerializeObject(finishParams);
        byte[] data = Encoding.UTF8.GetBytes(json);
        
        Debug.Log($"发送结束帧: {json}");
        
        var sendTask = webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        
        while (!sendTask.IsCompleted)
        {
            yield return null;
        }

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"发送结束帧失败: {sendTask.Exception}");
        }
        else
        {
            Debug.Log("发送结束帧成功");
        }

        // 关闭连接
        if (webSocket != null)
        {
            var closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Finished", CancellationToken.None);
            while (!closeTask.IsCompleted)
            {
                yield return null;
            }
        }
    }
    
    /// <summary>
    /// 实现STT基类的SpeechToText方法（用于兼容性）
    /// </summary>
    /// <param name="_clip">音频片段</param>
    /// <param name="_callback">回调函数</param>
    public override void SpeechToText(AudioClip _clip, Action<string> _callback)
    {
        // 将AudioClip转换为字节数组
        float[] samples = new float[_clip.samples];
        _clip.GetData(samples, 0);
        var samplesShort = new short[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            samplesShort[index] = (short)(samples[index] * short.MaxValue);
        }
        byte[] audioData = new byte[samplesShort.Length * 2];
        Buffer.BlockCopy(samplesShort, 0, audioData, 0, audioData.Length);
        
        SpeechToText(audioData, _callback);
    }

    /// <summary>
    /// 实现STT基类的SpeechToText方法（字节数组版本）
    /// </summary>
    /// <param name="_audioData">音频数据</param>
    /// <param name="_callback">回调函数</param>
    public override void SpeechToText(byte[] _audioData, Action<string> _callback)
    {
        // 对于实时识别，我们直接添加音频数据到队列
        if (isConnected && isSendingAudio)
        {
            AddAudioData(_audioData);
                }
                else
                {
            // 如果未连接，启动实时识别
            StartRealTimeRecognition(_callback);
            // 延迟添加音频数据
            StartCoroutine(DelayedAddAudioData(_audioData));
        }
    }
    
    /// <summary>
    /// 延迟添加音频数据
    /// </summary>
    private IEnumerator DelayedAddAudioData(byte[] audioData)
    {
        // 等待连接建立
        while (!isConnected)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        AddAudioData(audioData);
    }
    
    private void OnDestroy()
    {
        StopRealTimeRecognition();
        CleanupWebSocket();
    }
    
    /// <summary>
    /// 清理WebSocket连接
    /// </summary>
    private void CleanupWebSocket()
    {
        if (webSocket != null)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cleanup", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"关闭WebSocket时出现异常: {ex.Message}");
            }
            finally
            {
                webSocket.Dispose();
                webSocket = null;
            }
        }
        
        isConnected = false;
        isConnecting = false;
        isSendingAudio = false;
    }
    
    #endregion

    #region 数据结构

    /// <summary>
    /// 实时识别响应数据结构
    /// </summary>
    [System.Serializable]
    public class RealTimeResponse
    {
        public string type;
        public object result; // 支持字符串或字符串数组
        public string err_msg;
        public string err_no;
    }

    #endregion
}
