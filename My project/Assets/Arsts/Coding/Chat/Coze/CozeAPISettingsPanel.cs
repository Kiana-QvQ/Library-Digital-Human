using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Coze API设置面板
/// 用于修改和测试Coze API Key
/// </summary>
public class CozeAPISettingsPanel : MonoBehaviour
{
    [Header("UI组件")]
    [Tooltip("API设置面板（主面板）")]
    [SerializeField] private GameObject apiSettingsPanel;
    
    [Tooltip("关闭按钮")]
    [SerializeField] private Button closeButton;
    
    [Tooltip("API输入框")]
    [SerializeField] private TMP_InputField apiInputField;
    
    [Tooltip("确认按钮（自动测试并保存）")]
    [SerializeField] private Button confirmButton;
    
    [Tooltip("状态提示文本")]
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Tooltip("当前API显示文本（只读，显示当前使用的API）")]
    [SerializeField] private TextMeshProUGUI currentAPIText;
    
    [Header("设置按钮引用")]
    [Tooltip("打开API设置界面的按钮（在设置面板中）")]
    [SerializeField] private Button openAPIButton;

    [Header("Coze客户端引用")]
    [Tooltip("CozeAgentClient组件引用")]
    [SerializeField] private CozeAgentClient cozeClient;

    [Tooltip("Coze配置设置（统一管理API Key和音色）")]
    [SerializeField] private CozeSettings cozeSettings;
    
    [Tooltip("Coze语音API（用于查询音色列表）")]
    [SerializeField] private CozeVoiceAPI voiceAPI;

    // 保存原始API Key（用于测试失败时恢复）
    private string originalAPIKey = "";
    
    // 是否正在测试
    private bool isTesting = false;

    private void Awake()
    {
        // 默认隐藏面板
        if (apiSettingsPanel != null)
        {
            apiSettingsPanel.SetActive(false);
        }
    }

    private void Start()
    {
        InitializeUI();
        LoadCurrentAPI();
    }

    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        // 如果没有手动指定，尝试自动查找组件
        if (cozeSettings == null)
        {
            // 方式1：通过APIManager获取
            APIManager apiManager = APIManager.Instance;
            if (apiManager != null)
                cozeSettings = apiManager.GetCozeSettings();
            
            // 方式2：直接查找
            if (cozeSettings == null)
                cozeSettings = FindObjectOfType<CozeSettings>();
        }
        
        if (cozeClient == null)
        {
            cozeClient = FindObjectOfType<CozeAgentClient>();
        }
        
        if (voiceAPI == null)
        {
            voiceAPI = FindObjectOfType<CozeVoiceAPI>();
        }

        // 设置按钮事件
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        }

        // 设置按钮（打开API设置界面）
        if (openAPIButton != null)
        {
            openAPIButton.onClick.AddListener(OpenPanel);
        }

        // 初始化状态文本
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    /// <summary>
    /// 加载当前API Key（只显示部分，隐藏敏感信息）
    /// </summary>
    private void LoadCurrentAPI()
    {
        string currentAPI = "";
        
        // 优先从Settings获取
        if (cozeSettings != null)
        {
            currentAPI = cozeSettings.GetAPIKey();
        }
        else if (cozeClient != null)
        {
            currentAPI = cozeClient.GetCurrentAPIKey();
        }
        
        if (!string.IsNullOrEmpty(currentAPI))
        {
            originalAPIKey = currentAPI;
            
            // 显示部分API（前4位和后4位，中间用*代替）
            if (!string.IsNullOrEmpty(currentAPI) && currentAPI.Length > 8)
            {
                string maskedAPI = currentAPI.Substring(0, 4) + "****" + currentAPI.Substring(currentAPI.Length - 4);
                if (currentAPIText != null)
                {
                    currentAPIText.text = $"当前API: {maskedAPI}";
                }
            }
            else if (!string.IsNullOrEmpty(currentAPI))
            {
                if (currentAPIText != null)
                {
                    currentAPIText.text = $"当前API: {currentAPI}";
                }
            }
            else
            {
                if (currentAPIText != null)
                {
                    currentAPIText.text = "当前API: 未设置";
                }
            }
        }
    }

    /// <summary>
    /// 打开API设置面板
    /// </summary>
    public void OpenPanel()
    {
        if (apiSettingsPanel != null)
        {
            apiSettingsPanel.SetActive(true);
            LoadCurrentAPI();
            
            // 清空输入框和状态
            if (apiInputField != null)
            {
                apiInputField.text = "";
            }
            
            if (statusText != null)
            {
                statusText.text = "";
            }
        }
    }

    /// <summary>
    /// 关闭API设置面板
    /// </summary>
    public void ClosePanel()
    {
        if (apiSettingsPanel != null)
        {
            apiSettingsPanel.SetActive(false);
        }
        
        // 清空输入框和状态
        if (apiInputField != null)
        {
            apiInputField.text = "";
        }
        
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    /// <summary>
    /// 确认按钮点击事件（自动测试，测试成功后保存）
    /// </summary>
    private void OnConfirmButtonClicked()
    {
        if (isTesting)
        {
            ShowStatus("正在测试中，请稍候...", Color.yellow);
            return;
        }

        string newAPI = apiInputField?.text?.Trim();
        
        if (string.IsNullOrEmpty(newAPI))
        {
            ShowStatus("请输入API Key", Color.red);
            return;
        }

        // 自动测试API，测试成功后才保存
        StartCoroutine(TestAndSaveAPI(newAPI));
    }

    /// <summary>
    /// 测试并保存API Key（自动测试，测试成功后才保存）
    /// </summary>
    /// <param name="newAPIKey">新的API Key</param>
    private IEnumerator TestAndSaveAPI(string newAPIKey)
    {
        isTesting = true;
        
        // 禁用按钮和输入框
        SetButtonsInteractable(false);
        
        ShowStatus("正在测试API Key...", Color.yellow);

        if (cozeClient == null)
        {
            ShowStatus("错误：未找到CozeAgentClient组件", Color.red);
            isTesting = false;
            SetButtonsInteractable(true);
            yield break;
        }

        // 调用CozeClient的测试方法
        bool testResult = false;
        string errorMessage = "";
        
        yield return StartCoroutine(cozeClient.TestAPIKey(newAPIKey, (success, message) =>
        {
            testResult = success;
            errorMessage = message;
        }));

        if (testResult)
        {
            // 测试成功，更新API Key
            ShowStatus("API Key测试成功，正在保存...", Color.green);
            
            // 优先使用Settings更新
            if (cozeSettings != null)
            {
                cozeSettings.SetAPIKey(newAPIKey);
            }
            else if (cozeClient != null)
            {
            cozeClient.UpdateAPIKey(newAPIKey);
            }
            
            originalAPIKey = newAPIKey;
            LoadCurrentAPI();
            
            ShowStatus("API Key已更新并保存", Color.green);
            
            // 延迟关闭面板
            yield return new WaitForSeconds(1.5f);
            ClosePanel();
        }
        else
        {
            // 测试失败，不更新API Key，保持原API不变
            ShowStatus($"API Key测试失败: {errorMessage}，未更新API", Color.red);
        }

        isTesting = false;
        SetButtonsInteractable(true);
    }

    /// <summary>
    /// 设置按钮和输入框交互状态
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (confirmButton != null)
        {
            confirmButton.interactable = interactable;
        }
        
        if (apiInputField != null)
        {
            apiInputField.interactable = interactable;
        }
    }

    /// <summary>
    /// 显示状态信息
    /// </summary>
    private void ShowStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"[CozeAPISettings] {message}");
    }

    /// <summary>
    /// 公共方法：从外部打开面板（供设置按钮调用）
    /// </summary>
    public void ShowAPISettings()
    {
        OpenPanel();
    }
}

