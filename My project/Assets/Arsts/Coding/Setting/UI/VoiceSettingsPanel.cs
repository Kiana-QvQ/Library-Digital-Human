using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 语音设置面板
/// </summary>
public class VoiceSettingsPanel : MonoBehaviour
{
    [Header("唤醒词设置")]
    [SerializeField] private TMP_InputField wakeWordInput;
    [SerializeField] private Slider wakeWordConfidenceSlider;
    [SerializeField] private TextMeshProUGUI wakeWordConfidenceText;
    [SerializeField] private Toggle enableWakeWordToggle;
    
    [Header("语音识别设置")]
    [SerializeField] private TMP_Dropdown speechLanguageDropdown;
    [SerializeField] private Slider speechTimeoutSlider;
    [SerializeField] private TextMeshProUGUI speechTimeoutText;
    [SerializeField] private Toggle enableRealTimeRecognitionToggle;
    
    [Header("TTS设置")]
    [SerializeField] private TMP_Dropdown ttsVoiceDropdown;
    [SerializeField] private Slider ttsSpeedSlider;
    [SerializeField] private TextMeshProUGUI ttsSpeedText;
    [SerializeField] private Slider ttsPitchSlider;
    [SerializeField] private TextMeshProUGUI ttsPitchText;
    [SerializeField] private Toggle enableStreamTTSToggle;
    [SerializeField] private Slider ttsBufferTimeSlider;
    [SerializeField] private TextMeshProUGUI ttsBufferTimeText;
    [SerializeField] private Slider minTextLengthSlider;
    [SerializeField] private TextMeshProUGUI minTextLengthText;
    
    [Header("测试按钮")]
    [SerializeField] private Button testWakeWordButton;
    [SerializeField] private Button testTTSButton;
    [SerializeField] private Button resetVoiceButton;
    
    private VoiceSettings currentSettings;
    
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
        // 初始化唤醒词置信度滑块
        if (wakeWordConfidenceSlider != null)
        {
            wakeWordConfidenceSlider.minValue = 0f;
            wakeWordConfidenceSlider.maxValue = 1f;
        }
        
        // 初始化语音超时滑块
        if (speechTimeoutSlider != null)
        {
            speechTimeoutSlider.minValue = 1f;
            speechTimeoutSlider.maxValue = 10f;
        }
        
        // 初始化TTS速度滑块
        if (ttsSpeedSlider != null)
        {
            ttsSpeedSlider.minValue = 0.5f;
            ttsSpeedSlider.maxValue = 2f;
        }
        
        // 初始化TTS音调滑块
        if (ttsPitchSlider != null)
        {
            ttsPitchSlider.minValue = 0.5f;
            ttsPitchSlider.maxValue = 2f;
        }
        
        // 初始化TTS缓冲时间滑块
        if (ttsBufferTimeSlider != null)
        {
            ttsBufferTimeSlider.minValue = 0.1f;
            ttsBufferTimeSlider.maxValue = 2f;
        }
        
        // 初始化最小文本长度滑块
        if (minTextLengthSlider != null)
        {
            minTextLengthSlider.minValue = 1f;
            minTextLengthSlider.maxValue = 20f;
        }
        
