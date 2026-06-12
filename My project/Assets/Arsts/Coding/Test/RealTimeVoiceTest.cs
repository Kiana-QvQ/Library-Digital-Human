using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 实时语音识别测试脚本
/// </summary>
public class RealTimeVoiceTest : MonoBehaviour
{
    [Header("测试组件")]
    public BaiduInTimeVoice baiduInTimeVoice;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI resultText;
    public Button startButton;
    public Button stopButton;

    private bool isTestRunning = false;

    private void Start()
    {
        if (baiduInTimeVoice == null)
            baiduInTimeVoice = FindObjectOfType<BaiduInTimeVoice>();

        if (startButton != null)
            startButton.onClick.AddListener(StartTest);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopTest);

        UpdateUI();
    }

    private void StartTest()
    {
        if (baiduInTimeVoice == null)
        {
            UpdateStatus("错误: 未找到BaiduInTimeVoice组件");
            return;
        }

        isTestRunning = true;
        UpdateStatus("开始实时语音识别测试...");
        
        baiduInTimeVoice.StartRealTimeRecognition(OnRecognitionResult);
        UpdateUI();
    }

    private void StopTest()
    {
        if (baiduInTimeVoice != null)
        {
            baiduInTimeVoice.StopRealTimeRecognition();
        }

        isTestRunning = false;
        UpdateStatus("停止实时语音识别测试");
        UpdateUI();
    }

    private void OnRecognitionResult(string result)
    {
        if (!string.IsNullOrEmpty(result))
        {
            Debug.Log($"测试识别结果: {result}");
            if (resultText != null)
            {
                resultText.text += $"{System.DateTime.Now:HH:mm:ss} - {result}\n";
            }
        }
    }

    private void UpdateStatus(string status)
    {
        Debug.Log(status);
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    private void UpdateUI()
    {
        if (startButton != null)
            startButton.interactable = !isTestRunning;
        
        if (stopButton != null)
            stopButton.interactable = isTestRunning;
    }

    private void OnDestroy()
    {
        StopTest();
    }
}