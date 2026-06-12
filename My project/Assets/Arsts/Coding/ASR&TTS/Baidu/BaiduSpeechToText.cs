using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Text;

[RequireComponent(typeof(BaiduSettings))]
public class BaiduSpeechToText : STT
{
    #region 语音识别
    /// <summary>
    /// token管理器
    /// </summary>
    [SerializeField] private BaiduSettings m_Settings;

    private void Awake()
    {
        m_Settings = this.GetComponent<BaiduSettings>();
        m_SpeechRecognizeURL = "https://vop.baidu.com/server_api";
    }

    /// <summary>
    /// 语音识别
    /// </summary>
    /// <param name="_clip"></param>
    /// <param name="_callback"></param>
    public override void SpeechToText(AudioClip _clip, Action<string> _callback)
    {
        if (_clip != null)
        {
            double sec = _clip.length;
            Debug.Log($"[ASR:Baidu] SpeechToText start clip='{_clip.name}' length={sec:F2}s freq={_clip.frequency}Hz ch={_clip.channels}");
            OperationLogger.LogSystem($"[ASR:Baidu] start clip='{_clip.name}' length={sec:F2}s freq={_clip.frequency}Hz ch={_clip.channels}");
        }
        else
        {
            Debug.LogWarning("[ASR:Baidu] SpeechToText start (clip is null)");
            OperationLogger.LogSystem("[ASR:Baidu] start (clip is null)");
        }
        StartCoroutine(RefreshTokenAndRecognize(_clip, _callback));
    }

    private IEnumerator RefreshTokenAndRecognize(AudioClip _clip, Action<string> _callback)
    {
        yield return m_Settings.RefreshToken();
        StartCoroutine(GetBaiduRecognize(_clip, _callback));
    }