        // 初始化语言选项
        if (speechLanguageDropdown != null)
        {
            speechLanguageDropdown.ClearOptions();
            speechLanguageDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "中文 (zh-CN)",
                "英文 (en-US)",
                "日文 (ja-JP)",
                "韩文 (ko-KR)"
            });
        }
        
        // 初始化TTS语音选项
        if (ttsVoiceDropdown != null)
        {
            ttsVoiceDropdown.ClearOptions();
            ttsVoiceDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "小燕 (xiaoyan)",
                "小宇 (xiaoyu)",
                "小研 (xiaoyan)",
                "小琪 (xiaoqi)",
                "小峰 (xiaofeng)",
                "小美 (xiaomei)"
            });
        }
    }
    
    /// <summary>
    /// 设置事件监听
    /// </summary>
    private void SetupEventListeners()
    {
        // 唤醒词设置事件
        if (wakeWordInput != null)
            wakeWordInput.onEndEdit.AddListener(OnWakeWordChanged);
        
        if (wakeWordConfidenceSlider != null)
            wakeWordConfidenceSlider.onValueChanged.AddListener(OnWakeWordConfidenceChanged);
        
        if (enableWakeWordToggle != null)
            enableWakeWordToggle.onValueChanged.AddListener(OnEnableWakeWordChanged);
        
        // 语音识别设置事件
        if (speechLanguageDropdown != null)
            speechLanguageDropdown.onValueChanged.AddListener(OnSpeechLanguageChanged);
        
        if (speechTimeoutSlider != null)
            speechTimeoutSlider.onValueChanged.AddListener(OnSpeechTimeoutChanged);
        
        if (enableRealTimeRecognitionToggle != null)
            enableRealTimeRecognitionToggle.onValueChanged.AddListener(OnEnableRealTimeRecognitionChanged);
        
        // TTS设置事件
        if (ttsVoiceDropdown != null)
            ttsVoiceDropdown.onValueChanged.AddListener(OnTTSVoiceChanged);
        
        if (ttsSpeedSlider != null)
            ttsSpeedSlider.onValueChanged.AddListener(OnTTSSpeedChanged);
        
        if (ttsPitchSlider != null)
            ttsPitchSlider.onValueChanged.AddListener(OnTTSPitchChanged);
        
        if (enableStreamTTSToggle != null)
            enableStreamTTSToggle.onValueChanged.AddListener(OnEnableStreamTTSChanged);
        
        if (ttsBufferTimeSlider != null)
            ttsBufferTimeSlider.onValueChanged.AddListener(OnTTSBufferTimeChanged);
        
        if (minTextLengthSlider != null)
            minTextLengthSlider.onValueChanged.AddListener(OnMinTextLengthChanged);
        
        // 按钮事件
        if (testWakeWordButton != null)
            testWakeWordButton.onClick.AddListener(TestWakeWord);
        
        if (testTTSButton != null)
            testTTSButton.onClick.AddListener(TestTTS);
        
        if (resetVoiceButton != null)
            resetVoiceButton.onClick.AddListener(ResetVoiceSettings);
        
        // 监听设置变更事件
        SettingsManager.OnVoiceSettingsChanged += OnVoiceSettingsChanged;
    }
    
    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        if (SettingsManager.Instance != null)
        {
            currentSettings = SettingsManager.Instance.GetVoiceSettings();
            UpdateUI();
        }
    }
    
    /// <summary>
    /// 更新UI显示
    /// </summary>
    private void UpdateUI()
    {
        // 更新唤醒词设置
        if (wakeWordInput != null)
            wakeWordInput.text = currentSettings.wakeWord;
        
        if (wakeWordConfidenceSlider != null)
            wakeWordConfidenceSlider.value = currentSettings.wakeWordConfidence;
        
        if (enableWakeWordToggle != null)
            enableWakeWordToggle.isOn = currentSettings.enableWakeWord;
        
        // 更新语音识别设置
        if (speechLanguageDropdown != null)
        {
            int languageIndex = GetLanguageIndex(currentSettings.speechLanguage);
            speechLanguageDropdown.value = languageIndex;
        }
        
        if (speechTimeoutSlider != null)
            speechTimeoutSlider.value = currentSettings.speechTimeout;
        
        if (enableRealTimeRecognitionToggle != null)
            enableRealTimeRecognitionToggle.isOn = currentSettings.enableRealTimeRecognition;
        
        // 更新TTS设置
        if (ttsVoiceDropdown != null)
        {
            int voiceIndex = GetVoiceIndex(currentSettings.ttsVoice);
            ttsVoiceDropdown.value = voiceIndex;
        }
        
        if (ttsSpeedSlider != null)
            ttsSpeedSlider.value = currentSettings.ttsSpeed;
        
        if (ttsPitchSlider != null)
            ttsPitchSlider.value = currentSettings.ttsPitch;
        
        if (enableStreamTTSToggle != null)
            enableStreamTTSToggle.isOn = currentSettings.enableStreamTTS;
        
        if (ttsBufferTimeSlider != null)
            ttsBufferTimeSlider.value = currentSettings.ttsBufferTime;
        
        if (minTextLengthSlider != null)
            minTextLengthSlider.value = currentSettings.minTextLength;
        
        // 更新文本显示
        UpdateTexts();
    }
    
    /// <summary>
    /// 更新文本显示
    /// </summary>
    private void UpdateTexts()
    {
        if (wakeWordConfidenceText != null)
            wakeWordConfidenceText.text = $"{Mathf.RoundToInt(currentSettings.wakeWordConfidence * 100)}%";
        
        if (speechTimeoutText != null)
            speechTimeoutText.text = $"{currentSettings.speechTimeout:F1}秒";
        
        if (ttsSpeedText != null)
            ttsSpeedText.text = $"{currentSettings.ttsSpeed:F1}x";
        
        if (ttsPitchText != null)
            ttsPitchText.text = $"{currentSettings.ttsPitch:F1}x";
        
        if (ttsBufferTimeText != null)
            ttsBufferTimeText.text = $"{currentSettings.ttsBufferTime:F1}秒";
        
        if (minTextLengthText != null)
            minTextLengthText.text = $"{Mathf.RoundToInt(currentSettings.minTextLength)}字符";
    }
    
    /// <summary>
    /// 获取语言索引
    /// </summary>
    private int GetLanguageIndex(string language)
    {
        switch (language)
        {
            case "zh-CN": return 0;
            case "en-US": return 1;
            case "ja-JP": return 2;
            case "ko-KR": return 3;
            default: return 0;
        }
    }
    
    /// <summary>
    /// 获取语言值
    /// </summary>
    private string GetLanguageValue(int index)
    {
        switch (index)
        {
            case 0: return "zh-CN";
            case 1: return "en-US";
            case 2: return "ja-JP";
            case 3: return "ko-KR";
            default: return "zh-CN";
        }
    }
    
    /// <summary>
    /// 获取语音索引
    /// </summary>
    private int GetVoiceIndex(string voice)
    {
        switch (voice)
        {
            case "xiaoyan": return 0; // 小燕和小研都使用xiaoyan，默认返回0
            case "xiaoyu": return 1;
            case "xiaoqi": return 3;
            case "xiaofeng": return 4;
            case "xiaomei": return 5;
            default: return 0;
        }
    }
    
    /// <summary>
    /// 获取语音值
    /// </summary>
    private string GetVoiceValue(int index)
    {
        switch (index)
        {
            case 0: return "xiaoyan"; // 小燕
            case 1: return "xiaoyu";
            case 2: return "xiaoyan"; // 小研也使用xiaoyan语音
            case 3: return "xiaoqi";
            case 4: return "xiaofeng";
            case 5: return "xiaomei";
            default: return "xiaoyan";
        }
    }
    
    // 事件处理方法
    private void OnWakeWordChanged(string value)
    {
        currentSettings.wakeWord = value;
        ApplySettings();
    }
    
    private void OnWakeWordConfidenceChanged(float value)
    {
        currentSettings.wakeWordConfidence = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnEnableWakeWordChanged(bool value)
    {
        currentSettings.enableWakeWord = value;
        ApplySettings();
    }
    
    private void OnSpeechLanguageChanged(int index)
    {
        currentSettings.speechLanguage = GetLanguageValue(index);
        ApplySettings();
    }
    
    private void OnSpeechTimeoutChanged(float value)
    {
        currentSettings.speechTimeout = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnEnableRealTimeRecognitionChanged(bool value)
    {
        currentSettings.enableRealTimeRecognition = value;
        ApplySettings();
    }
    
    private void OnTTSVoiceChanged(int index)
    {
        currentSettings.ttsVoice = GetVoiceValue(index);
        ApplySettings();
    }
    
    private void OnTTSSpeedChanged(float value)
    {
        currentSettings.ttsSpeed = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnTTSPitchChanged(float value)
    {
        currentSettings.ttsPitch = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnEnableStreamTTSChanged(bool value)
    {
        currentSettings.enableStreamTTS = value;
        ApplySettings();
    }
    
    private void OnTTSBufferTimeChanged(float value)
    {
        currentSettings.ttsBufferTime = value;
        UpdateTexts();
        ApplySettings();
    }
    
    private void OnMinTextLengthChanged(float value)
    {
        currentSettings.minTextLength = Mathf.RoundToInt(value);
        UpdateTexts();
        ApplySettings();
    }
    
    /// <summary>
    /// 测试唤醒词
    /// </summary>
    private void TestWakeWord()
    {
        Debug.Log($"测试唤醒词: {currentSettings.wakeWord}");
        // 这里可以添加唤醒词测试逻辑
    }
    
    /// <summary>
    /// 测试TTS
    /// </summary>
    private void TestTTS()
    {
        Debug.Log("测试TTS语音合成");
        // 这里可以添加TTS测试逻辑
    }
    
    /// <summary>
    /// 重置语音设置
    /// </summary>
    private void ResetVoiceSettings()
    {
        currentSettings = new VoiceSettings();
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
            SettingsManager.Instance.UpdateVoiceSettings(currentSettings);
        }
    }
    
    /// <summary>
    /// 语音设置变更回调
    /// </summary>
    private void OnVoiceSettingsChanged(VoiceSettings newSettings)
    {
        currentSettings = newSettings;
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        // 取消事件监听
        SettingsManager.OnVoiceSettingsChanged -= OnVoiceSettingsChanged;
    }
}
