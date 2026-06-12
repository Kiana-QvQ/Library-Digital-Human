using UnityEngine;
using System;

/// <summary>
/// Coze语音合成配置设置
/// 存储Coze TTS相关的API配置和组件引用
/// 可以直接从Project窗口拖拽脚本文件到GameObject上挂载
/// </summary>
public class CozeSettings : MonoBehaviour
{
    [Header("API配置")]
    [Tooltip("Coze API Key (Personal Access Token). 获取方式: https://www.coze.cn -> 开发者中心 -> API管理")]
    [SerializeField] private string apiKey = "";
    
    [Header("音色配置")]
    [Tooltip("当前使用的音色ID")]
    [SerializeField] private string currentVoiceId = "";
    
    [Tooltip("音色名称（用于显示）")]
    [SerializeField] private string currentVoiceName = "";
    
    [Header("组件引用（自动查找）")]
    [Tooltip("Coze流式TTS组件")]
    [SerializeField] private CozeStreamTTS streamTTS;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // 事件
    public event Action<string> OnAPIKeyChanged;
    public event Action<string, string> OnVoiceChanged; // voiceId, voiceName
    
    /// <summary>
    /// 初始化（由APIManager调用）
    /// </summary>
    public void Initialize()
    {
        // 自动查找组件
        if (streamTTS == null)
            streamTTS = FindObjectOfType<CozeStreamTTS>();
        
        // 同步配置到组件
        SyncToComponents();
    }
    
    /// <summary>
    /// 设置API Key
    /// </summary>
    public void SetAPIKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            if (enableDebug)
                Debug.LogWarning("CozeSettings: API Key为空");
            return;
        }
        
        apiKey = key;
        SyncAPIKeyToComponents();
        OnAPIKeyChanged?.Invoke(apiKey);
        
        if (enableDebug)
            Debug.Log("CozeSettings: API Key已更新");
    }
    
    /// <summary>
    /// 获取API Key
    /// </summary>
    public string GetAPIKey() => apiKey;
    
    /// <summary>
    /// 设置音色ID
    /// </summary>
    public void SetVoiceId(string voiceId, string voiceName = "")
    {
        currentVoiceId = voiceId;
        if (!string.IsNullOrEmpty(voiceName))
            currentVoiceName = voiceName;
        
        SyncVoiceIdToComponents();
        OnVoiceChanged?.Invoke(voiceId, currentVoiceName);
        
        if (enableDebug)
            Debug.Log($"CozeSettings: 音色已更新 - {currentVoiceName} ({voiceId})");
    }
    
    /// <summary>
    /// 获取音色ID
    /// </summary>
    public string GetVoiceId() => currentVoiceId;
    
    /// <summary>
    /// 获取音色名称
    /// </summary>
    public string GetVoiceName() => currentVoiceName;
    
    /// <summary>
    /// 同步配置到所有组件
    /// </summary>
    private void SyncToComponents()
    {
        SyncAPIKeyToComponents();
        SyncVoiceIdToComponents();
    }
    
    /// <summary>
    /// 同步API Key到组件
    /// </summary>
    private void SyncAPIKeyToComponents()
    {
        if (string.IsNullOrEmpty(apiKey)) return;
        
        // 同步到StreamTTS（语音合成核心组件）
        if (streamTTS != null)
        {
            streamTTS.SetAPIKey(apiKey);
        }
    }
    
    /// <summary>
    /// 同步音色ID到组件
    /// </summary>
    private void SyncVoiceIdToComponents()
    {
        if (string.IsNullOrEmpty(currentVoiceId)) return;
        
        if (streamTTS != null)
        {
            streamTTS.SetVoiceId(currentVoiceId);
        }
    }
    
    // 组件访问接口
    public CozeStreamTTS GetStreamTTS() => streamTTS;
    
    /// <summary>
    /// 设置组件引用
    /// </summary>
    public void SetStreamTTS(CozeStreamTTS tts) => streamTTS = tts;
}

