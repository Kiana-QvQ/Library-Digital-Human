using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 设置面板主控制器
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("设置面板")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button importButton;
    
    [Header("设置标签页")]
    [SerializeField] private Button gameTabButton;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button uiTabButton;
    [SerializeField] private Button networkTabButton;
    [SerializeField] private Button voiceTabButton;
    [SerializeField] private Button aiTabButton;
    [SerializeField] private Button visionTabButton;
    [SerializeField] private Button developerTabButton;
    
    [Header("设置内容面板")]
    [SerializeField] private GameObject gameSettingsPanel;
    [SerializeField] private GameObject audioSettingsPanel;
    [SerializeField] private GameObject uiSettingsPanel;
    [SerializeField] private GameObject networkSettingsPanel;
    [SerializeField] private GameObject voiceSettingsPanel;
    [SerializeField] private GameObject aiSettingsPanel;
    [SerializeField] private GameObject visionSettingsPanel;
    [SerializeField] private GameObject developerSettingsPanel;
    
    [Header("状态显示")]
    [SerializeField] private TextMeshProUGUI statusText;
    
    private SettingsData currentSettings;
    private GameObject currentActivePanel;
    
    private void Start()
    {
        InitializeUI();
        LoadCurrentSettings();
        SetupEventListeners();
    }
    
    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        // 默认显示游戏设置面板
        ShowSettingsPanel(gameSettingsPanel);
        
        // 设置默认标签页状态
        SetTabActive(gameTabButton, true);
    }
    
    /// <summary>
    /// 加载当前设置
    /// </summary>
    private void LoadCurrentSettings()
    {
        if (SettingsManager.Instance != null)
        {
            currentSettings = SettingsManager.Instance.GetSettings();
        }
    }
    
    /// <summary>
    /// 设置事件监听
    /// </summary>
    private void SetupEventListeners()
    {
        // 关闭按钮
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSettings);
        
        // 重置按钮
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSettings);
        
        // 导出按钮
        if (exportButton != null)
            exportButton.onClick.AddListener(ExportSettings);
        
        // 导入按钮
        if (importButton != null)
            importButton.onClick.AddListener(ImportSettings);
        
        // 标签页按钮
        if (gameTabButton != null)
            gameTabButton.onClick.AddListener(() => SwitchToTab(gameSettingsPanel, gameTabButton));
        
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(() => SwitchToTab(audioSettingsPanel, audioTabButton));
        
        if (uiTabButton != null)
            uiTabButton.onClick.AddListener(() => SwitchToTab(uiSettingsPanel, uiTabButton));
        
        if (networkTabButton != null)
            networkTabButton.onClick.AddListener(() => SwitchToTab(networkSettingsPanel, networkTabButton));
        
        if (voiceTabButton != null)
            voiceTabButton.onClick.AddListener(() => SwitchToTab(voiceSettingsPanel, voiceTabButton));
        
        if (aiTabButton != null)
            aiTabButton.onClick.AddListener(() => SwitchToTab(aiSettingsPanel, aiTabButton));
        
        if (visionTabButton != null)
            visionTabButton.onClick.AddListener(() => SwitchToTab(visionSettingsPanel, visionTabButton));
        
        if (developerTabButton != null)
            developerTabButton.onClick.AddListener(() => SwitchToTab(developerSettingsPanel, developerTabButton));
        
        // 监听设置变更事件
        SettingsManager.OnSettingsChanged += OnSettingsChanged;
    }
    
    /// <summary>
    /// 显示设置面板
    /// </summary>
    public void ShowSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            LoadCurrentSettings();
        }
    }
    
    /// <summary>
    /// 关闭设置面板
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// 切换到指定标签页
    /// </summary>
    private void SwitchToTab(GameObject targetPanel, Button tabButton)
    {
        // 隐藏当前面板
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(false);
        }
        
        // 显示目标面板
        ShowSettingsPanel(targetPanel);
        
        // 更新标签页状态
        ResetAllTabs();
        SetTabActive(tabButton, true);
    }
    
    /// <summary>
    /// 显示设置面板
    /// </summary>
    private void ShowSettingsPanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            currentActivePanel = panel;
        }
    }
    
    /// <summary>
    /// 设置标签页激活状态
    /// </summary>
    private void SetTabActive(Button tabButton, bool active)
    {
        if (tabButton != null)
        {
            ColorBlock colors = tabButton.colors;
            colors.normalColor = active ? Color.cyan : Color.white;
            tabButton.colors = colors;
        }
    }
    
    /// <summary>
    /// 重置所有标签页状态
    /// </summary>
    private void ResetAllTabs()
    {
        SetTabActive(gameTabButton, false);
        SetTabActive(audioTabButton, false);
        SetTabActive(uiTabButton, false);
        SetTabActive(networkTabButton, false);
        SetTabActive(voiceTabButton, false);
        SetTabActive(aiTabButton, false);
        SetTabActive(visionTabButton, false);
        SetTabActive(developerTabButton, false);
    }
    
    /// <summary>
    /// 重置设置
    /// </summary>
    private void ResetSettings()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ResetToDefault();
            ShowStatus("设置已重置为默认值", Color.green);
        }
    }
    
    /// <summary>
    /// 导出设置
    /// </summary>
    private void ExportSettings()
    {
        if (SettingsManager.Instance != null)
        {
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, "exported_settings.json");
            SettingsManager.Instance.ExportSettings(filePath);
            ShowStatus($"设置已导出到: {filePath}", Color.green);
        }
    }
    
    /// <summary>
    /// 导入设置
    /// </summary>
    private void ImportSettings()
    {
        // 这里可以添加文件选择对话框
        // 暂时使用默认路径
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "exported_settings.json");
        
        if (System.IO.File.Exists(filePath))
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.ImportSettings(filePath);
                ShowStatus("设置导入成功", Color.green);
            }
        }
        else
        {
            ShowStatus("未找到导入文件", Color.red);
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
            
            // 3秒后清除状态信息
            Invoke(nameof(ClearStatus), 3f);
        }
    }
    
    /// <summary>
    /// 清除状态信息
    /// </summary>
    private void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }
    
    /// <summary>
    /// 设置变更回调
    /// </summary>
    private void OnSettingsChanged(SettingsData newSettings)
    {
        currentSettings = newSettings;
        ShowStatus("设置已更新", Color.blue);
    }
    
    private void OnDestroy()
    {
        // 取消事件监听
        SettingsManager.OnSettingsChanged -= OnSettingsChanged;
    }
}
