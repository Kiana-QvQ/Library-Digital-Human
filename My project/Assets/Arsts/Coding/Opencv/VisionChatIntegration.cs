using UnityEngine;

/// <summary>
/// 视觉与聊天系统集成示例
/// 展示如何将视觉分析结果传递给聊天系统
/// </summary>
public class VisionChatIntegration : MonoBehaviour
{
    private const bool VisionFeaturesDisabled = true;

    [Header("组件引用")]
    [Tooltip("GLM视觉客户端")]
    [SerializeField] private GLMVisionClient visionClient;
    
    [Tooltip("聊天客户端（CozeAgentClient）")]
    [SerializeField] private CozeAgentClient chatClient;
    
    [Tooltip("语音控制管理器（可选）")]
    [SerializeField] private VoiceControlManager voiceControlManager;
    
    [Header("集成设置")]
    [Tooltip("是否在发送消息时自动附加视觉分析")]
    [SerializeField] private bool autoAttachVision = false;
    
    [Tooltip("视觉分析提示词")]
    [SerializeField] private string visionPrompt = "请详细描述这张图片的内容。";
    
    [Tooltip("触发视觉分析的按键")]
    [SerializeField] private KeyCode visionTriggerKey = KeyCode.V;
    
    private string lastVisionDescription = "";
    private bool isVisionAnalysisPending = false;
    
    private void Start()
    {
        if (VisionFeaturesDisabled)
        {
            Debug.Log("VisionChatIntegration: 视觉分析与大模型联动已禁用");
            return;
        }

        // 自动查找组件
        if (visionClient == null)
            visionClient = FindObjectOfType<GLMVisionClient>();
        
        if (chatClient == null)
            chatClient = FindObjectOfType<CozeAgentClient>();
        
        if (voiceControlManager == null)
            voiceControlManager = FindObjectOfType<VoiceControlManager>();
        
        // 订阅视觉分析事件
        if (visionClient != null)
        {
            visionClient.OnVisionAnalysisReceived += OnVisionAnalysisReceived;
            visionClient.OnVisionError += OnVisionError;
        }
    }
    
    private void Update()
    {
        if (VisionFeaturesDisabled)
            return;

        // 按 V 键触发视觉分析
        if (Input.GetKeyDown(visionTriggerKey))
        {
            TriggerVisionAnalysis();
        }
    }
    
    /// <summary>
    /// 触发视觉分析
    /// </summary>
    public void TriggerVisionAnalysis()
    {
        if (VisionFeaturesDisabled)
        {
            Debug.Log("VisionChatIntegration: 视觉分析已禁用");
            return;
        }

        if (visionClient == null)
        {
            Debug.LogError("VisionChatIntegration: GLMVisionClient 未找到");
            return;
        }
        
        Debug.Log("VisionChatIntegration: 触发视觉分析...");
        isVisionAnalysisPending = true;
        visionClient.AnalyzeCurrentFrame(visionPrompt);
    }
    
    /// <summary>
    /// 发送带视觉上下文的消息
    /// </summary>
    public void SendMessageWithVision(string userMessage)
    {
        if (VisionFeaturesDisabled)
        {
            Debug.Log("VisionChatIntegration: 带视觉上下文的消息发送已禁用");
            return;
        }

        if (autoAttachVision && !string.IsNullOrEmpty(lastVisionDescription))
        {
            // 将视觉描述作为上下文附加到消息中
            string contextMessage = $"用户发送了一张图片，图片内容：{lastVisionDescription}。用户说：{userMessage}";
            
            if (chatClient != null)
            {
                StartCoroutine(chatClient.SendMessageToAgent(contextMessage, OnChatResponseReceived));
            }
            else if (voiceControlManager != null)
            {
                // 通过 VoiceControlManager 发送（需要访问私有方法，这里只是示例）
                Debug.Log($"VisionChatIntegration: 发送带视觉上下文的消息: {contextMessage}");
            }
        }
        else
        {
            // 普通消息发送
            if (chatClient != null)
            {
                StartCoroutine(chatClient.SendMessageToAgent(userMessage, OnChatResponseReceived));
            }
        }
    }
    
    /// <summary>
    /// 视觉分析结果回调
    /// </summary>
    private void OnVisionAnalysisReceived(string description)
    {
        Debug.Log($"VisionChatIntegration: 收到视觉分析结果:\n{description}");
        lastVisionDescription = description;
        isVisionAnalysisPending = false;
    }
    
    /// <summary>
    /// </summary>
    private void OnVisionError(string error)
    {
        Debug.LogError($"VisionChatIntegration: 视觉分析错误: {error}");
        isVisionAnalysisPending = false;
    }
    
    /// <summary>
    /// 聊天响应回调
    /// </summary>
    private void OnChatResponseReceived(string response)
    {
        Debug.Log($"VisionChatIntegration: 收到聊天响应: {response}");
    }
    
    private void OnDestroy()
    {
        if (visionClient != null)
        {
            visionClient.OnVisionAnalysisReceived -= OnVisionAnalysisReceived;
            visionClient.OnVisionError -= OnVisionError;
        }
    }
}

