using UnityEngine;

/// <summary>
/// 统一的API管理器
/// 管理所有提供商的配置和组件同步
/// </summary>
public class APIManager : MonoBehaviour
{
    [Header("提供商配置（可直接拖拽GameObject或组件引用）")]
    [Tooltip("Coze配置 - 可以直接从Project窗口拖拽脚本文件到GameObject上挂载，或拖拽已挂载的组件引用")]
    public CozeSettings cozeSettings;
    
    [Tooltip("Doubao配置 - 可以直接从Project窗口拖拽脚本文件到GameObject上挂载，或拖拽已挂载的组件引用")]
    public DoubaoSettings doubaoSettings;
    
    [Tooltip("百度配置")]
    public BaiduSettings baiduSettings;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // 单例模式
    private static APIManager instance;
    public static APIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<APIManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // 自动查找Settings
        if (cozeSettings == null)
            cozeSettings = FindObjectOfType<CozeSettings>();
        
        if (doubaoSettings == null)
            doubaoSettings = FindObjectOfType<DoubaoSettings>();
        
        if (baiduSettings == null)
            baiduSettings = FindObjectOfType<BaiduSettings>();
        
        // 初始化所有Settings
        InitializeAllSettings();
    }
    
    /// <summary>
    /// 初始化所有Settings
    /// </summary>
    private void InitializeAllSettings()
    {
        if (cozeSettings != null)
        {
            cozeSettings.Initialize();
            if (enableDebug)
                Debug.Log("APIManager: CozeSettings已初始化");
        }
        
        if (doubaoSettings != null)
        {
            doubaoSettings.Initialize();
            if (enableDebug)
                Debug.Log("APIManager: DoubaoSettings已初始化");
        }
        
        // BaiduSettings 已有自己的初始化逻辑，不需要额外初始化
        if (baiduSettings != null && enableDebug)
        {
            Debug.Log("APIManager: BaiduSettings已找到");
        }
    }
    
    /// <summary>
    /// 获取Coze配置
    /// </summary>
    public CozeSettings GetCozeSettings() => cozeSettings;
    
    /// <summary>
    /// 获取Doubao配置
    /// </summary>
    public DoubaoSettings GetDoubaoSettings() => doubaoSettings;
    
    /// <summary>
    /// 获取百度配置
    /// </summary>
    public BaiduSettings GetBaiduSettings() => baiduSettings;
    
    /// <summary>
    /// 设置Coze配置
    /// </summary>
    public void SetCozeSettings(CozeSettings settings)
    {
        cozeSettings = settings;
        if (cozeSettings != null)
        {
            cozeSettings.Initialize();
        }
    }
    
    /// <summary>
    /// 设置Doubao配置
    /// </summary>
    public void SetDoubaoSettings(DoubaoSettings settings)
    {
        doubaoSettings = settings;
        if (doubaoSettings != null)
        {
            doubaoSettings.Initialize();
        }
    }
    
    /// <summary>
    /// 设置百度配置
    /// </summary>
    public void SetBaiduSettings(BaiduSettings settings)
    {
        baiduSettings = settings;
    }
}

