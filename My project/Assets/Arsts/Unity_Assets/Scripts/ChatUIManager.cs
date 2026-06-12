using UnityEngine;
using UnityEngine.UI;

namespace OllamaChat
{
    /// <summary>
    /// 聊天UI管理器 - 管理聊天界面的UI元素
    /// Chat UI Manager - Manages UI elements for chat interface
    /// </summary>
    public class ChatUIManager : MonoBehaviour
    {
        [Header("UI组件 UI Components")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private InputField messageInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text responseText;
        [SerializeField] private Text statusText;
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button toggleButton;
        
        [Header("设置面板 Settings Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private InputField serverUrlInput;
        [SerializeField] private Button saveSettingsButton;
        [SerializeField] private Button closeSettingsButton;
        
        [Header("连接状态指示 Connection Status Indicator")]
        [SerializeField] private Image connectionIndicator;
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color disconnectedColor = Color.red;
        [SerializeField] private Color connectingColor = Color.yellow;
        
        private OllamaChatClient chatClient;
        private bool isSettingsOpen = false;
        
        private void Start()
        {
            InitializeUI();
            SetupChatClient();
        }
        
        /// <summary>
        /// 初始化UI
        /// Initialize UI
        /// </summary>
        private void InitializeUI()
        {
            // 设置按钮事件
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendButtonClicked);
            }
            
            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearButtonClicked);
            }
            
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(OnToggleSettingsClicked);
            }
            
            if (saveSettingsButton != null)
            {
                saveSettingsButton.onClick.AddListener(OnSaveSettingsClicked);
            }
            
            if (closeSettingsButton != null)
            {
                closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);
            }
            
            // 初始化设置面板
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
            
            // 初始化服务器URL输入框
            if (serverUrlInput != null)
            {
                serverUrlInput.text = "http://localhost:8080";
            }
            
            // 初始化连接状态指示器
            UpdateConnectionIndicator(connectingColor);
        }
        
        /// <summary>
        /// 设置聊天客户端
        /// Setup chat client
        /// </summary>
        private void SetupChatClient()
        {
            chatClient = GetComponent<OllamaChatClient>();
            if (chatClient == null)
            {
                chatClient = gameObject.AddComponent<OllamaChatClient>();
            }
            
            // 设置UI组件引用
            if (chatClient != null)
            {
                // 使用反射设置私有字段（在开发环境中）
                var messageInputField = typeof(OllamaChatClient).GetField("messageInput", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sendButtonField = typeof(OllamaChatClient).GetField("sendButton", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var responseTextField = typeof(OllamaChatClient).GetField("responseText", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var statusTextField = typeof(OllamaChatClient).GetField("statusText", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var chatScrollRectField = typeof(OllamaChatClient).GetField("chatScrollRect", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                messageInputField?.SetValue(chatClient, messageInput);
                sendButtonField?.SetValue(chatClient, sendButton);
                responseTextField?.SetValue(chatClient, responseText);
                statusTextField?.SetValue(chatClient, statusText);
                chatScrollRectField?.SetValue(chatClient, chatScrollRect);
                
                // 订阅事件
                chatClient.OnConnectionStatusChanged += OnConnectionStatusChanged;
                chatClient.OnResponseReceived += OnResponseReceived;
                chatClient.OnErrorOccurred += OnErrorOccurred;
            }
        }
        
        /// <summary>
        /// 发送按钮点击事件
        /// Send button click event
        /// </summary>
        private void OnSendButtonClicked()
        {
            if (chatClient != null)
            {
                chatClient.SendMessage();
            }
        }
        
        /// <summary>
        /// 清空按钮点击事件
        /// Clear button click event
        /// </summary>
        private void OnClearButtonClicked()
        {
            if (chatClient != null)
            {
                chatClient.ClearChat();
            }
        }
        
        /// <summary>
        /// 切换设置面板
        /// Toggle settings panel
        /// </summary>
        private void OnToggleSettingsClicked()
        {
            isSettingsOpen = !isSettingsOpen;
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(isSettingsOpen);
            }
        }
        
        /// <summary>
        /// 保存设置
        /// Save settings
        /// </summary>
        private void OnSaveSettingsClicked()
        {
            if (chatClient != null && serverUrlInput != null)
            {
                chatClient.SetServerUrl(serverUrlInput.text);
                OnCloseSettingsClicked();
            }
        }
        
        /// <summary>
        /// 关闭设置面板
        /// Close settings panel
        /// </summary>
        private void OnCloseSettingsClicked()
        {
            isSettingsOpen = false;
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// 连接状态改变事件
        /// Connection status changed event
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            UpdateConnectionIndicator(isConnected ? connectedColor : disconnectedColor);
        }
        
        /// <summary>
        /// 收到回复事件
        /// Response received event
        /// </summary>
        private void OnResponseReceived(string response)
        {
            // 可以在这里添加额外的UI更新逻辑
            Debug.Log($"收到AI回复: {response}");
        }
        
        /// <summary>
        /// 错误发生事件
        /// Error occurred event
        /// </summary>
        private void OnErrorOccurred(string error)
        {
            // 可以在这里添加错误处理逻辑
            Debug.LogError($"聊天错误: {error}");
        }
        
        /// <summary>
        /// 更新连接状态指示器
        /// Update connection status indicator
        /// </summary>
        private void UpdateConnectionIndicator(Color color)
        {
            if (connectionIndicator != null)
            {
                connectionIndicator.color = color;
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅事件
            if (chatClient != null)
            {
                chatClient.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                chatClient.OnResponseReceived -= OnResponseReceived;
                chatClient.OnErrorOccurred -= OnErrorOccurred;
            }
        }
    }
}
