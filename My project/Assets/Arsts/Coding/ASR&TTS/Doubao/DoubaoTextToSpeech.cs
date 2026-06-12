using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Doubao（火山引擎）文本转语音实现
/// 继承TTS基类，使用DoubaoStreamTTS实现流式语音合成
/// </summary>
public class DoubaoTextToSpeech : TTS
{
    [Header("组件引用")]
    [Tooltip("Doubao流式TTS组件")]
    [SerializeField] private DoubaoStreamTTS streamTTS;

    [Tooltip("Doubao V3 双向流式TTS组件（用于流式追加文本）")]
    [SerializeField] private DoubaoStreamTTSV3 streamTTSV3;
    
    [Tooltip("Doubao配置设置（用于获取凭证和speaker_id）")]
    [SerializeField] private DoubaoSettings doubaoSettings;
    
    [Header("TTS设置")]
    [Tooltip("音频源（用于播放）")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("是否优先使用V3流式模式（支持多次追加文本）")]
    [SerializeField] private bool preferV3Streaming = true;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // 当前合成状态
    private bool isSynthesizing = false;
    private Action<AudioClip> currentCallback;
    private Action<AudioClip, string> currentCallbackWithText;
    private string currentText = "";
    private AudioClip currentAudioClip = null;

    // 流式会话状态
    private bool isStreamingSession = false;
    private bool firstStreamAudioArrived = false;
    
    private void Awake()
    {
        // 查找 DoubaoSettings（优先从 APIManager 获取）
        if (doubaoSettings == null)
        {
            APIManager apiManager = APIManager.Instance;
            if (apiManager != null)
                doubaoSettings = apiManager.GetDoubaoSettings();
            
            if (doubaoSettings == null)
                doubaoSettings = FindObjectOfType<DoubaoSettings>();
        }
        
        // 从 DoubaoSettings 获取组件引用（统一管理，避免重复查找）
        if (doubaoSettings != null)
        {
            streamTTS = doubaoSettings.GetStreamTTS();
            streamTTSV3 = doubaoSettings.GetStreamTTSV3();
            
            // 监听凭证和说话人ID变化事件（自动同步）
            doubaoSettings.OnCredentialsChanged += OnCredentialsChanged;
            doubaoSettings.OnSpeakerIdChanged += OnSpeakerIdChanged;
            
            // 确保 Settings 已初始化（如果还没有）
            doubaoSettings.SyncIfNeeded();
        }
        
        // 如果 Settings 中没有组件引用，才自己查找（向后兼容）
        if (streamTTS == null)
            streamTTS = GetComponent<DoubaoStreamTTS>();
        if (streamTTSV3 == null)
            streamTTSV3 = GetComponent<DoubaoStreamTTSV3>();
        
        // AudioSource 管理
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // 注册流式TTS回调
        RegisterCallbacks();
    }
    
    /// <summary>
    /// 注册组件回调
    /// </summary>
    private void RegisterCallbacks()
    {
        if (streamTTS != null)
        {
            streamTTS.OnAudioReceived += OnAudioReceived;
            streamTTS.OnSynthesisCompleted += OnSynthesisCompleted;
            streamTTS.OnError += OnTTSError;
        }
        if (streamTTSV3 != null)
        {
            streamTTSV3.OnAudioReceived += OnAudioReceived;
            streamTTSV3.OnSynthesisCompleted += OnSynthesisCompleted;
            streamTTSV3.OnError += OnTTSError;
        }
    }
    
    /// <summary>
    /// 凭证变化回调（DoubaoSettings 已自动同步，这里主要用于日志）
    /// </summary>
    private void OnCredentialsChanged(string appId, string token)
                {
        if (enableDebug)
            {
            Debug.Log("DoubaoTextToSpeech: 凭证已更新（由 DoubaoSettings 自动同步）");
        }
    }
    
    /// <summary>
    /// 语音合成，返回合成音频
    /// </summary>
    public override void Speak(string _msg, Action<AudioClip> _callback)
    {
        if (string.IsNullOrEmpty(_msg))
        {
            _callback?.Invoke(null);
            return;
        }
        
        if (streamTTS == null)
        {
            Debug.LogError("DoubaoTextToSpeech: DoubaoStreamTTS未设置");
            _callback?.Invoke(null);
            return;
        }
        
        currentText = _msg;
        currentCallback = _callback;
        currentCallbackWithText = null;
        isSynthesizing = true;
        currentAudioClip = null;
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoTextToSpeech: 开始合成语音: {_msg}");
        }
        
