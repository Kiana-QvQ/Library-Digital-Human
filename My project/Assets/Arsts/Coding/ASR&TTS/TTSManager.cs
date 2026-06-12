using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// TTS统一管理器
/// 管理所有TTS提供者（Baidu、Coze、Doubao），提供统一的接口
/// </summary>
public class TTSManager : MonoBehaviour
{
    [Header("TTS服务选择")]
    [Tooltip("当前使用的TTS服务")]
    [SerializeField] private TTSProvider currentProvider = TTSProvider.Baidu;
    
    [Tooltip("是否启用自动后备（如果当前服务失败，自动尝试其他服务）")]
    [SerializeField] private bool enableAutoFallback = false;
    
    [Header("TTS组件引用（自动查找）")]
    [Tooltip("百度TTS组件")]
    [SerializeField] private BaiduTextToSpeech baiduTTS;
    
    [Tooltip("Coze TTS组件")]
    [SerializeField] private CozeTextToSpeech cozeTTS;
    
    [Tooltip("Doubao TTS组件")]
    [SerializeField] private DoubaoTextToSpeech doubaoTTS;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    /// <summary>
    /// TTS服务提供者枚举
    /// </summary>
    public enum TTSProvider
    {
        Baidu,      // 百度TTS
        Coze,       // Coze TTS（WebSocket流式）
        Doubao      // Doubao TTS（支持音色和语音克隆）
    }
    
    private void Awake()
    {
        // 自动查找同一GameObject下的TTS组件
        if (baiduTTS == null)
            baiduTTS = GetComponent<BaiduTextToSpeech>();
        
        if (cozeTTS == null)
            cozeTTS = GetComponent<CozeTextToSpeech>();
        
        if (doubaoTTS == null)
            doubaoTTS = GetComponent<DoubaoTextToSpeech>();
        
        ForceBaiduProvider();
    }

    /// <summary>
    /// 固定使用百度 TTS，禁用 Coze/豆包流式与自动后备
    /// </summary>
    public void ForceBaiduProvider()
    {
        currentProvider = TTSProvider.Baidu;
        enableAutoFallback = false;
        ValidateCurrentProvider();
    }
    
    /// <summary>
    /// 验证当前选择的TTS服务是否可用
    /// </summary>
    private void ValidateCurrentProvider()
    {
        TTS currentTTS = GetCurrentTTS();
        if (currentTTS == null)
        {
            Debug.LogWarning($"TTSManager: 当前选择的TTS服务 ({currentProvider}) 不可用，尝试查找其他可用服务");
            
            // 自动切换到可用的服务（优先百度）
            if (baiduTTS != null)
                currentProvider = TTSProvider.Baidu;
            else if (doubaoTTS != null)
                currentProvider = TTSProvider.Doubao;
            else if (cozeTTS != null)
                currentProvider = TTSProvider.Coze;
            else
                Debug.LogError("TTSManager: 未找到任何可用的TTS服务！");
        }
    }
    
    /// <summary>
    /// 获取当前选择的TTS组件
    /// </summary>
    private TTS GetCurrentTTS()
    {
        switch (currentProvider)
        {
            case TTSProvider.Baidu:
                return baiduTTS;
            case TTSProvider.Coze:
                return cozeTTS;
            case TTSProvider.Doubao:
                return doubaoTTS;
            default:
                return null;
        }
    }
    
    /// <summary>
    /// 设置当前使用的TTS服务
    /// </summary>
    public void SetProvider(TTSProvider provider)
    {
        currentProvider = provider;
        ValidateCurrentProvider();
        
        if (enableDebug)
        {
            Debug.Log($"TTSManager: 切换到TTS服务 - {provider}");
        }
    }
    
    /// <summary>
    /// 获取当前使用的TTS服务
    /// </summary>
    public TTSProvider GetProvider()
    {
        return currentProvider;
    }
    
