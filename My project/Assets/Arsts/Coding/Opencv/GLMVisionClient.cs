using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using OpenCVForUnity.CoreModule;

/// <summary>
/// GLM 视觉客户端 - 调用 GLM-4.6V-Flash 视觉模型 API
/// </summary>
public class GLMVisionClient : MonoBehaviour
{
    private const bool VisionFeaturesDisabled = true;

    [Header("组件引用")]
    [Tooltip("眼睛追踪控制器（用于获取摄像头帧）")]
    [SerializeField] private EyesTrackingController eyesController;
    
    [Header("视觉分析设置")]
    [Tooltip("是否启用自动分析")]
    [SerializeField] private bool autoAnalyze = false;
    
    [Tooltip("自动分析间隔（秒）")]
    [SerializeField] private float analyzeInterval = 5f;
    
    [Tooltip("最大图像宽度（压缩）")]
    [SerializeField] private int maxImageWidth = 1024;
    
    [Tooltip("最大图像高度（压缩）")]
    [SerializeField] private int maxImageHeight = 1024;
    
    [Tooltip("图像压缩质量（0-100）")]
    [SerializeField] private int imageQuality = 80;
    
    [Tooltip("是否启用 thinking 模式")]
    [SerializeField] private bool enableThinking = true;

    [Header("路由设置")]
    [Tooltip("视觉请求路由模式：直连 GLM 或 通过后端 API")]
    [SerializeField] private VisionRouteMode routeMode = VisionRouteMode.BackendApi;

    [Tooltip("后端视觉分析接口地址（仅在 BackendApi 模式下使用）")]
    [SerializeField] private string backendVisionUrl = "http://127.0.0.1:8173/api/vision/analyze";
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // API 配置（由 Settings 设置）
    private string apiKey = "";
    private string apiBaseUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    private string model = "glm-4.6v-flash";
    
    // 状态
    private bool isAnalyzing = false;
    private float lastAnalyzeTime = 0f;
    private GLMVisionSettings settings;
    
    // 事件
    public System.Action<string> OnVisionAnalysisReceived; // 收到视觉分析结果
    public System.Action<string> OnVisionError; // 视觉分析错误
    
    private void Start()
    {
        // 查找 Settings
        if (settings == null)
        {
            APIManager apiManager = APIManager.Instance;
            if (apiManager != null)
                settings = apiManager.GetGLMVisionSettings();
            
            if (settings == null)
                settings = FindObjectOfType<GLMVisionSettings>();
        }
        
        // 从 Settings 获取配置
        if (settings != null)
        {
            apiKey = settings.GetApiKey();
            apiBaseUrl = settings.GetApiBaseUrl();
            model = settings.GetModel();
            
            // 监听配置变化
            settings.OnCredentialsChanged += OnCredentialsChanged;
        }
        
        // 查找眼睛追踪控制器
        if (eyesController == null)
            eyesController = FindObjectOfType<EyesTrackingController>();
        
        if (eyesController == null && enableDebug)
            Debug.LogWarning("GLMVisionClient: 未找到 EyesTrackingController，无法获取摄像头帧");
    }
    
    private void Awake()
    {
        if (VisionFeaturesDisabled)
            autoAnalyze = false;
    }

    private void Update()
    {
        if (VisionFeaturesDisabled)
            return;

        // 自动分析模式
        if (autoAnalyze && !isAnalyzing && Time.time - lastAnalyzeTime >= analyzeInterval)
        {
            AnalyzeCurrentFrame();
        }
    }
    
    /// <summary>
    /// 设置 API 凭证（由 Settings 调用）
    /// </summary>
    public void SetCredentials(string newApiKey, string newApiBaseUrl, string newModel)
    {
        apiKey = newApiKey;
        apiBaseUrl = newApiBaseUrl;
        model = newModel;
        
        if (enableDebug)
            Debug.Log($"GLMVisionClient: API 凭证已更新");
    }

    /// <summary>
    /// 应用路由与后端设置（由 Settings 调用）
    /// </summary>
    public void ApplyRoutingSettings(VisionRouteMode mode, string backendUrl)
    {
        routeMode = mode;
        if (!string.IsNullOrEmpty(backendUrl))
        {
            backendVisionUrl = backendUrl;
        }

        if (enableDebug)
        {
            Debug.Log($"GLMVisionClient: 路由模式 = {routeMode}, 后端URL = {backendVisionUrl}");
        }
    }
    
