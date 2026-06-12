using UnityEngine;

/// <summary>
/// GLM 视觉模型配置管理
/// 统一管理 GLM-4.6V-Flash API 配置
/// </summary>
public class GLMVisionSettings : MonoBehaviour
{
    [Header("API配置")]
    [Tooltip("GLM API Key - 从 https://open.bigmodel.cn 获取")]
    [SerializeField] private string apiKey = "0f7c17daae644dafab98e1e1422cd59c.bXwSTYvzitFJ1Ost";
    
    [Tooltip("API 基础URL")]
    [SerializeField] private string apiBaseUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    
    [Tooltip("模型名称")]
    [SerializeField] private string model = "glm-4.6v-flash";
    
    [Header("组件引用")]
    [Tooltip("GLM视觉客户端组件（自动查找）")]
    [SerializeField] private GLMVisionClient visionClient;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // 事件
    public System.Action OnCredentialsChanged;
    
    /// <summary>
    /// 获取 API Key
    /// </summary>
    public string GetApiKey() => apiKey;
    
    /// <summary>
    /// 获取 API 基础URL
    /// </summary>
    public string GetApiBaseUrl() => apiBaseUrl;
    
    /// <summary>
    /// 获取模型名称
    /// </summary>
    public string GetModel() => model;
    
    /// <summary>
    /// 获取视觉客户端组件
    /// </summary>
    public GLMVisionClient GetVisionClient()
    {
        if (visionClient == null)
        {
            visionClient = GetComponent<GLMVisionClient>();
            if (visionClient == null)
                visionClient = FindObjectOfType<GLMVisionClient>();
        }
        return visionClient;
    }
    
    /// <summary>
    /// 设置 API Key
    /// </summary>
    public void SetApiKey(string newApiKey)
    {
        if (apiKey != newApiKey)
        {
            apiKey = newApiKey;
            OnCredentialsChanged?.Invoke();
            if (enableDebug)
                Debug.Log("GLMVisionSettings: API Key 已更新");
        }
    }
    
    /// <summary>
    /// 初始化设置
    /// </summary>
    public void Initialize()
    {
        // 查找视觉客户端组件
        if (visionClient == null)
        {
            visionClient = GetComponent<GLMVisionClient>();
            if (visionClient == null)
                visionClient = FindObjectOfType<GLMVisionClient>();
        }
        
        // 同步配置到组件
        SyncToComponents();
        
        if (enableDebug)
        {
            Debug.Log($"GLMVisionSettings: 初始化完成 - VisionClient: {visionClient != null}");
        }
    }
    
    /// <summary>
    /// 同步配置到组件
    /// </summary>
    private void SyncToComponents()
    {
        if (visionClient != null)
        {
            visionClient.SetCredentials(apiKey, apiBaseUrl, model);
        }
    }
    
    private void Start()
    {
        Initialize();
    }
    
    private void OnValidate()
    {
        // 在 Inspector 中修改时自动同步
        if (Application.isPlaying)
        {
            SyncToComponents();
        }
    }
}

