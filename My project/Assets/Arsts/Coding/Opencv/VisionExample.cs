using UnityEngine;
using OpenCVForUnity.CoreModule;

/// <summary>
/// GLM 视觉功能使用示例
/// 展示如何集成视觉分析到现有系统中
/// </summary>
public class VisionExample : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("GLM视觉客户端")]
    [SerializeField] private GLMVisionClient visionClient;
    
    [Tooltip("眼睛追踪控制器")]
    [SerializeField] private EyesTrackingController eyesController;
    
    [Header("测试设置")]
    [Tooltip("按空格键触发视觉分析")]
    [SerializeField] private KeyCode triggerKey = KeyCode.Space;
    
    [Tooltip("自定义提示词（可选）")]
    [SerializeField] private string customPrompt = "请详细描述这张图片，包括场景、人物、物体等。";
    
    private void Start()
    {
        // 自动查找组件
        if (visionClient == null)
            visionClient = FindObjectOfType<GLMVisionClient>();
        
        if (eyesController == null)
            eyesController = FindObjectOfType<EyesTrackingController>();
        
        // 订阅视觉分析结果事件
        if (visionClient != null)
        {
            visionClient.OnVisionAnalysisReceived += OnVisionAnalysisReceived;
            visionClient.OnVisionError += OnVisionError;
        }
    }
    
    private void Update()
    {
        // 按空格键触发视觉分析
        if (Input.GetKeyDown(triggerKey))
        {
            TriggerVisionAnalysis();
        }
    }
    
    /// <summary>
    /// 触发视觉分析
    /// </summary>
    public void TriggerVisionAnalysis()
    {
        if (visionClient == null)
        {
            Debug.LogError("VisionExample: GLMVisionClient 未找到");
            return;
        }
        
        Debug.Log("VisionExample: 触发视觉分析...");
        visionClient.AnalyzeCurrentFrame(customPrompt);
    }
    
    /// <summary>
    /// 视觉分析结果回调
    /// </summary>
    private void OnVisionAnalysisReceived(string description)
    {
        Debug.Log($"VisionExample: 收到视觉分析结果:\n{description}");
        
        // 这里可以将结果传递给聊天系统或其他组件
        // 例如：
        // chatSystem.SendMessage($"我看到：{description}");
    }
    
    /// <summary>
    /// 视觉分析错误回调
    /// </summary>
    private void OnVisionError(string error)
    {
        Debug.LogError($"VisionExample: 视觉分析错误: {error}");
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        if (visionClient != null)
        {
            visionClient.OnVisionAnalysisReceived -= OnVisionAnalysisReceived;
            visionClient.OnVisionError -= OnVisionError;
        }
    }
}