    /// <summary>
    /// 获取语音识别
    /// </summary>
    /// <param name="_callback"></param>
    /// <returns></returns>
    private IEnumerator GetBaiduRecognize(AudioClip _audioClip, System.Action<string> _callback)
    {
        stopwatch.Restart();

        // 检查音频参数
        if (_audioClip == null || _audioClip.samples == 0)
        {
            Debug.LogError("音频数据为空或无效");
            Debug.LogError("[ASR:Baidu] ????????samples=0?");
            OperationLogger.LogSystem("[ASR:Baidu] ????????samples=0?");
            _callback("音频数据无效");
            yield break;
        }

        // 检查token
        if (string.IsNullOrEmpty(m_Settings.m_Token))
        {
            Debug.LogError("Token为空，请先获取Token");
            Debug.LogError("[ASR:Baidu] Token ??????? Token");
            OperationLogger.LogSystem("[ASR:Baidu] Token ??????? Token");
            _callback("Token无效");
            yield break;
        }

        // 音频数据转换
        float[] samples = new float[_audioClip.samples];
        _audioClip.GetData(samples, 0);
        var samplesShort = new short[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            samplesShort[index] = (short)(samples[index] * short.MaxValue);
        }
        byte[] datas = new byte[samplesShort.Length * 2];
        Buffer.BlockCopy(samplesShort, 0, datas, 0, datas.Length);

        // 使用百度推荐的 JSON 接口格式，而不是表单上传，避免 3307 等识别错误
        var requestBody = new BaiduAsrRequest
        {
            format = "pcm",
            rate = 16000,
            channel = 1,
            token = m_Settings.m_Token,
            cuid = SystemInfo.deviceUniqueIdentifier,
            dev_pid = 1537, // 中文普通话
            len = datas.Length,
            speech = Convert.ToBase64String(datas)
        };

        string json = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        Debug.Log($"准备请求语音识别API，URL: {m_SpeechRecognizeURL}");
        Debug.Log($"音频数据长度: {datas.Length} bytes, JSON长度: {bodyRaw.Length} bytes");
        Debug.Log($"[ASR:Baidu] POST {m_SpeechRecognizeURL} audio_bytes={datas.Length} json_bytes={bodyRaw.Length}");
        OperationLogger.LogSystem($"[ASR:Baidu] POST {m_SpeechRecognizeURL} audio_bytes={datas.Length} json_bytes={bodyRaw.Length}");

        using (UnityWebRequest unityWebRequest = new UnityWebRequest(m_SpeechRecognizeURL, "POST"))
        {
            unityWebRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            unityWebRequest.downloadHandler = new DownloadHandlerBuffer();
            unityWebRequest.SetRequestHeader("Content-Type", "application/json");

            // 发送请求
            yield return unityWebRequest.SendWebRequest();

            // 处理响应
            string asrResult;
            try
            {
                if (unityWebRequest.result == UnityWebRequest.Result.Success)
                {
                    asrResult = unityWebRequest.downloadHandler.text;
                    Debug.Log($"语音识别返回: {asrResult}");
                    Debug.Log($"[ASR:Baidu] Response OK http={(long)unityWebRequest.responseCode} bytes={(asrResult ?? "").Length}");

                    RecogizeBackData _data = JsonUtility.FromJson<RecogizeBackData>(asrResult);
                    if (_data != null && _data.err_no == "0")
                    {
                        string text = (_data.result != null && _data.result.Count > 0) ? _data.result[0] : "";
                        OperationLogger.LogSystem($"[ASR:Baidu] OK err_no=0 text='{text}'");
                        _callback(_data.result[0]);
                    }
                    else
                    {
                        string errNo = _data != null ? _data.err_no : "unknown";
                        string errMsg = _data != null ? _data.err_msg : "解析失败";
                        Debug.LogError($"语音识别失败，错误码: {errNo}, 错误信息: {errMsg}");
                        Debug.LogError($"[ASR:Baidu] Failed err_no={errNo} err_msg='{errMsg}'");
                        OperationLogger.LogSystem($"[ASR:Baidu] Failed err_no={errNo} err_msg='{errMsg}'");
                        _callback($"语音识别失败: {errMsg}");
                    }
                }
                else
                {
                    Debug.LogError($"网络请求失败: {unityWebRequest.error}");
                    Debug.LogError($"[ASR:Baidu] Network failed: {unityWebRequest.error}");
                    OperationLogger.LogSystem($"[ASR:Baidu] Network failed: {unityWebRequest.error}");
                    _callback($"请求失败: {unityWebRequest.error}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理响应异常: {ex.Message}");
                Debug.LogError($"[ASR:Baidu] Exception: {ex.Message}");
                OperationLogger.LogSystem($"[ASR:Baidu] Exception: {ex.Message}");
                _callback($"处理响应异常: {ex.Message}");
            }
        }

        stopwatch.Stop();
        Debug.Log("语音识别耗时：" + stopwatch.Elapsed.TotalSeconds + "秒");
        Debug.Log($"[ASR:Baidu] SpeechToText elapsed={stopwatch.Elapsed.TotalSeconds:F3}s");
        OperationLogger.LogSystem($"[ASR:Baidu] elapsed={stopwatch.Elapsed.TotalSeconds:F3}s");
    }

    #endregion

    [System.Serializable]
    public class RecogizeBackData
    {
        public string corpus_no = string.Empty;
        public string err_msg = string.Empty;
        public string err_no = string.Empty;
        public List<string> result;
        public string sn = string.Empty;
    }

    /// <summary>
    /// 返回的token
    /// </summary>
    [System.Serializable]
    public class TokenInfo
    {
        public string access_token = string.Empty;
    }

    /// <summary>
    /// 百度语音识别 JSON 接口请求体
    /// </summary>
    [System.Serializable]
    private class BaiduAsrRequest
    {
        public string format;
        public int rate;
        public int channel;
        public string token;
        public string cuid;
        public int dev_pid;
        public int len;
        public string speech;
    }
}