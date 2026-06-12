using UnityEngine;
using System;

/// <summary>
/// Doubao配置设置
/// 存储Doubao（火山引擎）相关的API配置和组件引用
/// 可以直接挂载到GameObject上，也可以从Project窗口拖拽脚本文件到GameObject上挂载
/// </summary>
public class DoubaoSettings : MonoBehaviour
{
    [Header("API配置")]
    [Tooltip("火山引擎 AppID")]
    [SerializeField] private string appId = "";
    
    [Tooltip("火山引擎 Access Token")]
    [SerializeField] private string token = "";
    
    [Tooltip("集群名称")]
    [SerializeField] private string cluster = "volcano_icl";
    
    [Header("音色配置")]
    [Tooltip("是否启用语音克隆功能（默认关闭，需要手动开启）")]
    [SerializeField] private bool enableVoiceClone = false;
    
    [Tooltip("当前使用的说话人ID（克隆后的speaker_id）")]
    [SerializeField] private string currentSpeakerId = "";
    
    [Tooltip("说话人名称（用于显示）")]
    [SerializeField] private string currentSpeakerName = "";
    
    [Header("组件引用（自动查找）")]
    [Tooltip("Doubao流式TTS")]
    [SerializeField] private DoubaoStreamTTS streamTTS;
    
    [Tooltip("Doubao V3 双向流式TTS")]
    [SerializeField] private DoubaoStreamTTSV3 streamTTSV3;
    
    [Tooltip("Doubao语音克隆")]
    [SerializeField] private DoubaoVoiceClone voiceClone;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // 事件
    public event Action<string, string> OnCredentialsChanged; // appId, token
    public event Action<string, string> OnSpeakerIdChanged; // speakerId, speakerName
    
