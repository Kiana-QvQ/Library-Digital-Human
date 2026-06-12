using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace OllamaChat
{
    /// <summary>
    /// Ollama聊天客户端 - 用于与Python服务器通信
    /// Ollama Chat Client - For communicating with Python server
    /// </summary>
    public class OllamaChatClient : MonoBehaviour
    {
        [Header("服务器设置 Server Settings")]
        [SerializeField] private string serverUrl = "http://localhost:8080";
        [SerializeField] private float requestTimeout = 30f;
        
        [Header("UI组件 UI Components")]
        [SerializeField] private InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text responseText;
        [SerializeField] private Text statusText;
        [SerializeField] private ScrollRect chatScrollRect;
        
        [Header("调试设置 Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private bool isConnected = false;
        private Coroutine healthCheckCoroutine;
        
        // 事件
        public event Action<string> OnResponseReceived;
        public event Action<string> OnErrorOccurred;
        public event Action<bool> OnConnectionStatusChanged;
        
        private void Start()
        {
            InitializeUI();
            StartHealthCheck();
        }
        
        private void OnDestroy()
        {
            StopHealthCheck();
        }
        
        /// <summary>
        /// 初始化UI组件
        /// Initialize UI components
        /// </summary>
        private void InitializeUI()
        {
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(SendMessage);
            }
            
            if (messageInput != null)
            {
                messageInput.onEndEdit.AddListener(OnInputEndEdit);
            }
            
            UpdateStatus("正在连接服务器...", Color.yellow);
        }
        
        /// <summary>
        /// 输入框结束编辑事件
        /// Input field end edit event
        /// </summary>
        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SendMessage();
            }
        }
        
        /// <summary>
        /// 开始健康检查
        /// Start health check
        /// </summary>
        private void StartHealthCheck()
        {
            if (healthCheckCoroutine != null)
            {
                StopCoroutine(healthCheckCoroutine);
            }
            healthCheckCoroutine = StartCoroutine(HealthCheckCoroutine());
        }
        
        /// <summary>
        /// 停止健康检查
        /// Stop health check
        /// </summary>
        private void StopHealthCheck()
        {
            if (healthCheckCoroutine != null)
            {
                StopCoroutine(healthCheckCoroutine);
                healthCheckCoroutine = null;
            }
        }
        
        /// <summary>
        /// 健康检查协程
        /// Health check coroutine
        /// </summary>
        private IEnumerator HealthCheckCoroutine()
        {
            while (true)
            {
                yield return StartCoroutine(CheckServerHealth());
                yield return new WaitForSeconds(5f); // 每5秒检查一次
            }
        }
        
        /// <summary>
        /// 检查服务器健康状态
        /// Check server health status
        /// </summary>
        private IEnumerator CheckServerHealth()
        {
            string url = $"{serverUrl}/health";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        HealthResponse healthResponse = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                        bool wasConnected = isConnected;
                        isConnected = healthResponse.status == "healthy";
                        
                        if (wasConnected != isConnected)
                        {
                            OnConnectionStatusChanged?.Invoke(isConnected);
                        }
                        
                        if (isConnected)
                        {
                            UpdateStatus("服务器连接正常", Color.green);
                        }
                        else
                        {
                            UpdateStatus("服务器状态异常", Color.red);
                        }
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"健康检查: {healthResponse.status}, Ollama可用: {healthResponse.ollama_available}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"解析健康检查响应失败: {e.Message}");
                        SetConnectionStatus(false);
                    }
                }
                else
                {
                    SetConnectionStatus(false);
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"健康检查失败: {request.error}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 设置连接状态
        /// Set connection status
        /// </summary>
        private void SetConnectionStatus(bool connected)
        {
            bool wasConnected = isConnected;
            isConnected = connected;
            
            if (wasConnected != isConnected)
            {
                OnConnectionStatusChanged?.Invoke(isConnected);
            }
            
            if (!isConnected)
            {
                UpdateStatus("服务器连接失败", Color.red);
            }
        }
        
        /// <summary>
        /// 发送消息
        /// Send message
        /// </summary>
        public void SendMessage()
        {
            if (messageInput == null || string.IsNullOrEmpty(messageInput.text.Trim()))
            {
                Debug.LogWarning("消息不能为空");
                return;
            }
            
            if (!isConnected)
            {
                UpdateStatus("服务器未连接，无法发送消息", Color.red);
                return;
            }
            
            string message = messageInput.text.Trim();
            messageInput.text = "";
            
            StartCoroutine(SendChatMessage(message));
        }
        
        /// <summary>
        /// 发送聊天消息协程
        /// Send chat message coroutine
        /// </summary>
        private IEnumerator SendChatMessage(string message)
        {
            UpdateStatus("正在发送消息...", Color.yellow);
            
            // 显示用户消息
            AppendToChat($"用户: {message}", Color.blue);
            
            string url = $"{serverUrl}/chat";
            
            // 创建请求数据
            ChatRequest requestData = new ChatRequest
            {
                message = message,
                system_prompt = null
            };
            
            string jsonData = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = (int)requestTimeout;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        ChatResponse response = JsonUtility.FromJson<ChatResponse>(request.downloadHandler.text);
                        
                        // 显示AI回复
                        AppendToChat($"AI: {response.response}", Color.green);
                        
                        OnResponseReceived?.Invoke(response.response);
                        UpdateStatus("消息发送成功", Color.green);
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"收到回复: {response.response}");
                        }
                    }
                    catch (Exception e)
                    {
                        string errorMsg = $"解析响应失败: {e.Message}";
                        Debug.LogError(errorMsg);
                        AppendToChat($"错误: {errorMsg}", Color.red);
                        OnErrorOccurred?.Invoke(errorMsg);
                    }
                }
                else
                {
                    string errorMsg = $"请求失败: {request.error}";
                    Debug.LogError(errorMsg);
                    AppendToChat($"错误: {errorMsg}", Color.red);
                    OnErrorOccurred?.Invoke(errorMsg);
                    UpdateStatus("消息发送失败", Color.red);
                }
            }
        }
        
        /// <summary>
        /// 添加消息到聊天显示
        /// Add message to chat display
        /// </summary>
        private void AppendToChat(string message, Color color)
        {
            if (responseText != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string coloredMessage = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>[{timestamp}] {message}</color>\n";
                responseText.text += coloredMessage;
                
                // 滚动到底部
                if (chatScrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    chatScrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }
        
        /// <summary>
        /// 更新状态显示
        /// Update status display
        /// </summary>
        private void UpdateStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"状态: {message}");
            }
        }
        
        /// <summary>
        /// 清空聊天记录
        /// Clear chat history
        /// </summary>
        public void ClearChat()
        {
            if (responseText != null)
            {
                responseText.text = "";
            }
        }
        
        /// <summary>
        /// 设置服务器URL
        /// Set server URL
        /// </summary>
        public void SetServerUrl(string url)
        {
            serverUrl = url;
            Debug.Log($"服务器URL已更新为: {serverUrl}");
        }
        
        /// <summary>
        /// 获取连接状态
        /// Get connection status
        /// </summary>
        public bool IsConnected()
        {
            return isConnected;
        }
    }
    
    // 数据类定义
    [Serializable]
    public class ChatRequest
    {
        public string message;
        public string system_prompt;
    }
    
    [Serializable]
    public class ChatResponse
    {
        public string response;
        public string user_message;
    }
    
    [Serializable]
    public class HealthResponse
    {
        public string status;
        public bool ollama_available;
    }
}
