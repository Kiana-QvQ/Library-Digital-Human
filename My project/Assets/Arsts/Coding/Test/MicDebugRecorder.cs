using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicDebugRecorder : MonoBehaviour
{
    [Header("录音参数")]
    public int recordSeconds = 5;   // 录音时长（秒）
    public int sampleRate   = 16000;

    [Header("触发方式")]
    public KeyCode hotKey = KeyCode.F9; // 按 F9 开始一次测试录音

    private AudioClip clip;

    void Start()
    {
        // 打印当前可用的麦克风设备
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicDebug] 未检测到麦克风设备");
        }
        else
        {
            Debug.Log("[MicDebug] 检测到的麦克风设备：");
            foreach (var dev in Microphone.devices)
            {
                Debug.Log(" - " + dev);
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(hotKey))
        {
            StartCoroutine(RecordAndAnalyze());
        }
    }

    private IEnumerator RecordAndAnalyze()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicDebug] 无麦克风设备，无法录音");
            yield break;
        }

        Debug.Log($"[MicDebug] 开始录音 {recordSeconds}s, {sampleRate}Hz …");
        clip = Microphone.Start(null, false, recordSeconds, sampleRate);

        // 等录音结束
        yield return new WaitForSeconds(recordSeconds + 0.1f);
        Microphone.End(null);

        if (clip == null)
        {
            Debug.LogError("[MicDebug] 录音失败，AudioClip 为 null");
            yield break;
        }

        // 基本信息
        Debug.Log($"[MicDebug] 录音完成: " +
                  $"freq={clip.frequency}, channels={clip.channels}, " +
                  $"length={clip.length:F2}s, samples={clip.samples}");

        // 计算 RMS（整体音量）
        float[] data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);

        double sum = 0;
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i] * data[i];
        }
        double rms = Mathf.Sqrt((float)(sum / data.Length));
        Debug.Log($"[MicDebug] 全程 RMS={rms:F4} (越大表示声音越明显，典型说话 > 0.01)");

        // 可选：回放录音，确认耳朵听到是否正常
        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log("[MicDebug] 已开始回放录音（绑定在当前对象的 AudioSource）");
        }
        else
        {
            Debug.Log("[MicDebug] 未找到 AudioSource，只做分析不回放");
        }
    }
}