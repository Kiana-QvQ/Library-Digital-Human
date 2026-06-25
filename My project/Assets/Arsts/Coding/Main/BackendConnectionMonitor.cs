using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 监控 Unity → 后端 → 学校大模型 的连接状态。
/// 测试探测结果仅写入 Console / OperationLogger，不写入场景对话 UI。
/// </summary>
public class BackendConnectionMonitor : MonoBehaviour
{
    public enum ConnectionState
    {
        Unknown,
        Checking,
        AllOk,
        BackendOnly,
        Offline,
    }

    [Header("后端地址")]
    [Tooltip("与 VoiceControlManager.backendChatUrl 一致，例如 http://127.0.0.1:8173/api/chat")]
    [SerializeField] private string backendChatUrl = "http://127.0.0.1:8173/api/chat";

    [SerializeField] private VoiceControlManager voiceControlManager;

    [Header("UI（可选，建议单独状态栏，不要复用对话 resultText）")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Text statusTextLegacy;
    [SerializeField] private Image statusIndicator;

    [Header("按钮（可选）")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button testChatButton;

    [Header("轮询")]
    [SerializeField] private bool pollOnStart = true;
    [SerializeField] private float pollIntervalSeconds = 12f;
    [Tooltip("启动时从后端 GET /api/config/app 拉取 backendChatUrl（打包后无需改 Unity）")]
    [SerializeField] private bool fetchAppConfigOnStart = true;
    [Tooltip("运行中定期重新拉取配置（秒，0=关闭）；Qt 改地址后 Unity 可不重启场景")]
    [SerializeField] private float refetchConfigIntervalSeconds = 120f;
    [Tooltip("未绑定 Status Text 时，自动在屏幕左上角创建一条状态栏")]
    [SerializeField] private bool createRuntimeStatusBar = true;

    [Header("对话探测（静默，不显示在场景聊天区）")]
    [SerializeField] private string probeMessage = "连接测试";
    [SerializeField] private bool runChatProbeOnStart = false;

    private Coroutine _pollCoroutine;
    private ConnectionState _state = ConnectionState.Unknown;
    private string _statusDetail = "尚未检测";
    private bool _chatProbeRunning;
    private string _lastAppliedConfigUrl = "";
    private string _lastAppliedBaiduKey = "";
    private float _lastConfigFetchTime = -999f;

    public ConnectionState State => _state;
    public string StatusDetail => _statusDetail;

    private void Awake()
    {
        if (voiceControlManager == null)
        {
            voiceControlManager = GetComponent<VoiceControlManager>();
        }

        if (statusText == null && statusTextLegacy == null && createRuntimeStatusBar)
        {
            TryCreateRuntimeStatusBar();
        }
    }

    private void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(() => StartCoroutine(CheckHealthCoroutine(true)));
        }

        if (testChatButton != null)
        {
            testChatButton.onClick.AddListener(() => StartCoroutine(RunChatProbeCoroutine()));
        }

        if (pollOnStart)
        {
            if (fetchAppConfigOnStart)
            {
                StartCoroutine(FetchAppConfigThenPoll(runChatProbeOnStart));
            }
            else
            {
                StartPolling();
                if (runChatProbeOnStart)
                {
                    StartCoroutine(RunChatProbeCoroutine());
                }
            }
        }
        else if (runChatProbeOnStart)
        {
            StartCoroutine(RunChatProbeCoroutine());
        }
    }

    private void OnDestroy()
    {
        StopPolling();
    }

    public void SetBackendChatUrl(string url)
    {
        ApplyBackendChatUrl(url);
    }

    private IEnumerator FetchAppConfigThenPoll(bool runProbeAfter)
    {
        yield return FetchAppConfigCoroutine();
        StartPolling();
        if (runProbeAfter)
        {
            yield return RunChatProbeCoroutine();
        }
    }

    private string ResolveAppConfigUrl()
    {
        string diagnostic = ResolveDiagnosticUrl();
        const string suffix = "/api/diagnostic";
        if (diagnostic.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return diagnostic.Substring(0, diagnostic.Length - suffix.Length) + "/api/config/app";
        }

        return "http://127.0.0.1:8173/api/config/app";
    }

    private IEnumerator FetchAppConfigCoroutine()
    {
        string[] candidates = BuildAppConfigUrlCandidates();
        foreach (string url in candidates)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 6;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    continue;
                }

