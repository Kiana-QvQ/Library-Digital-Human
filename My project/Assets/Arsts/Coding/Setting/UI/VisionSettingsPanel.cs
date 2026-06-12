using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 视觉设置面板
/// </summary>
public class VisionSettingsPanel : MonoBehaviour
{
    [Header("GLM API配置")]
    [SerializeField] private TMP_InputField glmApiKeyInput;
    [SerializeField] private TMP_InputField glmApiBaseUrlInput;
    [SerializeField] private TMP_InputField glmModelInput;
    
    [Header("视觉分析设置")]
    [SerializeField] private Toggle enableAutoAnalyzeToggle;
    [SerializeField] private Slider analyzeIntervalSlider;
    [SerializeField] private TextMeshProUGUI analyzeIntervalText;
    [SerializeField] private Slider maxImageWidthSlider;
    [SerializeField] private TextMeshProUGUI maxImageWidthText;
    [SerializeField] private Slider maxImageHeightSlider;
    [SerializeField] private TextMeshProUGUI maxImageHeightText;
    [SerializeField] private Slider imageQualitySlider;
    [SerializeField] private TextMeshProUGUI imageQualityText;
    [SerializeField] private Toggle enableThinkingToggle;
    
    [Header("视觉分析提示词")]
    [SerializeField] private TMP_InputField defaultPromptInput;
    
    [Header("与聊天系统集成")]
    [SerializeField] private Toggle autoAttachVisionToggle;
    [SerializeField] private TMP_Dropdown visionTriggerKeyDropdown;
    
    [Header("测试按钮")]
    [SerializeField] private Button testVisionButton;
    [SerializeField] private Button resetVisionButton;
    
    private VisionSettings currentSettings;
    
    private void Start()
    {
        InitializeUI();
        LoadSettings();
        SetupEventListeners();
    }
    
    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        // 初始化分析间隔滑块
        if (analyzeIntervalSlider != null)
        {
            analyzeIntervalSlider.minValue = 1f;
            analyzeIntervalSlider.maxValue = 60f;
        }
        
        // 初始化图像尺寸滑块
        if (maxImageWidthSlider != null)
        {
            maxImageWidthSlider.minValue = 256f;
            maxImageWidthSlider.maxValue = 2048f;
        }
        
        if (maxImageHeightSlider != null)
        {
            maxImageHeightSlider.minValue = 256f;
            maxImageHeightSlider.maxValue = 2048f;
        }
        
        // 初始化图像质量滑块
        if (imageQualitySlider != null)
        {
            imageQualitySlider.minValue = 0f;
            imageQualitySlider.maxValue = 100f;
        }
        
