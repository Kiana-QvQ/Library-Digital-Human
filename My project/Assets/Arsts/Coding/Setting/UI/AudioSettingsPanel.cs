using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 音频设置面板
/// </summary>
public class AudioSettingsPanel : MonoBehaviour
{
    [Header("音量控制")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private TextMeshProUGUI musicVolumeText;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TextMeshProUGUI sfxVolumeText;
    [SerializeField] private Slider ttsVolumeSlider;
    [SerializeField] private TextMeshProUGUI ttsVolumeText;
    [SerializeField] private Slider microphoneVolumeSlider;
    [SerializeField] private TextMeshProUGUI microphoneVolumeText;
    
    [Header("音频设备")]
    [SerializeField] private TMP_Dropdown audioOutputDropdown;
    [SerializeField] private TMP_Dropdown microphoneDropdown;
    
    [Header("音频质量")]
    [SerializeField] private TMP_Dropdown sampleRateDropdown;
    [SerializeField] private TMP_Dropdown bufferSizeDropdown;
    
    [Header("测试按钮")]
    [SerializeField] private Button testAudioButton;
    [SerializeField] private Button resetAudioButton;
    
    private AudioSettings currentSettings;
    
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
        // 初始化音量滑块范围
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
        }
        
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
        }
        
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
        }
        
        if (ttsVolumeSlider != null)
        {
            ttsVolumeSlider.minValue = 0f;
            ttsVolumeSlider.maxValue = 1f;
        }
        
        if (microphoneVolumeSlider != null)
        {
            microphoneVolumeSlider.minValue = 0f;
            microphoneVolumeSlider.maxValue = 1f;
        }
        
        // 初始化采样率选项
        if (sampleRateDropdown != null)
        {
            sampleRateDropdown.ClearOptions();
            sampleRateDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "22050 Hz",
                "44100 Hz",
                "48000 Hz"
            });
        }
        
        // 初始化缓冲区大小选项
        if (bufferSizeDropdown != null)
        {
            bufferSizeDropdown.ClearOptions();
            bufferSizeDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "256",
                "512",
                "1024",
                "2048"
            });
        }
        
        // 初始化音频设备列表
        RefreshAudioDevices();
    }
    
    /// <summary>
    /// 设置事件监听
    /// </summary>
    private void SetupEventListeners()
    {
        // 音量滑块事件
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        
        if (ttsVolumeSlider != null)
            ttsVolumeSlider.onValueChanged.AddListener(OnTTSVolumeChanged);
        
        if (microphoneVolumeSlider != null)
            microphoneVolumeSlider.onValueChanged.AddListener(OnMicrophoneVolumeChanged);
        
        // 设备选择事件
        if (audioOutputDropdown != null)
            audioOutputDropdown.onValueChanged.AddListener(OnAudioOutputChanged);
        
        if (microphoneDropdown != null)
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
        
        // 质量设置事件
        if (sampleRateDropdown != null)
            sampleRateDropdown.onValueChanged.AddListener(OnSampleRateChanged);
        
        if (bufferSizeDropdown != null)
            bufferSizeDropdown.onValueChanged.AddListener(OnBufferSizeChanged);
        
        // 按钮事件
        if (testAudioButton != null)
            testAudioButton.onClick.AddListener(TestAudio);
        
        if (resetAudioButton != null)
            resetAudioButton.onClick.AddListener(ResetAudioSettings);
        
        // 监听设置变更事件
        SettingsManager.OnAudioSettingsChanged += OnAudioSettingsChanged;
    }
    
    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        if (SettingsManager.Instance != null)
        {
            currentSettings = SettingsManager.Instance.GetAudioSettings();
            UpdateUI();
        }
    }
    
    /// <summary>
    /// 更新UI显示
    /// </summary>
    private void UpdateUI()
    {
        // 更新音量滑块
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = currentSettings.masterVolume;
        
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = currentSettings.musicVolume;
        
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = currentSettings.sfxVolume;
        
        if (ttsVolumeSlider != null)
            ttsVolumeSlider.value = currentSettings.ttsVolume;
        
        if (microphoneVolumeSlider != null)
            microphoneVolumeSlider.value = currentSettings.microphoneVolume;
        
        // 更新音量文本
        UpdateVolumeTexts();
        
        // 更新采样率
        if (sampleRateDropdown != null)
        {
            int sampleRateIndex = GetSampleRateIndex(currentSettings.sampleRate);
            sampleRateDropdown.value = sampleRateIndex;
        }
        
        // 更新缓冲区大小
        if (bufferSizeDropdown != null)
        {
            int bufferSizeIndex = GetBufferSizeIndex(currentSettings.bufferSize);
            bufferSizeDropdown.value = bufferSizeIndex;
        }
    }
    
    /// <summary>
    /// 更新音量文本
    /// </summary>
    private void UpdateVolumeTexts()
    {
        if (masterVolumeText != null)
            masterVolumeText.text = $"{Mathf.RoundToInt(currentSettings.masterVolume * 100)}%";
        
        if (musicVolumeText != null)
            musicVolumeText.text = $"{Mathf.RoundToInt(currentSettings.musicVolume * 100)}%";
        
        if (sfxVolumeText != null)
            sfxVolumeText.text = $"{Mathf.RoundToInt(currentSettings.sfxVolume * 100)}%";
        
        if (ttsVolumeText != null)
            ttsVolumeText.text = $"{Mathf.RoundToInt(currentSettings.ttsVolume * 100)}%";
        
        if (microphoneVolumeText != null)
            microphoneVolumeText.text = $"{Mathf.RoundToInt(currentSettings.microphoneVolume * 100)}%";
    }
    
    /// <summary>
    /// 刷新音频设备列表
    /// </summary>
    private void RefreshAudioDevices()
    {
        // 刷新音频输出设备
        if (audioOutputDropdown != null)
        {
            audioOutputDropdown.ClearOptions();
            audioOutputDropdown.AddOptions(new System.Collections.Generic.List<string> { "默认" });
            
            string[] devices = Microphone.devices;
            if (devices.Length > 0)
            {
                audioOutputDropdown.AddOptions(new System.Collections.Generic.List<string>(devices));
            }
        }
        
        // 刷新麦克风设备
        if (microphoneDropdown != null)
        {
            microphoneDropdown.ClearOptions();
            microphoneDropdown.AddOptions(new System.Collections.Generic.List<string> { "默认" });
            
            string[] devices = Microphone.devices;
            if (devices.Length > 0)
            {
                microphoneDropdown.AddOptions(new System.Collections.Generic.List<string>(devices));
            }
        }
    }
    
    /// <summary>
    /// 获取采样率索引
    /// </summary>
    private int GetSampleRateIndex(int sampleRate)
    {
        switch (sampleRate)
        {
            case 22050: return 0;
            case 44100: return 1;
            case 48000: return 2;
            default: return 1;
        }
    }
    
    /// <summary>
    /// 获取缓冲区大小索引
    /// </summary>
    private int GetBufferSizeIndex(int bufferSize)
    {
        switch (bufferSize)
        {
            case 256: return 0;
            case 512: return 1;
            case 1024: return 2;
            case 2048: return 3;
            default: return 2;
        }
    }
    
    /// <summary>
    /// 获取采样率值
    /// </summary>
    private int GetSampleRateValue(int index)
    {
        switch (index)
        {
            case 0: return 22050;
            case 1: return 44100;
            case 2: return 48000;
            default: return 44100;
        }
    }
    
    /// <summary>
    /// 获取缓冲区大小值
    /// </summary>
    private int GetBufferSizeValue(int index)
    {
        switch (index)
        {
            case 0: return 256;
            case 1: return 512;
            case 2: return 1024;
            case 3: return 2048;
            default: return 1024;
        }
    }
    
    // 事件处理方法
    private void OnMasterVolumeChanged(float value)
    {
        currentSettings.masterVolume = value;
        UpdateVolumeTexts();
        ApplySettings();
    }
    
    private void OnMusicVolumeChanged(float value)
    {
        currentSettings.musicVolume = value;
        UpdateVolumeTexts();
        ApplySettings();
    }
    
    private void OnSfxVolumeChanged(float value)
    {
        currentSettings.sfxVolume = value;
        UpdateVolumeTexts();
        ApplySettings();
    }
    
    private void OnTTSVolumeChanged(float value)
    {
        currentSettings.ttsVolume = value;
        UpdateVolumeTexts();
        ApplySettings();
    }
    
    private void OnMicrophoneVolumeChanged(float value)
    {
        currentSettings.microphoneVolume = value;
        UpdateVolumeTexts();
        ApplySettings();
    }
    
    private void OnAudioOutputChanged(int index)
    {
        // 这里可以添加音频输出设备切换逻辑
        Debug.Log($"音频输出设备切换到索引: {index}");
    }
    
    private void OnMicrophoneChanged(int index)
    {
        // 这里可以添加麦克风设备切换逻辑
        Debug.Log($"麦克风设备切换到索引: {index}");
    }
    
    private void OnSampleRateChanged(int index)
    {
        currentSettings.sampleRate = GetSampleRateValue(index);
        ApplySettings();
    }
    
    private void OnBufferSizeChanged(int index)
    {
        currentSettings.bufferSize = GetBufferSizeValue(index);
        ApplySettings();
    }
    
    /// <summary>
    /// 测试音频
    /// </summary>
    private void TestAudio()
    {
        // 这里可以添加音频测试逻辑
        Debug.Log("测试音频播放");
    }
    
    /// <summary>
    /// 重置音频设置
    /// </summary>
    private void ResetAudioSettings()
    {
        currentSettings = new AudioSettings();
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
            SettingsManager.Instance.UpdateAudioSettings(currentSettings);
        }
    }
    
    /// <summary>
    /// 音频设置变更回调
    /// </summary>
    private void OnAudioSettingsChanged(AudioSettings newSettings)
    {
        currentSettings = newSettings;
        UpdateUI();
    }
    
    private void OnDestroy()
    {
        // 取消事件监听
        SettingsManager.OnAudioSettingsChanged -= OnAudioSettingsChanged;
    }
}
