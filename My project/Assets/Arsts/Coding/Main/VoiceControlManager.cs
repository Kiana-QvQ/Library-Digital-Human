using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Windows.Speech;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class VoiceControlManager : MonoBehaviour
{
    private const string LocalOnlyResponse = "当前版本已断开大模型、知识库与视觉能力，仅保留本地前端、Unity唤醒、录音、文字输入以及百度语音识别和语音合成。";
    [Header("TTS统一管理")]
    [Tooltip("TTS统一管理器（挂载TTS GameObject）")]
    public TTSManager ttsManager;

    [Header("语音识别组件")]
    public BaiduSpeechToText speechToText;
    public BaiduInTimeVoice baiduInTimeVoice;

    [Tooltip("是否启用流式LLM+TTS（边生成边播）")]
    public bool useCozeStreaming = false;
    
    [Tooltip("是否启用语音克隆功能（默认关闭，需要手动开启）")]
    public bool enableVoiceClone = false;

    [Header("Unity语音识别")]
    [Tooltip("Unity语音识别关键词")]
    public string[] keywords = { "小美小美" };
    
    [Tooltip("Unity语音识别置信度")]
    public ConfidenceLevel confidenceLevel = ConfidenceLevel.Medium;

    [Header("UI组件")]
    public TextMeshProUGUI resultText;
    public Button startButton;
    public Button stopButton;
    public TMP_InputField inputField;
    public Button sendButton;
    public Button realTimeButton;

    [Header("对话滚动（可选）")]
    [Tooltip("若对话输出放在 Scroll View 中，请将该 ScrollRect 拖入以启用自动滚动到底部。")]
    public ScrollRect chatScrollRect;

    [Tooltip("是否启用对话自动滚动到底部")]
    public bool autoScrollEnabled = true;

    [Tooltip("仅当滚动条接近底部时才自动滚动（避免用户翻阅历史时被强制拉回底部）")]
    public bool autoScrollOnlyWhenNearBottom = true;

    [Range(0f, 0.2f)]
    [Tooltip("判断“接近底部”的阈值（verticalNormalizedPosition <= 阈值 视为接近底部；0 为底部，1 为顶部）")]
    public float autoScrollNearBottomThreshold = 0.02f;

    [Header("可选：使用 ButtonMouseClick / Legacy UI 替代标准组件")]
    [Tooltip("若使用 ButtonMouseClick，请在 Inspector 中将对应按钮拖到以下字段；脚本会自动在 eventClick 中注册监听，并通过 ActivationInteractive 控制交互。")]
    public ButtonMouseClick startButtonClick;
    public ButtonMouseClick stopButtonClick;
    public ButtonMouseClick sendButtonClick;
    public ButtonMouseClick realTimeButtonClick;

    [Tooltip("若场景使用旧版 InputField，请在此处拖入")]
    public InputField inputFieldLegacy;

    [Tooltip("若场景使用旧版 Text 作为对话输出，请在此处拖入")]
    public Text resultTextLegacy;

    [Tooltip("是否使用唤醒词功能")]
    public bool useWakeWordToggle = false;
    
    [Tooltip("是否使用实时对话功能")]
    public bool useRealTimeToggle = false;

    [Header("测试模式（无麦克风时使用）")]
    [Tooltip("启用测试模式，无需麦克风即可测试")]
    public bool enableTestMode = false;
    
    [Tooltip("测试模式：模拟唤醒词按钮")]
    public Button testWakeWordButton;
    
    [Tooltip("测试模式：模拟识别结果输入框")]
    public TMP_InputField testRecognitionInput;
    
    [Tooltip("测试模式：发送模拟识别结果按钮")]
    public Button testSendRecognitionButton;

    [Header("音频组件")]
    public AudioSource audioSource;
    public AudioClip audioClipToPlay;

    [Header("AI对话")]
    public CozeAgentClient cozeAgentClient;

    [Header("后端聊天设置")]
    [Tooltip("是否通过后端 /api/chat 转发至学校大模型")]
    [SerializeField] private bool useBackendForChat = true;

    [Tooltip("后端聊天接口地址，需与 digital_human_backend 的 PORT 一致")]
    [SerializeField] private string backendChatUrl = "http://127.0.0.1:8173/api/chat";

    [Tooltip("保留字段，转发模式下由后端固定模型，此处可不填")]
    [SerializeField] private string backendChatModelKey = "Qwen";

    [Header("API管理")]
    [Tooltip("统一的API管理器")]
    public APIManager apiManager;

    [Header("动画控制")]
    [Tooltip("动画控制器组件（自动创建）")]
    private AnimationController animationController;
    
    [Tooltip("角色Animator组件，用于控制动画状态转换（可选，如果未设置会自动查找）")]
    [SerializeField] private Animator characterAnimator;

    [Header("语音检测参数")]
    [Tooltip("开始说话的音量阈值")]
    public float startSpeakingThreshold = 0.05f;

    [Tooltip("停止说话的音量阈值(滞后效应)")]
    public float stopSpeakingThreshold = 0.03f;

    [Tooltip("音频采样的大小")]
    public int sampleSize = 1024;

    [Header("对话模式参数")]
    [Tooltip("唤醒后等待用户开始说话的超时时间(秒)")]
    public float waitUserSpeechTimeout = 15f;

    [Tooltip("用户说话最大持续时间(秒)")]
    public float maxUserSpeechDuration = 15f;

    [Tooltip("判定说话结束所需的静音时长(秒)")]
    public float speechEndSilenceDuration = 1.2f;

    private AudioClip recordingClip;
    private bool isRecording = false;
    private bool isUserSpeaking = false;
    private bool isWakeWordDetected = false;
    private string wakeWord = "小美小美";
    private bool isWaitingForWakeWord = true;
    private bool isInDialogueMode = false;
    private bool hasCapturedDialogueInSession = false;
    private bool isCapturingDialogueSegment = false;
    private int dialogueSegmentStartPos = 0;
    private float currentSpeechDuration = 0f;
    private float currentSilenceDuration = 0f;
    private Coroutine dialogueTimeoutCoroutine;

    // 流式相关
    private string streamingAccumulatedText = "";
    private bool streamingAnswerStarted = false;

    // Unity语音识别相关
    private KeywordRecognizer keywordRecognizer;
    private bool isUnityWakeWordEnabled = true;
    private bool isRealTimeRecognitionActive = false;
    
    // TTS播放状态，用于防止TTS音频被识别为用户输入
    private bool isTTSPlaying = false;
    private bool llmFeaturesDisabled = false;

    // 后端会话ID（可选，用于与后端保持上下文）
    private string backendSessionId = null;

    [System.Serializable]
    private class BackendChatRequest
    {
        public string message;
        public string user_id;
        public string session_id;
        public string system_prompt;
        public string model_key;
        public int memory_profile;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string kb_id;
    }

    [System.Serializable]
    private class BackendChatResponse
    {
        public string response;
        public string user_message;
        public string session_id;
    }

    #region UI 统一封装（支持 Button/ButtonMouseClick、TMP/Legacy）

    private void SetStartButtonInteractable(bool value)
    {
        if (startButton != null) startButton.interactable = value;
        else if (startButtonClick != null) startButtonClick.ActivationInteractive(value);
    }

    private void SetStopButtonInteractable(bool value)
    {
        if (stopButton != null) stopButton.interactable = value;
        else if (stopButtonClick != null) stopButtonClick.ActivationInteractive(value);
    }

    private void SetSendButtonInteractable(bool value)
    {
        if (sendButton != null) sendButton.interactable = value;
        else if (sendButtonClick != null) sendButtonClick.ActivationInteractive(value);
    }

    private void SetRealTimeButtonInteractable(bool value)
    {
        if (realTimeButton != null) realTimeButton.interactable = value;
        else if (realTimeButtonClick != null) realTimeButtonClick.ActivationInteractive(value);
    }

    private string GetInputText()
    {
        if (inputField != null) return inputField.text;
        if (inputFieldLegacy != null) return inputFieldLegacy.text;
        return string.Empty;
    }

    private void SetInputText(string text)
    {
        if (inputField != null) inputField.text = text;
        else if (inputFieldLegacy != null) inputFieldLegacy.text = text;
    }

    private string GetResultText()
    {
        if (resultText != null) return resultText.text;
        if (resultTextLegacy != null) return resultTextLegacy.text;
        return string.Empty;
    }

    private void SetResultText(string text)
    {
        string before = GetResultText();
        bool wasEmpty = string.IsNullOrEmpty(before);

        if (resultText != null) resultText.text = text;
        else if (resultTextLegacy != null) resultTextLegacy.text = text;

        // 首次写入时强制滚到底部；后续更新按“接近底部”策略
        RequestAutoScroll(force: wasEmpty);
    }

    private void AppendResultText(string text)
    {
        string before = GetResultText();
        bool wasEmpty = string.IsNullOrEmpty(before);

        if (resultText != null) resultText.text += text;
        else if (resultTextLegacy != null) resultTextLegacy.text += text;

        // 首次写入时强制滚到底部；后续追加按“接近底部”策略
        RequestAutoScroll(force: wasEmpty);
    }

    private bool HasResultText => resultText != null || resultTextLegacy != null;

    #region AutoScroll

    private Coroutine autoScrollCoroutine;
    private bool pendingForceAutoScroll;

    private void RequestAutoScroll(bool force)
    {
        if (!autoScrollEnabled) return;
        if (chatScrollRect == null) return;

        pendingForceAutoScroll |= force;

        if (autoScrollCoroutine == null)
        {
            autoScrollCoroutine = StartCoroutine(AutoScrollNextFrame());
        }
    }

    private IEnumerator AutoScrollNextFrame()
    {
        // 等布局在本帧末/下一帧更新（Content Size Fitter / Layout Group 通常延迟更新）
        yield return null;

        try
        {
            bool shouldScroll;
            if (pendingForceAutoScroll || !autoScrollOnlyWhenNearBottom)
            {
                shouldScroll = true;
            }
            else
            {
                // 0 = 底部, 1 = 顶部
                shouldScroll = chatScrollRect.verticalNormalizedPosition <= autoScrollNearBottomThreshold;
            }

            if (shouldScroll)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
            }
        }
        finally
        {
            pendingForceAutoScroll = false;
            autoScrollCoroutine = null;
        }
    }

    #endregion

    #endregion

    private void Awake()
    {
        InitializeUIComponents();
    }

    private void Start()
    {
        InitializeServices();
        InitializeUnitySpeechRecognition();

        // 默认关闭唤醒模式，用户可以点击所有按钮
        useWakeWordToggle = false;
        useRealTimeToggle = false;
        EnableNormalMode();
    }

    private void InitializeUIComponents()
    {
        // 开始录音按钮
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartManualRecording);
            SetStartButtonInteractable(true);
        }
        else if (startButtonClick != null)
        {
            startButtonClick.eventClick.AddListener(StartManualRecording);
            SetStartButtonInteractable(true);
        }
        else
        {
            Debug.LogWarning("未找到开始录音按钮（Button 或 ButtonMouseClick）");
        }

        // 停止录音按钮
        if (stopButton != null)
        {
            stopButton.onClick.AddListener(StopManualRecording);
            SetStopButtonInteractable(false);
        }
        else if (stopButtonClick != null)
        {
            stopButtonClick.eventClick.AddListener(StopManualRecording);
            SetStopButtonInteractable(false);
        }
        else
        {
            Debug.LogWarning("未找到停止录音按钮（Button 或 ButtonMouseClick）");
        }

        // 发送按钮
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendInputMessage);
            SetSendButtonInteractable(true);
        }
        else if (sendButtonClick != null)
        {
            sendButtonClick.eventClick.AddListener(SendInputMessage);
            SetSendButtonInteractable(true);
        }
        else
        {
            Debug.LogWarning("未找到发送按钮（Button 或 ButtonMouseClick）");
        }
        
        // 唤醒模式按钮
        if (realTimeButton != null)
        {
            realTimeButton.onClick.AddListener(ToggleWakeWordMode);
            SetRealTimeButtonInteractable(true);
        }
        else if (realTimeButtonClick != null)
        {
            realTimeButtonClick.eventClick.AddListener(ToggleWakeWordMode);
            SetRealTimeButtonInteractable(true);
        }
        else
        {
            Debug.LogWarning("未找到实时对话按钮（Button 或 ButtonMouseClick）");
        }

        // 初始化测试模式UI
        if (enableTestMode)
        {
            InitializeTestModeUI();
        }
    }

    /// <summary>
    /// 初始化测试模式UI
    /// </summary>
    private void InitializeTestModeUI()
    {
        if (testWakeWordButton != null)
        {
            testWakeWordButton.onClick.AddListener(SimulateWakeWordDetected);
            testWakeWordButton.interactable = true;
            Debug.Log("测试模式：唤醒词按钮已初始化");
        }

        if (testSendRecognitionButton != null)
        {
            testSendRecognitionButton.onClick.AddListener(SendTestRecognitionResult);
            testSendRecognitionButton.interactable = true;
            Debug.Log("测试模式：发送识别结果按钮已初始化");
        }

        Debug.Log("测试模式已启用 - 无需麦克风即可测试");
    }

    private void InitializeServices()
    {
        // 学校大模型经后端纯转发，不走 Coze 直连
        llmFeaturesDisabled = false;
        useCozeStreaming = false;
        if (!useBackendForChat)
        {
            useBackendForChat = true;
        }

        Debug.Log(
            $"[API Settings] 后端聊天已启用: useBackendForChat={useBackendForChat}, url={backendChatUrl}",
            this
        );

        if (baiduInTimeVoice == null)
            baiduInTimeVoice = GetComponent<BaiduInTimeVoice>();

        if (cozeAgentClient == null)
            cozeAgentClient = FindObjectOfType<CozeAgentClient>();
        
        // 如果没有指定TTSManager，尝试查找
        if (ttsManager == null)
        {
            ttsManager = FindObjectOfType<TTSManager>();
            if (ttsManager == null)
            {
                Debug.LogWarning("VoiceControlManager: 未找到TTSManager，请确保已创建TTS GameObject并挂载TTSManager组件");
            }
        }
        
        // 初始化统一的API管理器
        if (apiManager == null)
        {
            apiManager = APIManager.Instance;
            if (apiManager == null)
            {
                GameObject managerObj = new GameObject("APIManager");
                apiManager = managerObj.AddComponent<APIManager>();
            }
        }
        
        // 同步组件引用到Settings
        if (apiManager != null)
        {
            // 处理Coze配置
            CozeSettings cozeSettings = apiManager.GetCozeSettings();
            if (cozeSettings != null)
            {
                // 从TTSManager获取CozeTextToSpeech组件
                if (ttsManager != null)
                {
                    CozeTextToSpeech cozeTTS = ttsManager.GetCozeTTS();
                    if (cozeTTS != null)
                    {
                        CozeStreamTTS streamTTS = cozeTTS.GetComponent<CozeStreamTTS>();
                        if (streamTTS != null)
                        {
                            cozeSettings.SetStreamTTS(streamTTS);
                        }
                    }
                }
            }
            
            // 处理Doubao配置
            DoubaoSettings doubaoSettings = apiManager.GetDoubaoSettings();
            if (doubaoSettings != null)
            {
                // 同步语音克隆开关
                doubaoSettings.SetVoiceCloneEnabled(enableVoiceClone);
                
                // 从TTSManager获取DoubaoTextToSpeech组件
                if (ttsManager != null)
                {
                    DoubaoTextToSpeech doubaoTTS = ttsManager.GetDoubaoTTS();
                    if (doubaoTTS != null)
                    {
                        DoubaoStreamTTS streamTTS = doubaoTTS.GetComponent<DoubaoStreamTTS>();
                        if (streamTTS != null)
                        {
                            doubaoSettings.SetStreamTTS(streamTTS);
                        }
                        
                        DoubaoStreamTTSV3 streamTTSV3 = doubaoTTS.GetComponent<DoubaoStreamTTSV3>();
                        if (streamTTSV3 != null)
                        {
                            doubaoSettings.SetStreamTTSV3(streamTTSV3);
                        }
                    }
                }
            }
        }

        // 固定 TTS 走百度
        if (ttsManager != null)
        {
            ttsManager.ForceBaiduProvider();
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // 将人物 AudioSource 同步给 CozeAgentClient，使流式 TTS 也通过同一音源播放（便于唇形同步）
        // LLM/Coze chain disabled: do not sync streaming audio output.

        // 初始化动画控制器
        animationController = GetComponent<AnimationController>();
        if (animationController == null)
        {
            animationController = gameObject.AddComponent<AnimationController>();
        }
        animationController.Initialize(characterAnimator);
        
        // 订阅动画状态变化事件，用于记录日志
        animationController.OnAnimationStateChanged = (state, stateName) =>
        {
            LogSystemEvent($"动画状态切换: {stateName}");
        };
    }

    /// <summary>
    /// 初始化Unity语音识别
    /// </summary>
    private void InitializeUnitySpeechRecognition()
    {
        try
        {
            if (keywords != null && keywords.Length > 0)
            {
                keywordRecognizer = new KeywordRecognizer(keywords, confidenceLevel);
                keywordRecognizer.OnPhraseRecognized += OnUnityPhraseRecognized;
                keywordRecognizer.Start();
                Debug.Log("Unity语音识别已启动，关键词: " + string.Join(", ", keywords));
            }
            else
            {
                Debug.LogWarning("未设置Unity语音识别关键词");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Unity语音识别初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Unity语音识别回调
    /// </summary>
    /// <param name="args">识别结果</param>
    private void OnUnityPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"Unity语音识别结果: {args.text}, 置信度: {args.confidence}");
        LogSystemEvent($"Unity语音识别结果: {args.text}, 置信度: {args.confidence}");
        
        // 检查是否匹配唤醒词
        foreach (string keyword in keywords)
        {
            if (args.text.Contains(keyword))
            {
                Debug.Log($"检测到Unity唤醒词: {keyword}");
                LogSystemEvent($"检测到Unity唤醒词: {keyword}");
                HandleUnityWakeWordDetected();
                return;
            }
        }
    }

    /// <summary>
    /// 处理Unity唤醒词检测
    /// </summary>
    private void HandleUnityWakeWordDetected()
    {
        Debug.Log("Unity唤醒词检测成功，准备进入对话模式");
        LogSystemEvent("Unity唤醒词检测成功，准备进入对话模式");
        EnterDialogueMode();
    }

    /// <summary>
    /// 手动启动实时语音识别（按钮触发）
    /// </summary>
    public void StartManualRealTimeRecognition()
    {
        Debug.Log("手动启动实时语音识别");
        LogUserEvent("点击开始实时语音识别按钮");
        
        if (baiduInTimeVoice != null && !isRealTimeRecognitionActive)
        {
            isRealTimeRecognitionActive = true;
            baiduInTimeVoice.StartRealTimeRecognition(OnRealTimeSpeechRecognized);
            Debug.Log("实时语音识别已启动");
            LogSystemEvent("实时语音识别已启动");
        }
        else if (isRealTimeRecognitionActive)
        {
            Debug.Log("实时语音识别已在运行中");
            LogSystemEvent("实时语音识别已在运行中，无需重复启动");
        }
    }

    /// <summary>
    /// 停止实时语音识别
    /// </summary>
    public void StopManualRealTimeRecognition()
    {
        Debug.Log("停止实时语音识别");
        LogUserEvent("点击停止实时语音识别按钮");
        
        if (baiduInTimeVoice != null && isRealTimeRecognitionActive)
        {
            baiduInTimeVoice.StopRealTimeRecognition();
            isRealTimeRecognitionActive = false;
            Debug.Log("实时语音识别已停止");
            LogSystemEvent("实时语音识别已停止");
        }
    }

    /// <summary>
    /// 切换唤醒模式（通过realTimeButton触发）
    /// </summary>
    public void ToggleWakeWordMode()
    {
        useWakeWordToggle = !useWakeWordToggle;
        LogUserEvent($"用户切换唤醒模式 -> {(useWakeWordToggle ? "开启" : "关闭")}");
        useRealTimeToggle = false; // 关闭实时对话模式

        StopRecording();

        if (useWakeWordToggle)
        {
            EnableWakeWordRecognition();
        }
        else
        {
            DisableWakeWordRecognition();
        }
    }

    public void ToggleUseWakeWord()
    {
        useWakeWordToggle = !useWakeWordToggle;
        LogUserEvent($"用户切换唤醒词 -> {(useWakeWordToggle ? "开启" : "关闭")}");
        useRealTimeToggle = false; // 关闭实时对话模式

        StopRecording();

        if (useWakeWordToggle)
        {
            EnableWakeWordRecognition();
        }
        else
        {
            DisableWakeWordRecognition();
        }
    }
    
    public void ToggleRealTimeRecognition()
    {
        useRealTimeToggle = !useRealTimeToggle;
        LogUserEvent($"用户切换实时对话模式 -> {(useRealTimeToggle ? "开启" : "关闭")}");
        useWakeWordToggle = false; // 关闭唤醒词模式
        
        StopRecording();
        
        if (useRealTimeToggle)
        {
            EnableRealTimeRecognition();
        }
        else
        {
            DisableRealTimeRecognition();
        }
    }

    private void EnableWakeWordRecognition()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("未获取麦克风权限，请求权限...");
            Permission.RequestUserPermission(Permission.Microphone);
        }

        // 在唤醒模式下，不启动实时语音识别，只监听唤醒词

        StartContinuousListening();

        // 进入唤醒模式时，禁用其他按钮，但保持唤醒按钮可用
        SetStartButtonInteractable(false);
        SetStopButtonInteractable(false);
        SetRealTimeButtonInteractable(true); // 保持唤醒按钮可用，用于退出
        SetSendButtonInteractable(false);

        isWakeWordDetected = false;
        isWaitingForWakeWord = true; // ��ʼ״̬Ϊ�ȴ����Ѵ�

        isRealTimeRecognitionActive = false; // 确保实时识别未激活
        
        Debug.Log("开启唤醒词识别模式 - 等待唤醒词");
        LogSystemEvent("开启唤醒词识别模式，等待唤醒词");
    }

    /// <summary>
    /// 启用普通模式（默认模式，所有按钮可用）
    /// </summary>
    private void EnableNormalMode()
    {
        // 停止所有录音和识别
        StopRecording();
        
        // 启用所有按钮
        SetStartButtonInteractable(true);
        SetStopButtonInteractable(false);
        SetRealTimeButtonInteractable(true);
        SetSendButtonInteractable(true);

        // 重置状态
        isWakeWordDetected = false;
        isWaitingForWakeWord = false;
        isRealTimeRecognitionActive = false;
        
        Debug.Log("启用普通模式 - 所有按钮可用");
        LogSystemEvent("切换到普通模式，所有按钮可用");
    }

    private void DisableWakeWordRecognition()
    {
        // 停止实时语音识别
        if (baiduInTimeVoice != null)
        {
            baiduInTimeVoice.StopRealTimeRecognition();
        }

        StopContinuousListening();

        // 恢复到普通模式
        EnableNormalMode();

        Debug.Log("关闭唤醒词识别模式，恢复到普通模式");
        LogSystemEvent("关闭唤醒词识别模式，恢复普通模式");
    }
    
    private void EnableRealTimeRecognition()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Debug.Log("未获取麦克风权限，请求权限...");
            Permission.RequestUserPermission(Permission.Microphone);
        }
      
        SetStartButtonInteractable(false);
        SetStopButtonInteractable(false);
        SetRealTimeButtonInteractable(false);
        
        Debug.Log("开启实时对话模式");
        LogSystemEvent("开启实时对话模式");
    }
    
    private void DisableRealTimeRecognition()
    {
        SetStartButtonInteractable(true);
        SetStopButtonInteractable(false);
        SetRealTimeButtonInteractable(true);
        
        Debug.Log("关闭实时对话模式");
        LogSystemEvent("关闭实时对话模式");
    }

    public void StartManualRecording()
    {
        if (!isRecording && Microphone.devices.Length > 0)
        {
            Debug.Log("手动开始录音");
            LogUserEvent("点击开始录音按钮");
            recordingClip = Microphone.Start(null, false, 10, 16000);
            isRecording = true;

            SetStartButtonInteractable(false);
            SetStopButtonInteractable(true);
        }
    }

    public void StopManualRecording()
    {
        if (isRecording)
        {
            Debug.Log("手动停止录音");
            LogUserEvent("点击停止录音按钮");
            Microphone.End(null);
            isRecording = false;

            // 场景2：录音模式 - 用户停止录音时，立即切换到思考状态
            // 因为此时开始处理录音并准备发送消息
            SetAnimationState(1);

            if (speechToText != null)
            {
                speechToText.SpeechToText(recordingClip, OnSpeechRecognized);
            }
            else
            {
                // 如果没有语音识别组件，重置动画状态
                Debug.LogWarning("语音识别组件未设置，无法处理录音");
                SetAnimationState(0);
            }

            SetStartButtonInteractable(true);
            SetStopButtonInteractable(false);
        }
    }

    private void StartContinuousListening()
    {
        // 测试模式：使用模拟音频数据
        if (enableTestMode)
        {
            Debug.Log("测试模式：开始模拟监听");
            LogSystemEvent("测试模式：开始模拟音频监听");
            isRecording = true;
            isUserSpeaking = false;
            isWakeWordDetected = false;
            isWaitingForWakeWord = true;
            
            // 创建模拟的AudioClip用于测试
            recordingClip = AudioClip.Create("TestAudio", 16000 * 10, 1, 16000, false);
            StartCoroutine(MonitorAudioTestMode());
            return;
        }

        // 正常模式：使用真实麦克风
        if (!isRecording && Microphone.devices.Length > 0)
        {
            Debug.Log("开始持续监听");
            LogSystemEvent("开始麦克风持续监听");
            recordingClip = Microphone.Start(null, true, 3599, 16000);
            isRecording = true;
            isUserSpeaking = false;
            isWakeWordDetected = false;
            isWaitingForWakeWord = true;

            StartCoroutine(MonitorAudio());
        }
        else if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("未检测到麦克风设备，请启用测试模式");
            LogSystemEvent("未检测到麦克风设备，请检查硬件或启用测试模式");
        }
    }

    private void StopContinuousListening()
    {
        if (isRecording)
        {
            Debug.Log("停止持续监听");
            LogSystemEvent("停止麦克风持续监听");
            Microphone.End(null);
            isRecording = false;
        }
    }

    private IEnumerator MonitorAudio()
    {
        float[] samples = new float[sampleSize];
        int micPosition = 0;

        bool isCheckingWakeWord = false;
        float wakeWordCheckTime = 0f;
        const float WAKE_WORD_CHECK_DURATION = 2f; 
        int wakeWordCheckStartPos = 0; 

        bool isRecordingInstruction = false;
        int instructionStartPos = 0; 

        while (isRecording)
        {
            int currentPosition = Microphone.GetPosition(null);

            if (currentPosition < micPosition)
                micPosition = 0;

            if (currentPosition - micPosition >= sampleSize)
            {
                recordingClip.GetData(samples, micPosition);
                float rms = CalculateRMS(samples);

                // 将音频数据发送给实时语音识别
                // 在唤醒模式下或Unity唤醒词模式下，即使实时识别还没激活，也要提前缓存音频数据
                // AddAudioData会自动处理缓存逻辑
                if (baiduInTimeVoice != null && (useWakeWordToggle || isWakeWordDetected))
                {
                    byte[] audioData = ConvertSamplesToBytes(samples);
                    baiduInTimeVoice.AddAudioData(audioData);
                }
                
                if (isInDialogueMode)
                {
                    UpdateDialogueRecordingState(rms, micPosition, currentPosition, Time.deltaTime);
                }
                else if (!useWakeWordToggle)
                {
                    if (!isUserSpeaking && rms > startSpeakingThreshold)
                    {
                        Debug.Log("检测到用户开始说话");
                        isUserSpeaking = true;

                        if (isWaitingForWakeWord)
                        {
                            isCheckingWakeWord = true;
                            wakeWordCheckTime = 0f;
                            wakeWordCheckStartPos = micPosition;
                            Debug.Log("开始2秒唤醒词检查");
                        }
                        else
                        {
                            isRecordingInstruction = true;
                            instructionStartPos = micPosition;
                            Debug.Log("开始录音用户指令");
                        }
                    }
                    else if (isUserSpeaking && rms < stopSpeakingThreshold)
                    {
                        Debug.Log("停止录音用户指令");
                        isUserSpeaking = false;

                        if (isRecordingInstruction)
                        {
                            isRecordingInstruction = false;
                            Debug.Log("2秒唤醒词检查结束");
                            ProcessSpeechSegment(instructionStartPos, currentPosition, OnSpeechRecognizedForAI);
                        }
                    }
                }

                // 只在非唤醒词模式下进行传统的唤醒词检查
                if (!useWakeWordToggle && isCheckingWakeWord)
                {
                    wakeWordCheckTime += Time.deltaTime;


                    if (wakeWordCheckTime >= WAKE_WORD_CHECK_DURATION)
                    {
                        isCheckingWakeWord = false;
                        Debug.Log("2秒唤醒词检查结束");

                        // ����2��Ļ��Ѵʼ����Ƶ
                        ProcessSpeechSegment(wakeWordCheckStartPos, currentPosition, (result) => {
                            Debug.Log($"唤醒词检查结果: {result}");


                            // ����Ƿ�������Ѵ�
                            if (result.Contains(wakeWord))
                            {
                                Debug.Log("开始录音用户指令");
                                isWaitingForWakeWord = false;

                                // ������ʾ��
                                if (audioSource != null && audioClipToPlay != null)
                                {
                                    audioSource.clip = audioClipToPlay;
                                    audioSource.Play();
                                }

                                // ����û�����˵������ʼ¼��ָ��
                                if (isUserSpeaking)
                                {
                                    isRecordingInstruction = true;
                                    instructionStartPos = currentPosition; // �ӵ�ǰλ�ÿ�ʼ¼��ָ��
                                    Debug.Log("未检测到唤醒词");
                                }
                            }
                            else
                            {
                                Debug.Log("未检测到唤醒词");
                                // ����û�����˵������������
                            }
                        });
                    }
                }

                micPosition += sampleSize;
            }

            yield return null;
        }
    }

    private void UpdateDialogueRecordingState(float rms, int chunkStartPos, int currentPosition, float deltaTime)
    {
        if (!isInDialogueMode || hasCapturedDialogueInSession)
        {
            return;
        }

        if (!isCapturingDialogueSegment)
        {
            if (rms > startSpeakingThreshold)
            {
                Debug.Log("对话模式：检测到用户开始说话");
                LogSystemEvent("检测到用户开始说话，开始录制对话音频");
                if (dialogueTimeoutCoroutine != null)
                {
                    StopCoroutine(dialogueTimeoutCoroutine);
                    dialogueTimeoutCoroutine = null;
                }
                isCapturingDialogueSegment = true;
                isUserSpeaking = true;
                dialogueSegmentStartPos = chunkStartPos;
                currentSpeechDuration = 0f;
                currentSilenceDuration = 0f;
            }
            return;
        }

        currentSpeechDuration += deltaTime;

        if (rms > stopSpeakingThreshold)
        {
            currentSilenceDuration = 0f;
        }
        else
        {
            currentSilenceDuration += deltaTime;
        }

        if (currentSpeechDuration >= maxUserSpeechDuration)
        {
            Debug.Log("对话模式：达到最长说话时长，结束录音");
            LogSystemEvent("达到最长说话时长，自动结束录音");
            CompleteDialogueSegment(dialogueSegmentStartPos, currentPosition);
            return;
        }

        if (currentSilenceDuration >= speechEndSilenceDuration)
        {
            Debug.Log("对话模式：检测到静音，结束录音");
            LogSystemEvent("检测到静音达到阈值，结束录音");
            CompleteDialogueSegment(dialogueSegmentStartPos, currentPosition);
        }
    }

    private void CompleteDialogueSegment(int startPos, int endPos)
    {
        if (!isCapturingDialogueSegment)
        {
            return;
        }

        isCapturingDialogueSegment = false;
        isUserSpeaking = false;
        hasCapturedDialogueInSession = true;
        currentSpeechDuration = 0f;
        currentSilenceDuration = 0f;

        ProcessSpeechSegment(startPos, endPos, HandleDialogueRecognitionResult);
        LogSystemEvent("完成对话音频截取，提交语音识别");
    }

    private void HandleDialogueRecognitionResult(string result)
    {
        if (string.IsNullOrEmpty(result))
        {
            DisplayAgentResponse("没有听清，请再试一次");
            // 识别失败时，重置动画状态为待机
            SetAnimationState(0);
            ExitDialogueMode();
            return;
        }

        LogSystemEvent($"对话语音识别结果: {result}");
        OnSpeechRecognizedForAI(result);
    }

    // ��������ʶ����������AI��
    private void OnSpeechRecognizedForAI(string result)
    {
        Debug.Log($"AI语音识别结果: {result}");
        LogSystemEvent($"AI语音识别结果: {result}");
        SendMessageToAI(result);
    }

    // �޸ĺ��ProcessSpeechSegment����
    private void ProcessSpeechSegment(int startPos, int endPos, Action<string> callback)
    {
        int length = endPos - startPos;
        if (length <= 0)
        {
            callback?.Invoke("");
            return;
        }

        // ������ʱ��ƵƬ��
        float[] speechData = new float[length];
        recordingClip.GetData(speechData, startPos);

        AudioClip speechClip = AudioClip.Create("SpeechSegment", length, recordingClip.channels, recordingClip.frequency, false);
        speechClip.SetData(speechData, 0);

        if (speechToText != null)
        {
            // 使用包装的回调来确保AudioClip被正确释放
            speechToText.SpeechToText(speechClip, (result) => {
                // 释放AudioClip资源
                if (speechClip != null)
                {
                    DestroyImmediate(speechClip);
                }
                callback?.Invoke(result);
            });
        }
        else
        {
            // 释放AudioClip资源
            if (speechClip != null)
            {
                DestroyImmediate(speechClip);
            }
            callback?.Invoke("");
        }
    }

    private float CalculateRMS(float[] samples)
    {
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// 将音频采样转换为字节数组
    /// </summary>
    /// <param name="samples">音频采样</param>
    /// <returns>字节数组</returns>
    private byte[] ConvertSamplesToBytes(float[] samples)
    {
        var samplesShort = new short[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            samplesShort[index] = (short)(samples[index] * short.MaxValue);
        }
        byte[] audioData = new byte[samplesShort.Length * 2];
        System.Buffer.BlockCopy(samplesShort, 0, audioData, 0, audioData.Length);
        return audioData;
    }

    /// <summary>
    /// 实时语音识别结果回调
    /// </summary>
    /// <param name="result">识别结果</param>
    private void OnRealTimeSpeechRecognized(string result)
    {
        if (string.IsNullOrEmpty(result))
            return;

        // 过滤错误信息，不将错误当作识别结果处理
        if (result.Contains("识别错误") || result.Contains("识别失败") || result.Contains("错误码"))
        {
            Debug.LogWarning($"收到识别错误，不处理: {result}");
            LogSystemEvent($"实时识别错误，不处理: {result}");
            // 识别错误时，重置动画状态为待机（如果之前已经切换到思考状态）
            SetAnimationState(0);
            // 对于-3101错误（等待音频超时），尝试重新启动识别
            if (result.Contains("-3101") || result.Contains("wait audio over time"))
            {
                Debug.LogWarning("检测到音频超时错误，可能需要重新启动识别");
                LogSystemEvent("实时识别音频超时，可能需要重新启动识别");
                // 可以选择自动重试或提示用户
            }
            return;
        }

        // 如果TTS正在播放，忽略识别结果（防止TTS音频被识别为用户输入）
        if (isTTSPlaying)
        {
            Debug.Log($"TTS正在播放，忽略识别结果: {result}");
            LogSystemEvent("TTS播放中，忽略实时识别结果");
            return;
        }

        Debug.Log($"实时语音识别结果: {result}");

        // 显示识别结果到UI
        DisplayUserMessage(result);

        // 如果通过Unity唤醒词启动的实时识别，直接发送给AI
        if (isRealTimeRecognitionActive)
        {
            // 场景3：唤醒模式 - 识别到有效语音并准备发送时，立即切换到思考状态
            SetAnimationState(1);
            SendMessageToAI(result);
        }
        else if (useWakeWordToggle)
        {
            if (isWaitingForWakeWord && result.Contains(wakeWord))
            {
                // 检测到唤醒词，统一由HandleWakeWordDetected处理
                // 唤醒词检测不应该触发思考状态，保持待机状态
                Debug.Log("检测到唤醒词");
                HandleWakeWordDetected();
            }
            else if (!isWaitingForWakeWord)
            {
                // 场景3：唤醒模式 - 在指令模式下，识别到有效语音并准备发送时，立即切换到思考状态
                SetAnimationState(1);
                SendMessageToAI(result);
            }
        }
        else if (isWakeWordDetected && !isWaitingForWakeWord)
        {
            // 场景3：唤醒模式 - Unity唤醒词模式：如果已经唤醒，识别到有效语音并准备发送时，立即切换到思考状态
            SetAnimationState(1);
            // Unity唤醒词模式：如果已经唤醒（isWakeWordDetected为true），即使实时识别还没完全启动，也应该发送给AI
            // 这解决了延迟启动期间无法处理识别结果的问题
            SendMessageToAI(result);
        }
    }

    private void OnSpeechRecognized(string result)
    {

        // ����ʶ��ʧ�ܵ����
        if (string.IsNullOrEmpty(result) || result.Contains("识别失败") || result.Contains("语音识别失败"))
        {
            LogSystemEvent("批量语音识别失败或结果为空");
            
            // 识别失败时，重置动画状态为待机
            SetAnimationState(0);

            if (useWakeWordToggle && isWaitingForWakeWord)
            {
                return;
            }

            // ������ǵȴ����Ѵ�״̬����ʾ������Ϣ
            if (!isWaitingForWakeWord)
            {
                DisplayAgentResponse("抱歉，没有识别到您的语音，请再说一遍");

                // ����ǻ��Ѵ�ģʽ������Ϊ�ȴ����Ѵ�״̬
                if (useWakeWordToggle)
                {
                    isWaitingForWakeWord = true;
                }
            }
            return;
        }

        if (useWakeWordToggle)
        {
            if (isWaitingForWakeWord && result.Contains(wakeWord))
            {
                // 检测到唤醒词，统一由HandleWakeWordDetected处理
                LogSystemEvent("批量识别结果包含唤醒词");
                HandleWakeWordDetected();
            }
            else if (!isWaitingForWakeWord)
            {
                LogSystemEvent($"批量识别结果: {result}");
                SendMessageToAI(result);
            }
        }
        else
        {
            LogSystemEvent($"批量识别结果: {result}");
            SendMessageToAI(result);
        }
    }

    private void HandleWakeWordDetected()
    {
        Debug.Log("检测到唤醒词，准备进入对话模式");
        LogSystemEvent("实时识别检测到唤醒词，准备进入对话模式");
        EnterDialogueMode();
    }

    private void EnterDialogueMode()
    {
        if (!isRecording)
        {
            StartContinuousListening();
        }

        ClearMessageContent();
        LogSystemEvent("进入对话模式，准备提示用户说话");

        if (dialogueTimeoutCoroutine != null)
        {
            StopCoroutine(dialogueTimeoutCoroutine);
            dialogueTimeoutCoroutine = null;
        }

        isWaitingForWakeWord = false;
        isWakeWordDetected = true;
        isInDialogueMode = true;
        hasCapturedDialogueInSession = false;
        isCapturingDialogueSegment = false;
        currentSpeechDuration = 0f;
        currentSilenceDuration = 0f;
        isUserSpeaking = false;

        if (audioSource != null && audioClipToPlay != null)
        {
            isTTSPlaying = true;
            audioSource.clip = audioClipToPlay;
            audioSource.Play();
            StartCoroutine(ResetTTSFlagAfterDelay(audioClipToPlay.length));
            LogTTSEvent("播放唤醒提示音");
        }

        DisplayAgentResponse("你好，有什么可以帮你");

        dialogueTimeoutCoroutine = StartCoroutine(WaitUserSpeechCoroutine());
        LogSystemEvent("启动等待用户说话的超时计时");
    }

    private void ExitDialogueMode()
    {
        if (dialogueTimeoutCoroutine != null)
        {
            StopCoroutine(dialogueTimeoutCoroutine);
            dialogueTimeoutCoroutine = null;
        }

        isInDialogueMode = false;
        isCapturingDialogueSegment = false;
        hasCapturedDialogueInSession = false;
        isUserSpeaking = false;
        currentSpeechDuration = 0f;
        currentSilenceDuration = 0f;

        isWaitingForWakeWord = true;
        isWakeWordDetected = false;

        if (baiduInTimeVoice != null && isRealTimeRecognitionActive)
        {
            baiduInTimeVoice.StopRealTimeRecognition();
            isRealTimeRecognitionActive = false;
        }

        LogSystemEvent("退出对话模式，恢复等待唤醒");
    }

    private IEnumerator WaitUserSpeechCoroutine()
    {
        float elapsed = 0f;
        while (isInDialogueMode && !isCapturingDialogueSegment && !hasCapturedDialogueInSession)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= waitUserSpeechTimeout)
            {
                DisplayAgentResponse("未检测到语音，已退出对话模式");
                LogSystemEvent("等待用户说话超时，退出对话模式");
                ExitDialogueMode();
                yield break;
            }
            yield return null;
        }
    }

    private void SendMessageToAI(string message)
    {
        // 验证消息有效性
        if (string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(message))
        {
            Debug.LogWarning("尝试发送空消息，已忽略");
            LogSystemEvent("尝试发送空消息，已忽略");
            // 消息无效时，重置动画状态（因为之前已经在触发点切换到了思考状态）
            SetAnimationState(0);
            return;
        }

        // 清理消息内容并显示用户消息
        ClearMessageContent();
        LogUserEvent($"向AI发送消息: {message}");
        DisplayUserMessage(message);

        // 本地占位模式：不出网、不进大模型，仍走 TTS / UI / 动画链路
        if (llmFeaturesDisabled)
        {
            Debug.Log("[Chat] LLM disabled, returning local placeholder response.", this);
            LogSystemEvent("大模型已断开，返回本地占位提示");
            OnAgentResponseReceived(LocalOnlyResponse);
            return;
        }

        // 根据配置决定走前端直连还是后端 /api/chat
        if (useBackendForChat)
        {
            Debug.Log(
                $"[Chat] Route=Backend, url='{backendChatUrl}', model_key='{backendChatModelKey}', " +
                $"session_id='{backendSessionId ?? ""}', message_len={message.Length}",
                this
            );
            StartCoroutine(SendMessageToBackendChat(message));
        }
        else
        {
            Debug.Log(
                $"[Chat] Route=Coze, stream={useCozeStreaming}, message_len={message.Length}",
                this
            );
            // 检查AI客户端是否可用（Coze 直连）
            if (cozeAgentClient != null)
            {
                if (useCozeStreaming)
                {
                    streamingAccumulatedText = "";
                    streamingAnswerStarted = false;
                    StartCoroutine(cozeAgentClient.SendStreamMessageToAgent(
                        message,
                        delta => OnAgentStreamDelta(delta),
                        completed => OnAgentStreamCompleted(completed),
                        error => OnAgentStreamError(error)
                    ));
                }
                else
                {
                    StartCoroutine(cozeAgentClient.SendMessageToAgent(message, OnAgentResponseReceived));
                }
            }
            else
            {
                Debug.LogError("CozeAgentClient未设置，无法发送消息");
                LogSystemEvent("CozeAgentClient未设置，无法发送消息");
                DisplayAgentResponse("错误：AI客户端未配置");
                // AI客户端不可用时，重置动画状态
                SetAnimationState(0);
            }
        }
    }

    /// <summary>
    /// 通过后端 /api/chat 发送消息
    /// </summary>
    private IEnumerator SendMessageToBackendChat(string message)
    {
        string preview = message == null
            ? ""
            : (message.Length > 80 ? message.Substring(0, 80) + "..." : message);

        // 构建请求体（纯转发：不传知识库、长期记忆）
        BackendChatRequest request = new BackendChatRequest
        {
            message = message,
            user_id = "unity_user",
            session_id = backendSessionId,
            system_prompt = null,
            model_key = backendChatModelKey,
            memory_profile = 0
        };

        string json = JsonConvert.SerializeObject(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest www = new UnityWebRequest(backendChatUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            LogSystemEvent($"通过后端发送聊天请求: {backendChatUrl}");
            Debug.Log(
                $"[BackendChat] POST {backendChatUrl} session_id='{backendSessionId ?? ""}' " +
                $"message_preview='{preview}'",
                this
            );

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"后端聊天请求失败: {www.error}";
                Debug.LogError(errorMsg);
                LogSystemEvent(errorMsg);
                DisplayAgentResponse("后端聊天服务不可用，请检查 digital_human_backend 是否已启动。");
                SetAnimationState(0);
            }
            else
            {
                string responseText = www.downloadHandler.text;
                Debug.Log(
                    $"[BackendChat] Response OK http={www.responseCode} bytes={(responseText ?? "").Length} session_id='{backendSessionId ?? ""}'",
                    this
                );
                if (string.IsNullOrEmpty(responseText))
                {
                    LogSystemEvent("后端聊天响应为空");
                    DisplayAgentResponse("后端未返回有效回复");
                    SetAnimationState(0);
                    yield break;
                }

                try
                {
                    BackendChatResponse resp = JsonConvert.DeserializeObject<BackendChatResponse>(responseText);
                    if (resp != null && !string.IsNullOrEmpty(resp.response))
                    {
                        // 更新会话ID，保持与后端上下文一致
                        string prevSessionId = backendSessionId;
                        backendSessionId = resp.session_id;
                        if (!string.Equals(prevSessionId, backendSessionId, StringComparison.Ordinal))
                        {
                            Debug.Log(
                                $"[BackendChat] session_id updated: '{prevSessionId ?? ""}' -> '{backendSessionId ?? ""}'",
                                this
                            );
                            LogSystemEvent($"后端会话ID更新: '{prevSessionId ?? ""}' -> '{backendSessionId ?? ""}'");
                        }
                        OnAgentResponseReceived(resp.response);
                    }
                    else
                    {
                        LogSystemEvent("后端聊天响应格式错误，未找到 response 字段");
                        DisplayAgentResponse("后端返回的聊天数据格式不正确");
                        SetAnimationState(0);
                    }
                }
                catch (System.Exception e)
                {
                    string errorMsg = $"解析后端聊天响应失败: {e.Message}";
                    Debug.LogError(errorMsg);
                    LogSystemEvent(errorMsg);
                    DisplayAgentResponse("解析后端响应失败");
                    SetAnimationState(0);
                }
            }
        }
    }

    private void OnAgentResponseReceived(string response)
    {
        // 检查响应是否有效（不是错误消息）
        if (string.IsNullOrEmpty(response))
        {
            Debug.LogWarning("收到空的AI回复");
            LogSystemEvent("收到空的AI回复");
            DisplayAgentResponse("抱歉，没有收到有效回复");
            // 空回复时重置动画状态
            SetAnimationState(0);
            return;
        }

        // 检查是否是错误响应
        if (response.Contains("错误") || response.Contains("错误码") || response.Contains("失败") || 
            response.Contains("Error") || response.Contains("error") || response.StartsWith("错误:"))
        {
            Debug.LogWarning($"收到错误响应: {response}");
            LogSystemEvent($"收到错误响应: {response}");
            DisplayAgentResponse(response);
            // 错误响应时重置动画状态
            SetAnimationState(0);
            return;
        }

        DisplayAgentResponse(response);
        LogSystemEvent("收到AI文本回复");

        // 开始生成文字和语音，切换到回答动作状态（state = 2）
        SetAnimationState(2);

        // 使用TTSManager统一接口
        if (ttsManager != null && audioSource != null)
        {
            ttsManager.Speak(response, (clip, errorMsg) =>
            {
                if (clip != null)
                {
                    // 清理之前的AudioClip
                    if (audioSource.clip != null && audioSource.clip != audioClipToPlay)
                    {
                        DestroyImmediate(audioSource.clip);
                    }
                    
                    // 标记TTS开始播放，防止TTS音频被识别为用户输入
                    isTTSPlaying = true;
                    
                    audioSource.clip = clip;
                    audioSource.Play();
                    LogTTSEvent($"开始播放AI语音回复（{ttsManager.GetProvider()}）");

                    // 播放完成后重置状态
                    if (useWakeWordToggle || isInDialogueMode)
                    {
                        StartCoroutine(ResetWakeWordStateAfterDelay(clip.length));
                    }
                    else
                    {
                        // 如果不是唤醒模式，也需要清理TTS AudioClip
                        StartCoroutine(CleanupTTSAudioClipAfterDelay(clip, clip.length));
                    }
                }
                else
                {
                    Debug.LogError($"TTS合成失败: {errorMsg}");
                    LogSystemEvent($"TTS合成失败: {errorMsg}");
                    // TTS失败时也要重置动画状态
                    SetAnimationState(0);
                }
            });
        }
        else
        {
            // 如果没有TTS，直接重置状态
            if (useWakeWordToggle || isInDialogueMode)
            {
                StartCoroutine(ResetWakeWordStateAfterDelay(2f)); // 等待2秒后重置
                LogSystemEvent("未配置TTS，使用默认延迟重置唤醒状态");
            }
            else
            {
                // 没有TTS时，立即重置动画状态
                SetAnimationState(0);
            }
        }
    }

    private IEnumerator ResetWakeWordStateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 清理TTS生成的AudioClip
        if (audioSource != null && audioSource.clip != null && audioSource.clip != audioClipToPlay)
        {
            DestroyImmediate(audioSource.clip);
            audioSource.clip = null;
            Debug.Log("已清理TTS AudioClip");
        }
        
        // 取消TTS播放标记，允许接收用户语音输入
        isTTSPlaying = false;
        
        // 回答完毕，切换回待机状态（state = 0）
        SetAnimationState(0);
        
        LogTTSEvent("AI语音播放完成");
        Debug.Log($"[TTS] Playback completed. delay={delay:F2}s", this);
        ExitDialogueMode();
        Debug.Log("对话流程结束，重新进入唤醒监听");
        LogSystemEvent("TTS播放完成，对话流程结束，返回唤醒监听");
    }
    
    /// <summary>
    /// 清理TTS生成的AudioClip
    /// </summary>
    private IEnumerator CleanupTTSAudioClipAfterDelay(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 清理TTS生成的AudioClip
        if (clip != null && audioSource.clip == clip)
        {
            audioSource.clip = null;
            DestroyImmediate(clip);
            Debug.Log("已清理TTS AudioClip");
        }
        
        // 取消TTS播放标记，允许接收用户语音输入
        isTTSPlaying = false;
        
        // 回答完毕，切换回待机状态（state = 0）
        SetAnimationState(0);
        
        LogTTSEvent("清理TTS音频，允许继续识别");
    }

    /// <summary>
    /// 设置动画状态（委托给AnimationController）
    /// </summary>
    /// <param name="state">动画状态值：0=待机, 1=思考, 2=回答动作</param>
    private void SetAnimationState(int state)
    {
        if (animationController != null)
        {
            animationController.SetAnimationState(state);
        }
        else
        {
            Debug.LogWarning("AnimationController未设置，无法控制动画状态");
        }
    }
    
    /// <summary>
    /// 延迟启动实时语音识别，避免识别到提示音频
    /// </summary>
    private IEnumerator StartRealTimeRecognitionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (baiduInTimeVoice != null && !isRealTimeRecognitionActive)
        {
            isRealTimeRecognitionActive = true;
            baiduInTimeVoice.StartRealTimeRecognition(OnRealTimeSpeechRecognized);
            Debug.Log("实时语音识别已启动");
        }
    }
    
    /// <summary>
    /// 在指定延迟后重置TTS播放标志
    /// </summary>
    private IEnumerator ResetTTSFlagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isTTSPlaying = false;
        Debug.Log("提示音频播放完成，允许接收用户语音输入");
        LogTTSEvent("提示音播放完成");
    }

    private void SendInputMessage()
    {
        string message = GetInputText();
        // 检查消息是否有效（非空且非空白）
        if (!string.IsNullOrEmpty(message) && !string.IsNullOrWhiteSpace(message))
        {
            LogUserEvent("点击发送文本消息按钮");
            
            // 场景1：文字输入 - 用户按下发送按钮时，立即切换到思考状态
            SetAnimationState(1);
            
            SendMessageToAI(message.Trim()); // 去除首尾空白字符
            SetInputText(string.Empty);
        }
        else
        {
            Debug.LogWarning("尝试发送空消息或空白消息");
            LogSystemEvent("尝试发送空消息或空白消息，已忽略");
        }
    }

    private void ClearMessageContent()
    {
        if (HasResultText)
        {
            SetResultText(string.Empty);
        }
    }

    private void DisplayUserMessage(string message)
    {
        if (HasResultText)
        {
            AppendResultText($"用户: {message}\n");
        }
    }
    private void DisplayAgentResponse(string response)
    {
        if (HasResultText)
        {
            string cleanedResponse = string.IsNullOrEmpty(response)
                ? string.Empty
                : response.TrimStart('\r', '\n');

            AppendResultText($"AI: {cleanedResponse}\n");
            LogSystemEvent($"AI: {cleanedResponse}");
        }
        else
        {
            LogSystemEvent($"AI: {response}");
        }
    }

    /// <summary>
    /// 流式模式下更新 AI 回复显示（单条消息逐字更新，不追加新行）
    /// </summary>
    private void DisplayAgentResponseStreaming(string accumulatedText, bool isFinal = false)
    {
        if (!HasResultText) return;

        string current = GetResultText();
        int aiPrefixIndex = current.LastIndexOf("AI: ");
        if (aiPrefixIndex >= 0)
        {
            // 已有 AI 行，替换为最新累积内容
            SetResultText(current.Substring(0, aiPrefixIndex) + "AI: " + accumulatedText);
        }
        else
        {
            // 首次显示，追加 AI 前缀和内容
            AppendResultText("AI: " + accumulatedText);
        }

        if (isFinal)
        {
            AppendResultText("\n");
        }
    }

    private void LogUserEvent(string message)
    {
        OperationLogger.LogUser(message);
    }

    private void LogSystemEvent(string message)
    {
        OperationLogger.LogSystem(message);
    }

    private void LogTTSEvent(string message)
    {
        OperationLogger.LogTTS(message);
    }
    
    /// <summary>
    /// Coze流式增量回调：实时累积文本并驱动流式TTS
    /// </summary>
    private void OnAgentStreamDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        // 去掉开头首次到达的多余换行，避免 "AI:" 后出现空行
        if (streamingAccumulatedText.Length == 0)
        {
            delta = delta.TrimStart('\r', '\n');
            if (string.IsNullOrEmpty(delta))
            {
                return;
            }
        }

        streamingAccumulatedText += delta;
        DisplayAgentResponseStreaming(streamingAccumulatedText, isFinal: false);

        // 首次增量到达时切换到回答状态
        if (!streamingAnswerStarted)
        {
            streamingAnswerStarted = true;
            SetAnimationState(2);
            
            // 开始流式会话
            if (ttsManager != null && ttsManager.SupportsStreaming())
            {
                ttsManager.BeginStream();
            }
        }

        // 使用TTSManager的流式接口
        if (ttsManager != null && ttsManager.SupportsStreaming())
        {
            ttsManager.AppendStreamText(delta);
        }
    }

    /// <summary>
    /// Coze流式完成回调
    /// </summary>
    private void OnAgentStreamCompleted(string message)
    {
        // 结束流式TTS
        if (ttsManager != null && ttsManager.SupportsStreaming())
        {
            ttsManager.CompleteStream();
        }

        // 确保最终文本展示并追加换行
        if (!string.IsNullOrEmpty(streamingAccumulatedText))
        {
            DisplayAgentResponseStreaming(streamingAccumulatedText, isFinal: true);
        }

        LogSystemEvent(string.IsNullOrEmpty(message) ? "流式回复完成" : message);
    }

    /// <summary>
    /// Coze流式错误回调
    /// </summary>
    private void OnAgentStreamError(string error)
    {
        LogSystemEvent($"流式错误: {error}");
        DisplayAgentResponse(error);
        SetAnimationState(0);
    }

    /// <summary>
    /// 已废弃：使用TTSManager统一接口，不再需要此方法
    /// </summary>
    [System.Obsolete("已废弃：使用TTSManager统一接口")]
    private void UseCozeTTS(string response)
    {
        if (ttsManager == null) return;
        CozeTextToSpeech cozeTextToSpeech = ttsManager.GetCozeTTS();
        if (cozeTextToSpeech == null) return;
        cozeTextToSpeech.Speak(response, (clip, errorMsg) =>
        {
            if (clip != null)
            {
                // 清理之前的AudioClip
                if (audioSource.clip != null && audioSource.clip != audioClipToPlay)
                {
                    DestroyImmediate(audioSource.clip);
                }
                
                // 标记TTS开始播放，防止TTS音频被识别为用户输入
                isTTSPlaying = true;
                
                audioSource.clip = clip;
                audioSource.Play();
                LogTTSEvent("开始播放AI语音回复（Coze WebSocket）");

                // 播放完成后重置状态
                if (useWakeWordToggle || isInDialogueMode)
                {
                    StartCoroutine(ResetWakeWordStateAfterDelay(clip.length));
                }
                else
                {
                    // 如果不是唤醒模式，也需要清理TTS AudioClip
                    StartCoroutine(CleanupTTSAudioClipAfterDelay(clip, clip.length));
                }
            }
            else
            {
                Debug.LogError($"Coze TTS合成失败: {errorMsg}");
                LogSystemEvent($"Coze TTS合成失败: {errorMsg}");
                
                // 如果Coze TTS失败，尝试使用百度TTS作为后备
                if (ttsManager != null && ttsManager.GetBaiduTTS() != null)
                {
                    Debug.Log("尝试使用百度TTS作为后备");
                    UseBaiduTTS(response);
                }
                else
                {
                    // TTS失败时也要重置动画状态
                    SetAnimationState(0);
                }
            }
        });
    }
    
    /// <summary>
    /// 已废弃：使用TTSManager统一接口，不再需要此方法
    /// </summary>
    [System.Obsolete("已废弃：使用TTSManager统一接口")]
    private void UseBaiduTTS(string response)
    {
        if (ttsManager == null) return;
        BaiduTextToSpeech baiduTextToSpeech = ttsManager.GetBaiduTTS();
        if (baiduTextToSpeech == null) return;
        baiduTextToSpeech.Speak(response, (clip, errorMsg) =>
        {
            if (clip != null)
            {
                // 清理之前的AudioClip
                if (audioSource.clip != null && audioSource.clip != audioClipToPlay)
                {
                    DestroyImmediate(audioSource.clip);
                }
                
                // 标记TTS开始播放，防止TTS音频被识别为用户输入
                isTTSPlaying = true;
                
                audioSource.clip = clip;
                audioSource.Play();
                LogTTSEvent("开始播放AI语音回复（百度TTS）");

                // 播放完成后重置状态
                if (useWakeWordToggle || isInDialogueMode)
                {
                    StartCoroutine(ResetWakeWordStateAfterDelay(clip.length));
                }
                else
                {
                    // 如果不是唤醒模式，也需要清理TTS AudioClip
                    StartCoroutine(CleanupTTSAudioClipAfterDelay(clip, clip.length));
                }
            }
            else
            {
                Debug.LogError($"百度TTS合成失败: {errorMsg}");
                LogSystemEvent($"TTS合成失败: {errorMsg}");
                // TTS失败时也要重置动画状态
                SetAnimationState(0);
            }
        });
    }
    
    private void OnDestroy()
    {
        StopRecording();
        
        // 停止实时语音识别
        if (baiduInTimeVoice != null)
        {
            baiduInTimeVoice.StopRealTimeRecognition();
        }

        // 停止Unity语音识别
        if (keywordRecognizer != null)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
            Debug.Log("Unity语音识别已停止");
        }
        
        // 清理录音音频片段
        if (recordingClip != null)
        {
            DestroyImmediate(recordingClip);
            recordingClip = null;
        }
    }
    private void StopRecording()
    {
        if (isRecording)
        {
            if (!enableTestMode)
            {
                Microphone.End(null);
            }
            isRecording = false;
            LogSystemEvent("停止录音并释放麦克风");
        }
        
        // 清理录音音频片段
        if (recordingClip != null)
        {
            DestroyImmediate(recordingClip);
            recordingClip = null;
            LogSystemEvent("清理录音音频片段");
        }
    }

    #region 测试模式方法

    /// <summary>
    /// 测试模式：模拟唤醒词检测
    /// </summary>
    public void SimulateWakeWordDetected()
    {
        if (!enableTestMode)
        {
            Debug.LogWarning("测试模式未启用");
            return;
        }

        Debug.Log("测试模式：模拟唤醒词检测");
        LogUserEvent("测试模式：手动模拟唤醒词");
        HandleUnityWakeWordDetected();
    }

    /// <summary>
    /// 测试模式：发送模拟识别结果
    /// </summary>
    public void SendTestRecognitionResult()
    {
        if (!enableTestMode)
        {
            Debug.LogWarning("测试模式未启用");
            return;
        }

        if (testRecognitionInput == null)
        {
            Debug.LogWarning("测试识别结果输入框未设置");
            return;
        }

        string testResult = testRecognitionInput.text;
        if (string.IsNullOrEmpty(testResult))
        {
            Debug.LogWarning("请输入测试识别结果");
            return;
        }

        Debug.Log($"测试模式：模拟识别结果 - {testResult}");
        LogUserEvent($"测试模式：发送模拟识别结果 - {testResult}");

        // 如果实时识别已激活，直接调用回调
        if (isRealTimeRecognitionActive && baiduInTimeVoice != null)
        {
            OnRealTimeSpeechRecognized(testResult);
        }
        else
        {
            // 否则直接发送给AI
            SendMessageToAI(testResult);
        }

        // 清空输入框
        testRecognitionInput.text = "";
    }

    /// <summary>
    /// 测试模式：模拟音频监控协程
    /// </summary>
    private IEnumerator MonitorAudioTestMode()
    {
        Debug.Log("测试模式：开始模拟音频监控");
        
        // 生成模拟的静音音频数据
        float[] samples = new float[sampleSize];
        int micPosition = 0;

        while (isRecording)
        {
            // 模拟音频数据（静音）
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 0f; // 静音数据
            }

            // 模拟发送音频数据给实时语音识别
            if (useWakeWordToggle && baiduInTimeVoice != null)
            {
                byte[] audioData = ConvertSamplesToBytes(samples);
                baiduInTimeVoice.AddAudioData(audioData);
            }

            micPosition += sampleSize;
            
            // 模拟音频采样间隔
            yield return new WaitForSeconds(sampleSize / 16000.0f); // 根据采样率计算延迟
        }

        Debug.Log("测试模式：停止模拟音频监控");
    }

    #endregion
}
