using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BaiduSpeechToText))]
public class SpeechRecognitionManager : MonoBehaviour
{
    public TextMeshProUGUI resultText; // 用于显示识别结果的Text组件
    public Button startButton; // 开始录音按钮
    public Button stopButton; // 停止录音按钮

    private BaiduSpeechToText speechToText;
    private AudioClip recordingClip;
    private bool isRecording = false;

    private void Awake()
    {
        speechToText = GetComponent<BaiduSpeechToText>();
        InitializeButtons();
    }

    private void InitializeButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartRecording);
            startButton.interactable = true;
        }

        if (stopButton != null)
        {
            stopButton.onClick.AddListener(StopRecording);
            stopButton.interactable = false;
        }
    }

    public void StartRecording()
    {
        if (!isRecording && startButton != null)
        {
            recordingClip = Microphone.Start(null, false, 10, 16000);
            isRecording = true;
            startButton.interactable = false;
            stopButton.interactable = true;
        }
    }

    public void StopRecording()
    {
        if (isRecording && stopButton != null)
        {
            Microphone.End(null);
            isRecording = false;
            speechToText.SpeechToText(recordingClip, DisplayResult);
            startButton.interactable = true;
            stopButton.interactable = false;
        }
    }

    private void DisplayResult(string result)
    {
        resultText.text = result;
        // 在这里添加 Debug.Log 语句
        Debug.Log("识别到的语音内容为: " + result);
    }
}