    /// <summary>
    /// 初始化（由APIManager调用）
    /// </summary>
    public void Initialize()
    {
        // 优先使用 Inspector 中拖拽的引用
        // 如果没有，优先查找同 GameObject 的组件，再全局查找
        
        // 查找 StreamTTS（优先同 GameObject，再全局）
        if (streamTTS == null)
        {
            streamTTS = GetComponent<DoubaoStreamTTS>();
            if (streamTTS == null)
                streamTTS = FindObjectOfType<DoubaoStreamTTS>();
        }
        
        // 查找 StreamTTSV3
        if (streamTTSV3 == null)
        {
            streamTTSV3 = GetComponent<DoubaoStreamTTSV3>();
            if (streamTTSV3 == null)
                streamTTSV3 = FindObjectOfType<DoubaoStreamTTSV3>();
        }
        
        // 查找 VoiceClone
        if (voiceClone == null)
        {
            voiceClone = GetComponent<DoubaoVoiceClone>();
            if (voiceClone == null)
                voiceClone = FindObjectOfType<DoubaoVoiceClone>();
        }
        
        // 同步配置到组件
        SyncToComponents();
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoSettings: 初始化完成 - StreamTTS: {streamTTS != null}, " +
                      $"StreamTTSV3: {streamTTSV3 != null}, VoiceClone: {voiceClone != null}");
        }
    }
    
    /// <summary>
    /// 延迟同步方法（用于组件初始化后）
    /// 如果组件引用为空，重新查找并同步
    /// </summary>
    public void SyncIfNeeded()
    {
        // 如果组件引用为空，重新查找
        if (streamTTS == null || streamTTSV3 == null || voiceClone == null)
        {
            if (enableDebug)
                Debug.Log("DoubaoSettings: 检测到组件引用为空，重新初始化");
            Initialize();
        }
        else
        {
            // 只同步配置，不重新查找
            SyncToComponents();
        }
    }
    
    /// <summary>
    /// 设置凭证
    /// </summary>
    public void SetCredentials(string appId, string token, string cluster = "volcano_icl")
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            if (enableDebug)
                Debug.LogWarning("DoubaoSettings: AppID或Token为空");
            return;
        }
        
        this.appId = appId;
        this.token = token;
        this.cluster = cluster;
        
        SyncCredentialsToComponents();
        OnCredentialsChanged?.Invoke(appId, token);
        
        if (enableDebug)
            Debug.Log("DoubaoSettings: 凭证已更新");
    }
    
    /// <summary>
    /// 获取凭证
    /// </summary>
    public string GetAppId() => appId;
    public string GetToken() => token;
    public string GetCluster() => cluster;
    
    /// <summary>
    /// 启用/禁用语音克隆功能
    /// </summary>
    public void SetVoiceCloneEnabled(bool enabled)
    {
        enableVoiceClone = enabled;
        if (enableDebug)
            Debug.Log($"DoubaoSettings: 语音克隆功能已{(enabled ? "启用" : "禁用")}");
    }
    
    /// <summary>
    /// 获取语音克隆功能是否启用
    /// </summary>
    public bool IsVoiceCloneEnabled() => enableVoiceClone;
    
    /// <summary>
    /// 设置说话人ID
    /// </summary>
    public void SetSpeakerId(string speakerId, string speakerName = "")
    {
        if (!enableVoiceClone)
        {
            if (enableDebug)
                Debug.LogWarning("DoubaoSettings: 语音克隆功能未启用，无法设置说话人ID");
            return;
        }
        
        currentSpeakerId = speakerId;
        if (!string.IsNullOrEmpty(speakerName))
            currentSpeakerName = speakerName;
        
        SyncSpeakerIdToComponents();
        OnSpeakerIdChanged?.Invoke(speakerId, currentSpeakerName);
        
        if (enableDebug)
            Debug.Log($"DoubaoSettings: 说话人ID已更新 - {currentSpeakerName} ({speakerId})");
    }
    
    /// <summary>
    /// 获取说话人ID
    /// </summary>
    public string GetSpeakerId() => currentSpeakerId;
    
    /// <summary>
    /// 获取说话人名称
    /// </summary>
    public string GetSpeakerName() => currentSpeakerName;
    
    /// <summary>
    /// 同步配置到所有组件
    /// </summary>
    private void SyncToComponents()
    {
        SyncCredentialsToComponents();
        SyncSpeakerIdToComponents();
    }
    
    /// <summary>
    /// 同步凭证到组件
    /// </summary>
    private void SyncCredentialsToComponents()
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token)) return;
        
        // 同步到StreamTTS
        if (streamTTS != null)
        {
            streamTTS.SetCredentials(appId, token, cluster);
        }
        
        // 同步到StreamTTSV3（V3使用resourceId，默认值为"seed-tts-1.0"）
        if (streamTTSV3 != null)
        {
            // V3 API 使用 resourceId 而不是 cluster，使用默认值
            streamTTSV3.SetCredentials(appId, token);
        }
        
        // 同步到VoiceClone
        if (voiceClone != null)
        {
            voiceClone.SetCredentials(appId, token);
        }
    }
    
    /// <summary>
    /// 同步说话人ID到组件
    /// </summary>
    private void SyncSpeakerIdToComponents()
    {
        if (string.IsNullOrEmpty(currentSpeakerId)) return;
        
        if (streamTTS != null)
        {
            streamTTS.SetSpeakerId(currentSpeakerId);
        }
        
        if (streamTTSV3 != null)
        {
            streamTTSV3.SetSpeakerId(currentSpeakerId);
        }
    }
    
    // 组件访问接口
    public DoubaoStreamTTS GetStreamTTS() => streamTTS;
    public DoubaoStreamTTSV3 GetStreamTTSV3() => streamTTSV3;
    public DoubaoVoiceClone GetVoiceClone() => voiceClone;
    
    /// <summary>
    /// 设置组件引用
    /// </summary>
    public void SetStreamTTS(DoubaoStreamTTS tts) => streamTTS = tts;
    public void SetStreamTTSV3(DoubaoStreamTTSV3 tts) => streamTTSV3 = tts;
    public void SetVoiceClone(DoubaoVoiceClone clone) => voiceClone = clone;
}