    /// <summary>
    /// 语音合成（统一接口）
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="callback">合成完成回调，返回音频片段和文本</param>
    public void Speak(string text, Action<AudioClip, string> callback)
    {
        if (string.IsNullOrEmpty(text))
        {
            callback?.Invoke(null, "");
            return;
        }
        
        TTS currentTTS = GetCurrentTTS();
        if (currentTTS == null)
        {
            Debug.LogError($"TTSManager: 当前TTS服务 ({currentProvider}) 不可用");
            callback?.Invoke(null, text);
            return;
        }
        
        if (enableDebug)
        {
            Debug.Log($"TTSManager: 使用 {currentProvider} 合成语音: {text}");
        }
        
        // 调用当前TTS服务
        currentTTS.Speak(text, (clip, errorMsg) =>
        {
            // 如果失败且启用自动后备，尝试其他服务
            if (clip == null && enableAutoFallback)
            {
                TryFallbackTTS(text, callback);
            }
            else
            {
                callback?.Invoke(clip, errorMsg ?? text);
            }
        });
    }
    
    /// <summary>
    /// 尝试使用后备TTS服务
    /// </summary>
    private void TryFallbackTTS(string text, Action<AudioClip, string> callback)
    {
        if (enableDebug)
        {
            Debug.Log($"TTSManager: {currentProvider} 合成失败，尝试后备服务");
        }
        
        // 仅尝试百度后备
        if (currentProvider != TTSProvider.Baidu && baiduTTS != null)
        {
            baiduTTS.Speak(text, callback);
            return;
        }
        
        // 所有服务都失败
        Debug.LogError("TTSManager: 所有TTS服务都失败");
        callback?.Invoke(null, text);
    }
    
    #region 流式TTS支持（Coze和Doubao）
    
    /// <summary>
    /// 开始流式会话（用于流式TTS）
    /// </summary>
    public void BeginStream()
    {
        if (currentProvider == TTSProvider.Coze && cozeTTS != null)
        {
            cozeTTS.BeginStream();
        }
        else if (currentProvider == TTSProvider.Doubao && doubaoTTS != null)
        {
            doubaoTTS.BeginStream();
        }
        else
        {
            if (enableDebug)
            {
                Debug.LogWarning($"TTSManager: {currentProvider} 不支持流式TTS");
            }
        }
    }
    
    /// <summary>
    /// 追加流式文本（用于流式TTS）
    /// </summary>
    public void AppendStreamText(string delta)
    {
        if (currentProvider == TTSProvider.Coze && cozeTTS != null)
        {
            cozeTTS.AppendStreamText(delta);
        }
        else if (currentProvider == TTSProvider.Doubao && doubaoTTS != null)
        {
            doubaoTTS.AppendStreamText(delta);
        }
    }
    
    /// <summary>
    /// 完成流式会话（用于流式TTS）
    /// </summary>
    public void CompleteStream()
    {
        if (currentProvider == TTSProvider.Coze && cozeTTS != null)
        {
            cozeTTS.CompleteStream();
        }
        else if (currentProvider == TTSProvider.Doubao && doubaoTTS != null)
        {
            doubaoTTS.CompleteStream();
        }
    }
    
    /// <summary>
    /// 检查当前TTS服务是否支持流式合成
    /// </summary>
    public bool SupportsStreaming()
    {
        return (currentProvider == TTSProvider.Coze && cozeTTS != null) ||
               (currentProvider == TTSProvider.Doubao && doubaoTTS != null);
    }
    
    #endregion
    
    #region 公共属性访问
    
    /// <summary>
    /// 获取百度TTS组件
    /// </summary>
    public BaiduTextToSpeech GetBaiduTTS() => baiduTTS;
    
    /// <summary>
    /// 获取Coze TTS组件
    /// </summary>
    public CozeTextToSpeech GetCozeTTS() => cozeTTS;
    
    /// <summary>
    /// 获取Doubao TTS组件
    /// </summary>
    public DoubaoTextToSpeech GetDoubaoTTS() => doubaoTTS;
    
    /// <summary>
    /// 检查指定服务是否可用
    /// </summary>
    public bool IsProviderAvailable(TTSProvider provider)
    {
        switch (provider)
        {
            case TTSProvider.Baidu:
                return baiduTTS != null;
            case TTSProvider.Coze:
                return cozeTTS != null;
            case TTSProvider.Doubao:
                return doubaoTTS != null;
            default:
                return false;
        }
    }
    
    #endregion
}

