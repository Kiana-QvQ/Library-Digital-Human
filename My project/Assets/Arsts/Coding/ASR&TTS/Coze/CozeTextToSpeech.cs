using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Coze文本转语音实现
/// 继承TTS基类，使用CozeStreamTTS实现流式语音合成
/// </summary>
public class CozeTextToSpeech : TTS
{
    [Header("组件引用")]
    [Tooltip("Coze流式TTS组件")]
    [SerializeField] private CozeStreamTTS streamTTS;
    
    [Tooltip("Coze配置设置（用于获取API Key和音色ID，可选）")]
    [SerializeField] private CozeSettings cozeSettings;
    
    [Tooltip("是否使用音色（如果为false，则不使用音色，使用默认音色）")]
    [SerializeField] private bool useVoiceId = false;
    
    [Header("TTS设置")]
    [Tooltip("是否使用流式合成（推荐）")]
    [SerializeField] private bool useStreamSynthesis = true;
    
    [Tooltip("流式发送的字符数")]
    [SerializeField] private int streamChunkSize = 10;
    
    [Tooltip("音频源（用于播放）")]
    [SerializeField] private AudioSource audioSource;
    
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
        // 自动查找组件
        if (streamTTS == null)
        {
            streamTTS = GetComponent<CozeStreamTTS>();
            if (streamTTS == null)
            {
                streamTTS = gameObject.AddComponent<CozeStreamTTS>();
            }
        }
        
        // 自动查找CozeSettings
        if (cozeSettings == null)
        {
            // 方式1：通过APIManager获取
            APIManager apiManager = APIManager.Instance;
            if (apiManager != null)
                cozeSettings = apiManager.GetCozeSettings();
            
            // 方式2：直接查找
            if (cozeSettings == null)
                cozeSettings = FindObjectOfType<CozeSettings>();
        }
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        // 注册流式TTS回调
        if (streamTTS != null)
        {
            streamTTS.OnAudioReceived += OnAudioReceived;
            streamTTS.OnSynthesisCompleted += OnSynthesisCompleted;
            streamTTS.OnError += OnTTSError;
        }
    }
    
    private void Start()
    {
        // 同步API Key和音色ID
        if (cozeSettings != null)
        {
            if (streamTTS != null)
            {
                string apiKey = cozeSettings.GetAPIKey();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    streamTTS.SetAPIKey(apiKey);
                }
                
                // 只有在启用音色时才设置
                if (useVoiceId)
                {
                    string voiceId = cozeSettings.GetVoiceId();
                    if (!string.IsNullOrEmpty(voiceId))
                    {
                        streamTTS.SetVoiceId(voiceId);
                    }
                }
                else
                {
                    // 禁用音色，使用默认音色
                    streamTTS.DisableVoiceId();
                }
            }
            
            // 监听音色变化
            cozeSettings.OnVoiceChanged += OnVoiceChanged;
        }
        else if (streamTTS != null)
        {
            // 如果没有配置设置，禁用音色
            streamTTS.DisableVoiceId();
        }
    }
    
    /// <summary>
    /// 开始流式会话（保证连接，重置状态）
    /// </summary>
    public void BeginStream()
    {
        if (streamTTS == null)
        {
            Debug.LogError("CozeTextToSpeech: CozeStreamTTS未设置");
            return;
        }

        if (!streamTTS.IsConnected())
        {
            streamTTS.Connect();
        }

        isStreamingSession = true;
        firstStreamAudioArrived = false;
    }

    /// <summary>
    /// 追加流式文本片段
    /// </summary>
    public void AppendStreamText(string delta)
    {
        if (string.IsNullOrEmpty(delta) || streamTTS == null)
        {
            return;
        }

        if (!isStreamingSession)
        {
            // 自动开启流式会话
            BeginStream();
        }

        streamTTS.AppendText(delta);
    }

    /// <summary>
    /// 结束流式会话
    /// </summary>
    public void CompleteStream()
    {
        if (!isStreamingSession || streamTTS == null)
        {
            return;
        }

        streamTTS.CompleteText();
        isStreamingSession = false;
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
            Debug.LogError("CozeTextToSpeech: CozeStreamTTS未设置");
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
            Debug.Log($"CozeTextToSpeech: 开始合成语音: {_msg}");
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
            Debug.LogError("CozeTextToSpeech: CozeStreamTTS未设置");
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
            Debug.Log($"CozeTextToSpeech: 开始合成语音: {_msg}");
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
            Debug.LogError("CozeTextToSpeech: 连接超时");
            OnTTSError("连接超时");
        }
    }
    
    /// <summary>
    /// 开始合成
    /// </summary>
    private void StartSynthesis(string text)
    {
        if (useStreamSynthesis)
        {
            // 流式合成
            streamTTS.SynthesizeText(text, streamChunkSize);
        }
        else
        {
            // 非流式：一次性发送
            streamTTS.AppendText(text);
            streamTTS.CompleteText();
        }
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

        // 标记流式首包到达
        if (isStreamingSession && !firstStreamAudioArrived)
        {
            firstStreamAudioArrived = true;
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
            Debug.Log("CozeTextToSpeech: 语音合成完成");
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
        Debug.LogError($"CozeTextToSpeech: TTS错误: {error}");
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
    /// 音色变化回调
    /// </summary>
    private void OnVoiceChanged(string voiceId, string voiceName)
    {
        if (streamTTS != null && useVoiceId)
        {
            streamTTS.SetVoiceId(voiceId);
            if (enableDebug)
            {
                Debug.Log($"CozeTextToSpeech: 音色已更新 - {voiceName} ({voiceId})");
            }
        }
        else if (streamTTS != null)
        {
            // 如果禁用了音色，确保不使用音色
            streamTTS.DisableVoiceId();
        }
    }
    
    private void OnDestroy()
    {
        if (streamTTS != null)
        {
            streamTTS.OnAudioReceived -= OnAudioReceived;
            streamTTS.OnSynthesisCompleted -= OnSynthesisCompleted;
            streamTTS.OnError -= OnTTSError;
        }
        
        if (cozeSettings != null)
        {
            cozeSettings.OnVoiceChanged -= OnVoiceChanged;
        }
    }
}