                try
                {
                    AppConfigResponse cfg = JsonConvert.DeserializeObject<AppConfigResponse>(req.downloadHandler.text);
                    if (cfg != null && !string.IsNullOrWhiteSpace(cfg.backendChatUrl))
                    {
                        string chatUrl = cfg.backendChatUrl.Trim();
                        if (chatUrl != _lastAppliedConfigUrl)
                        {
                            ApplyBackendChatUrl(chatUrl);
                            _lastAppliedConfigUrl = chatUrl;
                            string log =
                                $"[BackendConnectionMonitor] 已应用运行时配置 backendChatUrl={chatUrl}, " +
                                $"model={cfg.llmDefaultModel ?? ""}";
                            Debug.Log(log, this);
                            OperationLogger.LogSystem(log);
                        }

                        ApplyBaiduCredentialsIfConfigured(cfg);
                        _lastConfigFetchTime = Time.unscaledTime;
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BackendConnectionMonitor] 解析运行时配置失败: {ex.Message}", this);
                }
            }
        }

        Debug.LogWarning("[BackendConnectionMonitor] 拉取运行时配置失败，将使用 Inspector 中的默认地址", this);
    }

    private string[] BuildAppConfigUrlCandidates()
    {
        var list = new System.Collections.Generic.List<string>();
        string primary = ResolveAppConfigUrl();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            list.Add(primary);
        }

        const string fallback = "http://127.0.0.1:8173/api/config/app";
        if (!list.Contains(fallback))
        {
            list.Add(fallback);
        }

        return list.ToArray();
    }

    private void ApplyBackendChatUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        backendChatUrl = url.Trim();
        if (voiceControlManager != null)
        {
            voiceControlManager.ApplyBackendChatUrl(backendChatUrl);
        }
    }

    private void ApplyBaiduCredentialsIfConfigured(AppConfigResponse cfg)
    {
        if (cfg == null || !cfg.baiduConfigured)
        {
            return;
        }

        string apiKey = (cfg.baiduApiKey ?? "").Trim();
        string secretKey = (cfg.baiduSecretKey ?? "").Trim();
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
        {
            return;
        }

        string fingerprint = apiKey + "|" + secretKey;
        if (fingerprint == _lastAppliedBaiduKey)
        {
            return;
        }

        BaiduSettings.ApplyRuntimeCredentialsToAll(apiKey, secretKey);
        _lastAppliedBaiduKey = fingerprint;
        string log = "[BackendConnectionMonitor] 已应用运行时百度 ASR/TTS 凭证（来自 Qt / app_config.json）";
        Debug.Log(log, this);
        OperationLogger.LogSystem(log);
    }

    public void StartPolling()
    {
        StopPolling();
        _pollCoroutine = StartCoroutine(PollLoop());
    }

    public void StopPolling()
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            if (refetchConfigIntervalSeconds > 0f
                && Time.unscaledTime - _lastConfigFetchTime >= refetchConfigIntervalSeconds)
            {
                yield return FetchAppConfigCoroutine();
            }

            yield return CheckHealthCoroutine(false);
            yield return new WaitForSeconds(Mathf.Max(3f, pollIntervalSeconds));
        }
    }

    private string ResolveDiagnosticUrl()
    {
        string chat = (backendChatUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(chat))
        {
            return "http://127.0.0.1:8173/api/diagnostic";
        }

        const string chatSuffix = "/api/chat";
        if (chat.EndsWith(chatSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return chat.Substring(0, chat.Length - chatSuffix.Length) + "/api/diagnostic";
        }

        int apiIndex = chat.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        if (apiIndex > 0)
        {
            return chat.Substring(0, apiIndex) + "/api/diagnostic";
        }

        return chat + "/api/diagnostic";
    }

    private IEnumerator CheckHealthCoroutine(bool logAlways)
    {
        SetState(ConnectionState.Checking, "正在检测连接…");

        string url = ResolveDiagnosticUrl();
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string msg = $"后端不可达: {req.error}";
                SetState(ConnectionState.Offline, msg);
                if (logAlways)
                {
                    OperationLogger.LogSystem($"[连接检测] {msg}");
                }
                yield break;
            }

            try
            {
                DiagnosticResponse data = JsonConvert.DeserializeObject<DiagnosticResponse>(req.downloadHandler.text);
                ApplyDiagnostic(data, logAlways);
            }
            catch (Exception ex)
            {
                string msg = $"诊断响应解析失败: {ex.Message}";
                SetState(ConnectionState.Offline, msg);
                if (logAlways)
                {
                    OperationLogger.LogSystem($"[连接检测] {msg}");
                }
            }
        }
    }

    private void ApplyDiagnostic(DiagnosticResponse data, bool logAlways)
    {
        if (data == null)
        {
            SetState(ConnectionState.Offline, "诊断数据为空");
            return;
        }

        bool backendOk = string.Equals(data.backend, "ok", StringComparison.OrdinalIgnoreCase);
        bool llmOk = data.llm_reachable;

        string summary = data.summary ?? data.message ?? "";
        if (backendOk && llmOk)
        {
            SetState(ConnectionState.AllOk, summary);
        }
        else if (backendOk)
        {
            SetState(ConnectionState.BackendOnly, summary);
        }
        else
        {
            SetState(ConnectionState.Offline, summary);
        }

        string logLine =
            $"[连接检测] backend={data.backend}, llm={data.llm_provider}, " +
            $"reachable={data.llm_reachable}, model={data.llm_model}, detail={summary}";
        Debug.Log(logLine, this);

        if (logAlways || _state != ConnectionState.AllOk)
        {
            OperationLogger.LogSystem(logLine);
        }
    }

    private IEnumerator RunChatProbeCoroutine()
    {
        if (_chatProbeRunning)
        {
            yield break;
        }

        _chatProbeRunning = true;
        OperationLogger.LogSystem("[连接探测] 开始静默对话测试（不写入场景 UI）");

        string url = (backendChatUrl ?? "").Trim().TrimEnd('/');
        if (!url.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
        {
            url += "/api/chat";
        }

        var payload = new ChatProbeRequest
        {
            message = probeMessage,
            user_id = "unity_probe",
            memory_profile = 0,
        };
        string json = JsonConvert.SerializeObject(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 60;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = $"[连接探测] 失败 HTTP={req.responseCode} {req.error}";
                Debug.LogWarning(err, this);
                OperationLogger.LogSystem(err);
            }
            else
            {
                try
                {
                    ChatProbeResponse resp = JsonConvert.DeserializeObject<ChatProbeResponse>(req.downloadHandler.text);
                    string preview = resp?.response ?? "";
                    if (preview.Length > 120)
                    {
                        preview = preview.Substring(0, 120) + "…";
                    }

                    string ok = $"[连接探测] 成功 session={resp?.session_id ?? ""} 回复预览: {preview}";
                    Debug.Log(ok, this);
                    OperationLogger.LogSystem(ok);
                }
                catch (Exception ex)
                {
                    OperationLogger.LogSystem($"[连接探测] 响应解析失败: {ex.Message}");
                }
            }
        }

        _chatProbeRunning = false;
        yield return CheckHealthCoroutine(true);
    }

    private void SetState(ConnectionState state, string detail)
    {
        _state = state;
        _statusDetail = detail ?? "";
        UpdateStatusUi();
    }

    private void UpdateStatusUi()
    {
        string label = BuildStatusLabel();
        Color color = GetStateColor();

        if (statusText != null)
        {
            statusText.text = label;
            statusText.color = color;
        }

        if (statusTextLegacy != null)
        {
            statusTextLegacy.text = label;
            statusTextLegacy.color = color;
        }

        if (statusIndicator != null)
        {
            statusIndicator.color = color;
        }
    }

    private string BuildStatusLabel()
    {
        switch (_state)
        {
            case ConnectionState.AllOk:
                return $"● 已连接 · {_statusDetail}";
            case ConnectionState.BackendOnly:
                return $"● 后端正常 / 模型不可用 · {_statusDetail}";
            case ConnectionState.Checking:
                return $"◌ 检测中… {_statusDetail}";
            case ConnectionState.Offline:
                return $"○ 未连接 · {_statusDetail}";
            default:
                return $"○ 未知 · {_statusDetail}";
        }
    }

    private static Color GetStateColorFor(ConnectionState state)
    {
        switch (state)
        {
            case ConnectionState.AllOk:
                return new Color(0.2f, 0.85f, 0.35f);
            case ConnectionState.BackendOnly:
                return new Color(1f, 0.78f, 0.2f);
            case ConnectionState.Checking:
                return new Color(0.55f, 0.75f, 1f);
            case ConnectionState.Offline:
                return new Color(1f, 0.35f, 0.35f);
            default:
                return Color.gray;
        }
    }

    private Color GetStateColor() => GetStateColorFor(_state);

    private void TryCreateRuntimeStatusBar()
    {
        try
        {
            var canvasGo = new GameObject("BackendConnectionStatusCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(-24f, 36f);

            statusTextLegacy = textGo.AddComponent<Text>();
            statusTextLegacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusTextLegacy.fontSize = 16;
            statusTextLegacy.alignment = TextAnchor.MiddleLeft;
            statusTextLegacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusTextLegacy.verticalOverflow = VerticalWrapMode.Truncate;
            statusTextLegacy.text = "连接状态：初始化中…";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BackendConnectionMonitor] 无法创建运行时状态栏: {ex.Message}", this);
        }
    }

    [Serializable]
    private class AppConfigResponse
    {
        public string backendChatUrl;
        public string llmDefaultModel;
        public bool baiduConfigured;
        public string baiduApiKey;
        public string baiduSecretKey;
    }

    [Serializable]
    private class DiagnosticResponse
    {
        public string backend;
        public bool llm_configured;
        public bool llm_reachable;
        public string llm_provider;
        public string llm_model;
        public string message;
        public string summary;
    }

    [Serializable]
    private class ChatProbeRequest
    {
        public string message;
        public string user_id;
        public int memory_profile;
    }

    [Serializable]
    private class ChatProbeResponse
    {
        public string response;
        public string session_id;
    }
}