    /// <summary>
    /// 分析当前摄像头帧
    /// </summary>
    public void AnalyzeCurrentFrame(string customPrompt = null)
    {
        if (VisionFeaturesDisabled)
        {
            if (enableDebug)
                Debug.Log("GLMVisionClient: 视觉分析已禁用");
            return;
        }

        if (isAnalyzing)
        {
            if (enableDebug)
                Debug.LogWarning("GLMVisionClient: 正在分析中，请稍候...");
            return;
        }
        
        if (eyesController == null)
        {
            Debug.LogError("GLMVisionClient: EyesTrackingController 未找到");
            return;
        }
        
        if (!eyesController.IsInitialized())
        {
            if (enableDebug)
                Debug.LogWarning("GLMVisionClient: 摄像头未初始化");
            return;
        }
        
        // 获取当前帧
        Mat frame = eyesController.GetCurrentFrame();
        if (frame == null || frame.empty())
        {
            if (enableDebug)
                Debug.LogWarning("GLMVisionClient: 无法获取摄像头帧");
            return;
        }
        
        StartCoroutine(AnalyzeImageCoroutine(frame, customPrompt));
    }
    
    /// <summary>
    /// 分析指定的 Mat 图像
    /// </summary>
    public void AnalyzeImage(Mat image, string customPrompt = null)
    {
        if (isAnalyzing)
        {
            if (enableDebug)
                Debug.LogWarning("GLMVisionClient: 正在分析中，请稍候...");
            return;
        }
        
        if (image == null || image.empty())
        {
            Debug.LogError("GLMVisionClient: 图像为空");
            return;
        }
        
        StartCoroutine(AnalyzeImageCoroutine(image, customPrompt));
    }
    
    /// <summary>
    /// 分析图像协程
    /// </summary>
    private IEnumerator AnalyzeImageCoroutine(Mat image, string customPrompt)
    {
        isAnalyzing = true;
        Texture2D texture = null;
        Texture2D compressedTexture = null;
        
        try
        {
            // 1. 将 Mat 转换为 Texture2D
            texture = new Texture2D(image.width(), image.height(), TextureFormat.RGBA32, false);
            OpenCVForUnity.UnityUtils.Utils.matToTexture2D(image, texture);
            
            // 2. 压缩图像（如果太大）
            compressedTexture = CompressTexture(texture);
            
            // 3. 转换为 Base64
            byte[] imageBytes = compressedTexture.EncodeToJPG(imageQuality);
            string imageBase64 = System.Convert.ToBase64String(imageBytes);
            
            // 4. 根据路由模式构建并发送请求
            if (routeMode == VisionRouteMode.DirectApi)
            {
                yield return SendDirectGlmRequest(imageBase64, customPrompt);
            }
            else
            {
                yield return SendBackendVisionRequest(imageBase64, customPrompt);
            }
        }
        finally
        {
            // 清理资源
            if (texture != null)
                Destroy(texture);
            if (compressedTexture != null && compressedTexture != texture)
                Destroy(compressedTexture);
            
            isAnalyzing = false;
            lastAnalyzeTime = Time.time;
        }
    }
    
    /// <summary>
    /// 压缩纹理
    /// </summary>
    private Texture2D CompressTexture(Texture2D source)
    {
        if (source.width <= maxImageWidth && source.height <= maxImageHeight)
            return source;
        
        float scale = Mathf.Min(
            (float)maxImageWidth / source.width,
            (float)maxImageHeight / source.height
        );
        
        int newWidth = Mathf.RoundToInt(source.width * scale);
        int newHeight = Mathf.RoundToInt(source.height * scale);
        
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        
        Texture2D compressed = new Texture2D(newWidth, newHeight);
        compressed.ReadPixels(new UnityEngine.Rect(0, 0, newWidth, newHeight), 0, 0);
        compressed.Apply();
        
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        
        return compressed;
    }
    
    /// <summary>
    /// 凭证变化回调
    /// </summary>
    private void OnCredentialsChanged()
    {
        if (settings != null)
        {
            apiKey = settings.GetApiKey();
            apiBaseUrl = settings.GetApiBaseUrl();
            model = settings.GetModel();
        }
    }
    
    private void OnDestroy()
    {
        if (settings != null)
        {
            settings.OnCredentialsChanged -= OnCredentialsChanged;
        }
    }
    
