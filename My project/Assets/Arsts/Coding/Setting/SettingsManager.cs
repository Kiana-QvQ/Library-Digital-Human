using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 设置管理器 - 单例模式
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }
    
    [Header("设置数据")]
    [SerializeField] private SettingsData settings = new SettingsData();
    
    [Header("文件路径")]
    [SerializeField] private string settingsFileName = "user_settings.json";
    [SerializeField] private string defaultSettingsFileName = "default_settings.json";
    
    // 设置变更事件
    public static event Action<SettingsData> OnSettingsChanged;
    public static event Action<GameSettings> OnGameSettingsChanged;
    public static event Action<AudioSettings> OnAudioSettingsChanged;
    public static event Action<UISettings> OnUISettingsChanged;
    public static event Action<NetworkSettings> OnNetworkSettingsChanged;
    public static event Action<VoiceSettings> OnVoiceSettingsChanged;
    public static event Action<AISettings> OnAISettingsChanged;
    public static event Action<VisionSettings> OnVisionSettingsChanged;
    
    private string settingsFilePath;
    private string defaultSettingsFilePath;
    
    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        LoadSettings();
        ApplySettings();
    }
    
    /// <summary>
    /// 初始化设置
    /// </summary>
    private void InitializeSettings()
    {
        settingsFilePath = Path.Combine(Application.persistentDataPath, settingsFileName);
        defaultSettingsFilePath = Path.Combine(Application.streamingAssetsPath, defaultSettingsFileName);
        
        // 确保目录存在
        if (!string.IsNullOrEmpty(Path.GetDirectoryName(settingsFilePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath));
        }
    }
    
    /// <summary>
    /// 获取当前设置
    /// </summary>
    public SettingsData GetSettings()
    {
        return settings;
    }
    
    /// <summary>
    /// 获取游戏设置
    /// </summary>
    public GameSettings GetGameSettings()
    {
        return settings.game;
    }
    
    /// <summary>
    /// 获取音频设置
    /// </summary>
    public AudioSettings GetAudioSettings()
    {
        return settings.audio;
    }
    
    /// <summary>
    /// 获取UI设置
    /// </summary>
    public UISettings GetUISettings()
    {
        return settings.ui;
    }
    
    /// <summary>
    /// 获取网络设置
    /// </summary>
    public NetworkSettings GetNetworkSettings()
    {
        return settings.network;
    }
    
    /// <summary>
    /// 获取语音设置
    /// </summary>
    public VoiceSettings GetVoiceSettings()
    {
        return settings.voice;
    }
    
    /// <summary>
    /// 获取AI设置
    /// </summary>
    public AISettings GetAISettings()
    {
        return settings.ai;
    }
    
    /// <summary>
    /// 获取视觉设置
    /// </summary>
    public VisionSettings GetVisionSettings()
    {
        return settings.vision;
    }
    
    /// <summary>
    /// 更新游戏设置
    /// </summary>
    public void UpdateGameSettings(GameSettings newSettings)
    {
        settings.game = newSettings;
        OnGameSettingsChanged?.Invoke(settings.game);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
    }
    
    /// <summary>
    /// 更新音频设置
    /// </summary>
    public void UpdateAudioSettings(AudioSettings newSettings)
    {
        settings.audio = newSettings;
        OnAudioSettingsChanged?.Invoke(settings.audio);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
        ApplyAudioSettings();
    }
    
    /// <summary>
    /// 更新UI设置
    /// </summary>
    public void UpdateUISettings(UISettings newSettings)
    {
        settings.ui = newSettings;
        OnUISettingsChanged?.Invoke(settings.ui);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
        ApplyUISettings();
    }
    
    /// <summary>
    /// 更新网络设置
    /// </summary>
    public void UpdateNetworkSettings(NetworkSettings newSettings)
    {
        settings.network = newSettings;
        OnNetworkSettingsChanged?.Invoke(settings.network);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
    }
    
    /// <summary>
    /// 更新语音设置
    /// </summary>
    public void UpdateVoiceSettings(VoiceSettings newSettings)
    {
        settings.voice = newSettings;
        OnVoiceSettingsChanged?.Invoke(settings.voice);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
    }
    
    /// <summary>
    /// 更新AI设置
    /// </summary>
    public void UpdateAISettings(AISettings newSettings)
    {
        settings.ai = newSettings;
        OnAISettingsChanged?.Invoke(settings.ai);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
    }
    
    /// <summary>
    /// 更新视觉设置
    /// </summary>
    public void UpdateVisionSettings(VisionSettings newSettings)
    {
        settings.vision = newSettings;
        OnVisionSettingsChanged?.Invoke(settings.vision);
        OnSettingsChanged?.Invoke(settings);
        SaveSettings();
        ApplyVisionSettings();
    }
    
    /// <summary>
    /// 加载设置
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsFilePath))
            {
                string json = File.ReadAllText(settingsFilePath);
                settings = JsonConvert.DeserializeObject<SettingsData>(json);
                Debug.Log("设置加载成功");
            }
            else
            {
                LoadDefaultSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载设置失败: {e.Message}");
            LoadDefaultSettings();
        }
    }
    
    /// <summary>
    /// 加载默认设置
    /// </summary>
    public void LoadDefaultSettings()
    {
        try
        {
            if (File.Exists(defaultSettingsFilePath))
            {
                string json = File.ReadAllText(defaultSettingsFilePath);
                settings = JsonConvert.DeserializeObject<SettingsData>(json);
                Debug.Log("默认设置加载成功");
            }
            else
            {
                settings = new SettingsData();
                Debug.Log("使用内置默认设置");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载默认设置失败: {e.Message}");
            settings = new SettingsData();
        }
    }
    
    /// <summary>
    /// 保存设置
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(settingsFilePath, json);
            Debug.Log("设置保存成功");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存设置失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 重置为默认设置
    /// </summary>
    public void ResetToDefault()
    {
        LoadDefaultSettings();
        ApplySettings();
        OnSettingsChanged?.Invoke(settings);
        Debug.Log("设置已重置为默认值");
    }
    
    /// <summary>
    /// 应用所有设置
    /// </summary>
    private void ApplySettings()
    {
        ApplyAudioSettings();
        ApplyUISettings();
        ApplyGameSettings();
        ApplyVisionSettings();
        ApplyNetworkSettings();
    }
    
    /// <summary>
    /// 应用音频设置
    /// </summary>
    private void ApplyAudioSettings()
    {
        AudioListener.volume = settings.audio.masterVolume;
        // 这里可以添加更多音频设置的应用逻辑
    }
    
    /// <summary>
    /// 应用UI设置
    /// </summary>
    private void ApplyUISettings()
    {
        // 这里可以添加UI设置的应用逻辑
        // 例如：字体大小、主题等
    }
    
    /// <summary>
    /// 应用游戏设置
    /// </summary>
    private void ApplyGameSettings()
    {
        // 设置质量等级
        QualitySettings.SetQualityLevel((int)settings.game.quality);
        
        // 设置语言
        if (settings.game.language != "zh-CN")
        {
            // 这里可以添加语言切换逻辑
        }
    }
    
    /// <summary>
    /// 应用视觉设置
    /// </summary>
    private void ApplyVisionSettings()
    {
        // 同步视觉设置到 GLMVisionSettings / GLMVisionClient 组件
        GLMVisionSettings visionSettingsComponent = FindObjectOfType<GLMVisionSettings>();
        if (visionSettingsComponent != null)
        {
            // 基础凭证
            visionSettingsComponent.SetApiKey(settings.vision.glmApiKey);

            // 同步到 GLMVisionClient（包括路由模式和后端 URL）
            GLMVisionClient visionClient = visionSettingsComponent.GetVisionClient();
            if (visionClient != null)
            {
                visionClient.SetCredentials(
                    settings.vision.glmApiKey,
                    settings.vision.glmApiBaseUrl,
                    settings.vision.glmModel
                );

                visionClient.ApplyRoutingSettings(
                    settings.vision.routeMode,
                    settings.vision.backendVisionUrl
                );
            }
        }
    }
    
    /// <summary>
    /// 应用网络设置
    /// </summary>
    private void ApplyNetworkSettings()
    {
        // 同步 Coze 设置
        CozeSettings cozeSettings = FindObjectOfType<CozeSettings>();
        if (cozeSettings != null && !string.IsNullOrEmpty(settings.network.cozeApiKey))
        {
            // 如果 CozeSettings 有设置 API Key 的方法，可以在这里调用
            // cozeSettings.SetApiKey(settings.network.cozeApiKey);
        }
        
        // 同步 Doubao 设置
        DoubaoSettings doubaoSettings = FindObjectOfType<DoubaoSettings>();
        if (doubaoSettings != null)
        {
            // 如果 DoubaoSettings 有设置方法，可以在这里调用
            // doubaoSettings.SetCredentials(...);
        }
    }
    
    /// <summary>
    /// 导出设置
    /// </summary>
    public void ExportSettings(string filePath)
    {
        try
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
            Debug.Log($"设置导出成功: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"导出设置失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 导入设置
    /// </summary>
    public void ImportSettings(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                settings = JsonConvert.DeserializeObject<SettingsData>(json);
                ApplySettings();
                OnSettingsChanged?.Invoke(settings);
                Debug.Log($"设置导入成功: {filePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"导入设置失败: {e.Message}");
        }
    }
    
    private void OnDestroy()
    {
        SaveSettings();
    }
}
