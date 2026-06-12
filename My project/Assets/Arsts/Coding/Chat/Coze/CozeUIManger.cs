using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class AgentUIManager : MonoBehaviour
{
    [SerializeField] private CozeAgentClient agentClient;
    [SerializeField] private TextMeshProUGUI messageInput;
    [SerializeField] private TextMeshProUGUI conversationDisplay;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Button SendButton; // 发送按钮
    [SerializeField] private string presetMessage = "预设消息内容"; // 预设的消息

    private void Start()
    {
        // 确保组件正确
        if (agentClient == null)
            agentClient = FindObjectOfType<CozeAgentClient>();

        if (conversationDisplay == null)
            Debug.LogError("Conversation display not assigned!");

        // 为按钮添加点击事件
        if (SendButton != null)
        {
            SendButton.onClick.AddListener(OnNewSendMessageButtonClicked);
        }
    }

    // 发送预设消息按钮点击事件
    public void OnNewSendMessageButtonClicked()
    {
        string message = presetMessage.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            // 显示用户消息
            AppendMessage("You", message);

            // 清空输入
            messageInput.text = string.Empty;

            // 显示加载指示器
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            // 发送消息到代理
            StartCoroutine(agentClient.SendMessageToAgent(message, OnAgentResponseReceived));
        }
    }

    // 发送消息按钮点击事件
    public void OnSendMessageButtonClicked()
    {
        string message = messageInput.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            // 显示用户消息
            AppendMessage("You", message);

            // 清空输入
            messageInput.text = string.Empty;

            // 显示加载指示器
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            // 发送消息到代理
            StartCoroutine(agentClient.SendMessageToAgent(message, OnAgentResponseReceived));
        }
    }

    // 接收代理响应
    private void OnAgentResponseReceived(string response)
    {
        // 隐藏加载指示器
        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        // 显示 AI 响应
        AppendMessage("Coze Agent", response);
    }

    // 在对话显示中追加消息
    private void AppendMessage(string sender, string message)
    {
        if (conversationDisplay != null)
        {
            conversationDisplay.text += $"\n\n<color=blue><b>{sender}:</b></color> {message}";

            // 滚动到底部
            Canvas.ForceUpdateCanvases();
            if (conversationDisplay.GetComponentInParent<ScrollRect>() != null)
            {
                conversationDisplay.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0f;
            }
        }
    }

    // 清空对话按钮点击事件
    public void OnClearConversationButtonClicked()
    {
        if (conversationDisplay != null)
            conversationDisplay.text = string.Empty;

        agentClient.ClearConversation();
    }
}