        // 确保连接
        if (!streamTTS.IsConnected())
        {
            streamTTS.Connect();
            // 等待连接完成
            StartCoroutine(WaitForConnectionAndSynthesize(_msg));
        }
        else
        {
            StartSynthesis(_msg);
        }
    }
    
    /// <summary>
    /// 语音合成，返回合成音频和文本
    /// </summary>
    public override void Speak(string _msg, Action<AudioClip, string> _callback)
    {
        if (string.IsNullOrEmpty(_msg))
        {
            _callback?.Invoke(null, "");
            return;
        }
        
        if (streamTTS == null)
        {
            Debug.LogError("DoubaoTextToSpeech: DoubaoStreamTTS未设置");
            _callback?.Invoke(null, "");
            return;
        }
        
        currentText = _msg;
        currentCallback = null;
        currentCallbackWithText = _callback;
        isSynthesizing = true;
        currentAudioClip = null;
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoTextToSpeech: 开始合成语音: {_msg}");
        }
        
        // 确保连接
        if (!streamTTS.IsConnected())
        {
            streamTTS.Connect();
            // 等待连接完成
            StartCoroutine(WaitForConnectionAndSynthesize(_msg));
        }
        else
        {
            StartSynthesis(_msg);
        }
    }

    /// <summary>
    /// 开始流式会话（V3优先）
    /// </summary>
    public void BeginStream()
    {
        var target = preferV3Streaming && streamTTSV3 != null ? (object)streamTTSV3 : streamTTS as object;
        if (target == null)
        {
            Debug.LogWarning("DoubaoTextToSpeech: 未找到可用的流式TTS组件");
            return;
        }

        if (target is DoubaoStreamTTSV3 v3)
        {
            if (!v3.IsConnected()) v3.Connect();
            v3.BeginStream();
        }
        else if (target is DoubaoStreamTTS v1)
        {
            if (!v1.IsConnected()) v1.Connect();
            // v1 不支持多次追加，只能一次性文本；此处保持兼容，不做追加
        }

        isStreamingSession = true;
        firstStreamAudioArrived = false;
    }

    /// <summary>
    /// 追加流式文本
    /// </summary>
    public void AppendStreamText(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        var target = preferV3Streaming && streamTTSV3 != null ? (object)streamTTSV3 : streamTTS as object;
        if (target is DoubaoStreamTTSV3 v3)
        {
            if (!isStreamingSession) BeginStream();
            v3.AppendStreamText(delta);
        }
        else if (target is DoubaoStreamTTS v1)
        {
            // v1 不支持多次追加，此处不执行
            Debug.LogWarning("DoubaoStreamTTS (v1) 不支持流式追加文本");
        }
    }

    /// <summary>
    /// 完成流式会话
    /// </summary>
    public void CompleteStream()
    {
        if (!isStreamingSession)
        {
            return;
        }

        var target = preferV3Streaming && streamTTSV3 != null ? (object)streamTTSV3 : streamTTS as object;
        if (target is DoubaoStreamTTSV3 v3)
        {
            v3.CompleteStream();
        }
        else if (target is DoubaoStreamTTS v1)
        {
            // v1 无完成操作
        }

        isStreamingSession = false;
    }
    
    /// <summary>
    /// 等待连接并合成
    /// </summary>
    private IEnumerator WaitForConnectionAndSynthesize(string text)
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!streamTTS.IsConnected() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (streamTTS.IsConnected())
        {
            StartSynthesis(text);
        }
        else
        {
            Debug.LogError("DoubaoTextToSpeech: 连接超时");
            OnTTSError("连接超时");
        }
    }
    
    /// <summary>
    /// 开始合成
    /// </summary>
    private void StartSynthesis(string text)
    {
        streamTTS.SynthesizeText(text);
    }
    
    /// <summary>
    /// 收到音频回调
    /// </summary>
    private void OnAudioReceived(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return;
        }
        
        // 合并音频片段（如果有多个）
        if (currentAudioClip == null)
        {
            currentAudioClip = audioClip;
        }
        else
        {
            // 合并音频（简化处理，实际可以更复杂）
            currentAudioClip = audioClip; // 暂时使用最新的
        }
        
        // 立即播放（流式播放）
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }
    
    /// <summary>
    /// 合成完成回调
    /// </summary>
    private void OnSynthesisCompleted()
    {
        isSynthesizing = false;
        
        if (enableDebug)
        {
            Debug.Log("DoubaoTextToSpeech: 语音合成完成");
        }
        
        // 调用回调
        if (currentCallback != null)
        {
            currentCallback?.Invoke(currentAudioClip);
        }
        
        if (currentCallbackWithText != null)
        {
            currentCallbackWithText?.Invoke(currentAudioClip, currentText);
        }
        
        // 清理
        currentText = "";
        currentCallback = null;
        currentCallbackWithText = null;
    }
    
    /// <summary>
    /// TTS错误回调
    /// </summary>
    private void OnTTSError(string error)
    {
        Debug.LogError($"DoubaoTextToSpeech: TTS错误: {error}");
        isSynthesizing = false;
        
        // 调用回调（传递null表示失败）
        currentCallback?.Invoke(null);
        currentCallbackWithText?.Invoke(null, currentText);
        
        // 清理
        currentText = "";
        currentCallback = null;
        currentCallbackWithText = null;
    }
    
    /// <summary>
    /// Speaker ID变化回调
    /// </summary>
    private void OnSpeakerIdChanged(string speakerId, string speakerName)
    {
        if (streamTTS != null)
        {
            streamTTS.SetSpeakerId(speakerId);
        }
        if (streamTTSV3 != null)
        {
            streamTTSV3.SetSpeakerId(speakerId);
        }
        if (enableDebug)
        {
            Debug.Log($"DoubaoTextToSpeech: Speaker ID已更新 - {speakerName} ({speakerId})");
        }
    }
    
    private void OnDestroy()
    {
        // 取消注册组件回调
        if (streamTTS != null)
        {
            streamTTS.OnAudioReceived -= OnAudioReceived;
            streamTTS.OnSynthesisCompleted -= OnSynthesisCompleted;
            streamTTS.OnError -= OnTTSError;
        }
        if (streamTTSV3 != null)
        {
            streamTTSV3.OnAudioReceived -= OnAudioReceived;
            streamTTSV3.OnSynthesisCompleted -= OnSynthesisCompleted;
            streamTTSV3.OnError -= OnTTSError;
        }
        
        // 取消注册 Settings 事件
        if (doubaoSettings != null)
        {
            doubaoSettings.OnCredentialsChanged -= OnCredentialsChanged;
            doubaoSettings.OnSpeakerIdChanged -= OnSpeakerIdChanged;
        }
    }
}