    /// <summary>
    /// 直连 GLM 云端视觉 API 的请求
    /// </summary>
    private IEnumerator SendDirectGlmRequest(string imageBase64, string customPrompt)
    {
        // 构建 GLM 官方接口格式的请求
        GLMVisionRequest request = new GLMVisionRequest
        {
            model = model,
            messages = new List<GLMMessage>
            {
                new GLMMessage
                {
                    role = "user",
                    content = new List<GLMContent>
                    {
                        new GLMContent
                        {
                            type = "image_url",
                            image_url = new GLMImageUrl
                            {
                                url = $"data:image/jpeg;base64,{imageBase64}"
                            }
                        },
                        new GLMContent
                        {
                            type = "text",
                            text = customPrompt ?? "请详细描述这张图片的内容，包括场景、人物、物体、动作等。"
                        }
                    }
                }
            }
        };

        if (enableThinking)
        {
            request.thinking = new GLMThinking
            {
                type = "enabled"
            };
        }

        string jsonRequest = JsonConvert.SerializeObject(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        using (UnityWebRequest www = new UnityWebRequest(apiBaseUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            if (enableDebug)
                Debug.Log("GLMVisionClient: 发送直连 GLM 视觉分析请求...");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"视觉分析请求失败: {www.error}";
                Debug.LogError(errorMsg);
                OnVisionError?.Invoke(errorMsg);
            }
            else
            {
                string responseText = www.downloadHandler.text;

                if (enableDebug)
                    Debug.Log($"GLMVisionClient: 收到 GLM 响应: {responseText}");

                try
                {
                    GLMVisionResponse response = JsonConvert.DeserializeObject<GLMVisionResponse>(responseText);

                    if (response != null && response.choices != null && response.choices.Count > 0)
                    {
                        string description = response.choices[0].message.content;

                        if (enableDebug)
                            Debug.Log($"GLMVisionClient: 视觉分析结果 - {description}");

                        OnVisionAnalysisReceived?.Invoke(description);
                    }
                    else
                    {
                        string errorMsg = "响应格式错误";
                        Debug.LogError($"GLMVisionClient: {errorMsg}");
                        OnVisionError?.Invoke(errorMsg);
                    }
                }
                catch (System.Exception e)
                {
                    string errorMsg = $"解析响应失败: {e.Message}";
                    Debug.LogError($"GLMVisionClient: {errorMsg}");
                    OnVisionError?.Invoke(errorMsg);
                }
            }
        }
    }

    /// <summary>
    /// 通过本地/自建后端（digital_human_backend）发送视觉请求
    /// </summary>
    private IEnumerator SendBackendVisionRequest(string imageBase64, string customPrompt)
    {
        BackendVisionRequest backendRequest = new BackendVisionRequest
        {
            image_base64 = imageBase64,
            prompt = customPrompt ?? "请详细描述这张图片的内容，包括场景、人物、物体、动作等。",
            model = null
        };

        string jsonRequest = JsonConvert.SerializeObject(backendRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        using (UnityWebRequest www = new UnityWebRequest(backendVisionUrl, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            if (enableDebug)
                Debug.Log($"GLMVisionClient: 发送后端视觉分析请求到 {backendVisionUrl}...");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"后端视觉分析请求失败: {www.error}";
                Debug.LogError(errorMsg);
                OnVisionError?.Invoke(errorMsg);
            }
            else
            {
                string responseText = www.downloadHandler.text;

                if (enableDebug)
                    Debug.Log($"GLMVisionClient: 收到后端视觉响应: {responseText}");

                try
                {
                    BackendVisionResponse response = JsonConvert.DeserializeObject<BackendVisionResponse>(responseText);
                    if (response != null && !string.IsNullOrEmpty(response.description))
                    {
                        if (enableDebug)
                            Debug.Log($"GLMVisionClient: 后端视觉分析结果 - {response.description} (backend={response.backend}, model={response.model})");

                        OnVisionAnalysisReceived?.Invoke(response.description);
                    }
                    else
                    {
                        string errorMsg = "后端响应格式错误";
                        Debug.LogError($"GLMVisionClient: {errorMsg}");
                        OnVisionError?.Invoke(errorMsg);
                    }
                }
                catch (System.Exception e)
                {
                    string errorMsg = $"解析后端响应失败: {e.Message}";
                    Debug.LogError($"GLMVisionClient: {errorMsg}");
                    OnVisionError?.Invoke(errorMsg);
                }
            }
        }
    }

    // API 请求/响应数据类
    [System.Serializable]
    private class GLMVisionRequest
    {
        public string model;
        public List<GLMMessage> messages;
        public GLMThinking thinking;
    }
    
    [System.Serializable]
    private class GLMMessage
    {
        public string role;
        public List<GLMContent> content;
    }
    
    [System.Serializable]
    private class GLMContent
    {
        public string type;
        public string text;
        public GLMImageUrl image_url;
    }
    
    [System.Serializable]
    private class GLMImageUrl
    {
        public string url;
    }
    
    [System.Serializable]
    private class GLMThinking
    {
        public string type;
    }
    
    [System.Serializable]
    private class GLMVisionResponse
    {
        public List<GLMChoice> choices;
    }
    
    [System.Serializable]
    private class GLMChoice
    {
        public GLMMessageResponse message;
    }
    
    [System.Serializable]
    private class GLMMessageResponse
    {
        public string content;
    }

    // 后端视觉服务的请求/响应结构
    [System.Serializable]
    private class BackendVisionRequest
    {
        public string image_base64;
        public string prompt;
        public string model;
    }

    [System.Serializable]
    private class BackendVisionResponse
    {
        public string description;
        public string model;
        public string backend;
    }
}

