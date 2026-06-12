using UnityEngine;
using System.Collections;

/// <summary>
/// 流式响应使用示例
/// </summary>
public class StreamResponseExample : MonoBehaviour
{
    [SerializeField] private CozeAgentClient agentClient;
    [SerializeField] private string testMessage = "2024年10月1日是星期几";
    [SerializeField] private TTS ttsComponent; // TTS语音合成组件

    private void Start()
    {
        if (agentClient == null)
            agentClient = FindObjectOfType<CozeAgentClient>();
            
        // 设置TTS组件
        if (ttsComponent != null && agentClient != null)
        {
            agentClient.SetTTSComponent(ttsComponent);
        }
    }

    // 测试流式响应
    public void TestStreamResponse()
    {
        if (agentClient != null)
        {
            // 方法1：使用专门的流式方法
            StartCoroutine(agentClient.SendStreamMessageToAgent(
                testMessage,
                OnStreamContentReceived,
                OnStreamCompleted,
                OnStreamError
            ));
        }
    }

    // 测试通过开关控制的流式响应
    public void TestStreamResponseWithSwitch()
    {
        if (agentClient != null)
        {
            // 方法2：通过开关控制（推荐）
            agentClient.SetStreamResponse(true); // 启用流式响应
            StartCoroutine(agentClient.SendMessageToAgent(
                testMessage,
                OnStreamCompleted,
                true // 强制使用流式
            ));
        }
    }

    // 测试流式响应 + 语音合成
    public void TestStreamResponseWithTTS()
    {
        if (agentClient != null)
        {
            // 启用流式响应和语音合成
            agentClient.SetStreamResponse(true);
            agentClient.SetStreamTTS(true);
            
            StartCoroutine(agentClient.SendMessageToAgent(
                testMessage,
                OnStreamCompleted,
                true
            ));
        }
    }

    // 停止TTS播放
    public void StopTTSPlayback()
    {
        if (agentClient != null)
        {
            agentClient.StopTTSPlayback();
        }
    }

    // 接收到流式内容时的回调
    private void OnStreamContentReceived(string content)
    {
        Debug.Log($"接收到流式内容: {content}");
        // 这里可以实时更新UI，显示正在接收的内容
        // 例如：conversationDisplay.text += content;
    }

    // 流式响应完成时的回调
    private void OnStreamCompleted(string finalMessage)
    {
        Debug.Log($"流式响应完成: {finalMessage}");
        // 这里可以处理最终完成的响应
    }

    // 流式响应错误时的回调
    private void OnStreamError(string error)
    {
        Debug.LogError($"流式响应错误: {error}");
        // 这里可以处理错误情况
    }

    // 测试普通响应（非流式）
    public void TestNormalResponse()
    {
        if (agentClient != null)
        {
            StartCoroutine(agentClient.SendMessageToAgent(
                testMessage,
                OnNormalResponseReceived,
                false // 非流式
            ));
        }
    }

    private void OnNormalResponseReceived(string response)
    {
        Debug.Log($"普通响应: {response}");
    }
}