        // 初始化触发按键选项
        if (visionTriggerKeyDropdown != null)
        {
            visionTriggerKeyDropdown.ClearOptions();
            visionTriggerKeyDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "V键",
                "F键",
                "G键",
                "H键",
                "空格键"
            });
        }
    }
    
    /// <summary>
    /// 设置事件监听
    /// </summary>
    private void SetupEventListeners()
    {
        // GLM API配置事件
        if (glmApiKeyInput != null)
            glmApiKeyInput.onEndEdit.AddListener(OnGLMApiKeyChanged);
        
        if (glmApiBaseUrlInput != null)
            glmApiBaseUrlInput.onEndEdit.AddListener(OnGLMApiBaseUrlChanged);
        
        if (glmModelInput != null)
            glmModelInput.onEndEdit.AddListener(OnGLMModelChanged);
        
        // 视觉分析设置事件
        if (enableAutoAnalyzeToggle != null)
            enableAutoAnalyzeToggle.onValueChanged.AddListener(OnEnableAutoAnalyzeChanged);
        
        if (analyzeIntervalSlider != null)
            analyzeIntervalSlider.onValueChanged.AddListener(OnAnalyzeIntervalChanged);
        
        if (maxImageWidthSlider != null)
            maxImageWidthSlider.onValueChanged.AddListener(OnMaxImageWidthChanged);
        
        if (maxImageHeightSlider != null)
            maxImageHeightSlider.onValueChanged.AddListener(OnMaxImageHeightChanged);
        
        if (imageQualitySlider != null)
            imageQualitySlider.onValueChanged.AddListener(OnImageQualityChanged);
        
        if (enableThinkingToggle != null)
            enableThinkingToggle.onValueChanged.AddListener(OnEnableThinkingChanged);
        
        // 提示词事件
        if (defaultPromptInput != null)
            defaultPromptInput.onEndEdit.AddListener(OnDefaultPromptChanged);
        
        // 集成设置事件
        if (autoAttachVisionToggle != null)
            autoAttachVisionToggle.onValueChanged.AddListener(OnAutoAttachVisionChanged);
        
        if (visionTriggerKeyDropdown != null)
            visionTriggerKeyDropdown.onValueChanged.AddListener(OnVisionTriggerKeyChanged);
        
        // 按钮事件
        if (testVisionButton != null)
            testVisionButton.onClick.AddListener(TestVision);
        
        if (resetVisionButton != null)
            resetVisionButton.onClick.AddListener(ResetVisionSettings);
        
        // 监听设置变更事件
        SettingsManager.OnVisionSettingsChanged += OnVisionSettingsChanged;
    }
    
    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        if (SettingsManager.Instance != null)
        {
            currentSettings = SettingsManager.Instance.GetVisionSettings();
            UpdateUI();
        }
    }
    
    /// <summary>
    /// 更新UI显示
    /// </summary>
    private void UpdateUI()
    {
        // 更新GLM API配置
        if (glmApiKeyInput != null)
            glmApiKeyInput.text = currentSettings.glmApiKey;
        
        if (glmApiBaseUrlInput != null)
            glmApiBaseUrlInput.text = currentSettings.glmApiBaseUrl;
        
        if (glmModelInput != null)
            glmModelInput.text = currentSettings.glmModel;
        
        // 更新视觉分析设置
        if (enableAutoAnalyzeToggle != null)
            enableAutoAnalyzeToggle.isOn = currentSettings.enableAutoAnalyze;
        
        if (analyzeIntervalSlider != null)
            analyzeIntervalSlider.value = currentSettings.analyzeInterval;
        
        if (maxImageWidthSlider != null)
            maxImageWidthSlider.value = currentSettings.maxImageWidth;
        
        if (maxImageHeightSlider != null)
            maxImageHeightSlider.value = currentSettings.maxImageHeight;
        
        if (imageQualitySlider != null)
            imageQualitySlider.value = currentSettings.imageQuality;
        
        if (enableThinkingToggle != null)
            enableThinkingToggle.isOn = currentSettings.enableThinking;
        
        // 更新提示词
        if (defaultPromptInput != null)
            defaultPromptInput.text = currentSettings.defaultPrompt;
        
        // 更新集成设置
        if (autoAttachVisionToggle != null)
            autoAttachVisionToggle.isOn = currentSettings.autoAttachVision;
        
        if (visionTriggerKeyDropdown != null)
        {
            int keyIndex = GetKeyIndex(currentSettings.visionTriggerKey);
            visionTriggerKeyDropdown.value = keyIndex;
        }
        
        // 更新文本显示
        UpdateTexts();
    }
    
    /// <summary>
    /// 更新文本显示
    /// </summary>
    private void UpdateTexts()
    {
        if (analyzeIntervalText != null)
            analyzeIntervalText.text = $"{currentSettings.analyzeInterval:F1}秒";
        
        if (maxImageWidthText != null)
            maxImageWidthText.text = $"{Mathf.RoundToInt(currentSettings.maxImageWidth)}px";
        
        if (maxImageHeightText != null)
            maxImageHeightText.text = $"{Mathf.RoundToInt(currentSettings.maxImageHeight)}px";
        
        if (imageQualityText != null)
            imageQualityText.text = $"{Mathf.RoundToInt(currentSettings.imageQuality)}%";
    }
    
    /// <summary>
    /// 获取按键索引
    /// </summary>
    private int GetKeyIndex(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.V: return 0;
            case KeyCode.F: return 1;
            case KeyCode.G: return 2;
            case KeyCode.H: return 3;
            case KeyCode.Space: return 4;
            default: return 0;
        }
    }
    
    /// <summary>
    /// 获取按键值
    /// </summary>
    private KeyCode GetKeyValue(int index)
    {
        switch (index)
        {
            case 0: return KeyCode.V;
            case 1: return KeyCode.F;
            case 2: return KeyCode.G;
            case 3: return KeyCode.H;
            case 4: return KeyCode.Space;
            default: return KeyCode.V;
        }
    }
    
    // 事件处理方法
    private void OnGLMApiKeyChanged(string value)
    {
        currentSettings.glmApiKey = value;
        ApplySettings();
    }
    
    private void OnGLMApiBaseUrlChanged(string value)
    {
        currentSettings.glmApiBaseUrl = value;
        ApplySettings();
    }
    
    private void OnGLMModelChanged(string value)
    {
        currentSettings.glmModel = value;
        ApplySettings();
    }
    
    private void OnEnableAutoAnalyzeChanged(bool value)
    {
        currentSettings.enableAutoAnalyze = value;
        ApplySettings();
    }
    
    private void OnAnalyzeIntervalChanged(float value)
    {
        currentSettings.analyzeInterval = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnMaxImageWidthChanged(float value)
    {
        currentSettings.maxImageWidth = Mathf.RoundToInt(value);
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnMaxImageHeightChanged(float value)
    {
        currentSettings.maxImageHeight = Mathf.RoundToInt(value);
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnImageQualityChanged(float value)
    {
        currentSettings.imageQuality = Mathf.RoundToInt(value);
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnEnableThinkingChanged(bool value)
    {
        currentSettings.enableThinking = value;
        ApplySettings();
    }
    
    private void OnDefaultPromptChanged(string value)
    {
        currentSettings.defaultPrompt = value;
        ApplySettings();
    }
    
    private void OnAutoAttachVisionChanged(bool value)
    {
        currentSettings.autoAttachVision = value;
        ApplySettings();
    }
    
    private void OnVisionTriggerKeyChanged(int index)
    {
        currentSettings.visionTriggerKey = GetKeyValue(index);
        ApplySettings();
    }
    
    /// <summary>
    /// 测试视觉分析
    /// </summary>
    private void TestVision()
    {
        Debug.Log("测试视觉分析功能");
        GLMVisionClient visionClient = FindObjectOfType<GLMVisionClient>();
        if (visionClient != null)
        {
            visionClient.AnalyzeCurrentFrame(currentSettings.defaultPrompt);
            Debug.Log("视觉分析请求已发送");
        }
        else
        {
            Debug.LogWarning("未找到 GLMVisionClient 组件");
        }
    }
    
    /// <summary>
    /// 重置视觉设置
    /// </summary>
    private void ResetVisionSettings()
    {
        currentSettings = new VisionSettings();
        UpdateUI();
        ApplySettings();
    }
    
    /// <summary>
    /// 应用设置
    /// </summary>
    private void ApplySettings()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.UpdateVisionSettings(currentSettings);
        }
    }
    
    /// <summary>
    /// 视觉设置变更回调
    /// </summary>
    private void OnVisionSettingsChanged(VisionSettings newSettings)
    {
        currentSettings = newSettings;
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        // 取消事件监听
        SettingsManager.OnVisionSettingsChanged -= OnVisionSettingsChanged;
    }
}

