using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System;

/// <summary>
/// Doubao（火山引擎）语音克隆功能
/// 用于复刻指定音频文件中人声的音色
/// </summary>
public class DoubaoVoiceClone : MonoBehaviour
{
    // API配置（由 DoubaoSettings 统一管理，不显示在 Inspector 中）
    private string appId = "";
    private string token = "";
    private string cluster = "volcano_icl";
    
    [Header("克隆设置")]
    [Tooltip("模型类型：0=MEGA, 1=ICL1.0, 2=DiT标准版, 3=DiT还原版, 4=ICL2.0")]
    [SerializeField] private int modelType = 4;
    
    [Tooltip("语言：0=中文")]
    [SerializeField] private int language = 0;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // API端点
    private const string UPLOAD_ENDPOINT = "https://openspeech.bytedance.com/api/v1/mega_tts/audio/upload";
    private const string STATUS_ENDPOINT = "https://openspeech.bytedance.com/api/v1/mega_tts/status";
    
    // 克隆结果回调
    public System.Action<bool, string, CloneVoiceResponse> OnCloneComplete;
    public System.Action<bool, string, CloneStatusResponse> OnStatusQueryComplete;
    
    // 克隆响应数据结构
    [System.Serializable]
    public class CloneVoiceResponse
    {
        public int status_code;
        public string status_msg;
        public CloneVoiceData data;
    }
    
    [System.Serializable]
    public class CloneVoiceData
    {
        public string speaker_id;
    }
    
    [System.Serializable]
    public class CloneStatusResponse
    {
        public int status_code;
        public string status_msg;
        public CloneStatusData data;
    }
    
    [System.Serializable]
    public class CloneStatusData
    {
        public int status;  // 0=训练中, 1=成功, 2=失败
        public string speaker_id;
    }
    
    /// <summary>
    /// 从文件路径克隆音色
    /// </summary>
    /// <param name="audioFilePath">音频文件路径（支持 wav、mp3等）</param>
    /// <param name="speakerId">说话人ID（格式：S_xxxxx）</param>
    public void CloneVoiceFromFile(string audioFilePath, string speakerId)
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("DoubaoVoiceClone: AppID或Token未配置！");
            OnCloneComplete?.Invoke(false, "AppID或Token未配置", null);
            return;
        }
        
        if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
        {
            Debug.LogError($"DoubaoVoiceClone: 音频文件不存在: {audioFilePath}");
            OnCloneComplete?.Invoke(false, "音频文件不存在", null);
            return;
        }
        
        if (string.IsNullOrEmpty(speakerId))
        {
            Debug.LogError("DoubaoVoiceClone: Speaker ID不能为空！");
            OnCloneComplete?.Invoke(false, "Speaker ID不能为空", null);
            return;
        }
        
        // 验证Speaker ID格式（应该以S_开头）
        if (!speakerId.StartsWith("S_"))
        {
            Debug.LogWarning("DoubaoVoiceClone: Speaker ID建议以'S_'开头");
        }
        
        StartCoroutine(CloneVoiceCoroutine(audioFilePath, speakerId));
    }
    
    /// <summary>
    /// 从AudioClip克隆音色
    /// </summary>
    /// <param name="audioClip">Unity AudioClip对象</param>
    /// <param name="speakerId">说话人ID</param>
    /// <param name="audioFormat">音频格式（wav/mp3等）</param>
    public void CloneVoiceFromAudioClip(AudioClip audioClip, string speakerId, string audioFormat = "wav")
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("DoubaoVoiceClone: AppID或Token未配置！");
            OnCloneComplete?.Invoke(false, "AppID或Token未配置", null);
            return;
        }
        
        if (audioClip == null)
        {
            Debug.LogError("DoubaoVoiceClone: AudioClip为空！");
            OnCloneComplete?.Invoke(false, "AudioClip为空", null);
            return;
        }
        
        if (string.IsNullOrEmpty(speakerId))
        {
            Debug.LogError("DoubaoVoiceClone: Speaker ID不能为空！");
            OnCloneComplete?.Invoke(false, "Speaker ID不能为空", null);
            return;
        }
        
        // 将AudioClip转换为WAV字节数组
        byte[] audioData = ConvertAudioClipToWAV(audioClip);
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("DoubaoVoiceClone: AudioClip转换失败！");
            OnCloneComplete?.Invoke(false, "AudioClip转换失败", null);
            return;
        }
        
        StartCoroutine(CloneVoiceFromBytesCoroutine(audioData, speakerId, audioFormat));
    }
    
    /// <summary>
    /// 查询克隆状态
    /// </summary>
    /// <param name="speakerId">说话人ID</param>
    public void QueryCloneStatus(string speakerId)
    {
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("DoubaoVoiceClone: AppID或Token未配置！");
            OnStatusQueryComplete?.Invoke(false, "AppID或Token未配置", null);
            return;
        }
        
        if (string.IsNullOrEmpty(speakerId))
        {
            Debug.LogError("DoubaoVoiceClone: Speaker ID不能为空！");
            OnStatusQueryComplete?.Invoke(false, "Speaker ID不能为空", null);
            return;
        }
        
        StartCoroutine(QueryStatusCoroutine(speakerId));
    }
    
    /// <summary>
    /// 克隆音色协程（从文件路径）
    /// </summary>
    private IEnumerator CloneVoiceCoroutine(string audioFilePath, string speakerId)
    {
        // 读取音频文件
        byte[] audioData = File.ReadAllBytes(audioFilePath);
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError($"DoubaoVoiceClone: 读取音频文件失败: {audioFilePath}");
            OnCloneComplete?.Invoke(false, "读取音频文件失败", null);
            yield break;
        }
        
        // 获取文件扩展名作为格式
        string audioFormat = Path.GetExtension(audioFilePath).TrimStart('.').ToLower();
        if (string.IsNullOrEmpty(audioFormat))
        {
            audioFormat = "wav"; // 默认格式
        }
        
        yield return StartCoroutine(CloneVoiceFromBytesCoroutine(audioData, speakerId, audioFormat));
    }
    
    /// <summary>
    /// 克隆音色协程（从字节数组）
    /// </summary>
    private IEnumerator CloneVoiceFromBytesCoroutine(byte[] audioData, string speakerId, string audioFormat)
    {
        // Base64编码音频数据
        string encodedAudio = Convert.ToBase64String(audioData);
        
        // 构建请求数据
        CloneVoiceRequest request = new CloneVoiceRequest
        {
            appid = appId,
            speaker_id = speakerId,
            audios = new List<AudioData>
            {
                new AudioData
                {
                    audio_bytes = encodedAudio,
                    audio_format = audioFormat
                }
            },
            source = 2,
            language = language,
            model_type = modelType
        };
        
        string jsonRequest = JsonConvert.SerializeObject(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoVoiceClone: 开始克隆音色 - Speaker ID: {speakerId}, 格式: {audioFormat}, 大小: {audioData.Length} 字节");
        }
        
        // 发送请求
        using (UnityWebRequest www = new UnityWebRequest(UPLOAD_ENDPOINT, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer;{token}");
            www.SetRequestHeader("Resource-Id", "volc.megatts.voiceclone");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"请求失败: {www.error}";
                if (enableDebug)
                {
                    Debug.LogError($"DoubaoVoiceClone: {errorMsg}");
                    Debug.LogError($"响应内容: {www.downloadHandler?.text}");
                }
                OnCloneComplete?.Invoke(false, errorMsg, null);
                yield break;
            }
            
            string responseText = www.downloadHandler.text;
            
            if (enableDebug)
            {
                Debug.Log($"DoubaoVoiceClone: 响应内容: {responseText}");
            }
            
            // 解析响应
            try
            {
                CloneVoiceResponse response = JsonConvert.DeserializeObject<CloneVoiceResponse>(responseText);
                
                if (response == null)
                {
                    Debug.LogError("DoubaoVoiceClone: 响应解析失败，响应为null");
                    OnCloneComplete?.Invoke(false, "响应解析失败", null);
                    yield break;
                }
                
                // 检查状态码
                if (response.status_code == 0)
                {
                    if (enableDebug)
                    {
                        Debug.Log($"DoubaoVoiceClone: 克隆训练已提交！Speaker ID: {response.data?.speaker_id}");
                        Debug.Log($"DoubaoVoiceClone: 请使用QueryCloneStatus查询训练状态");
                    }
                    OnCloneComplete?.Invoke(true, "克隆训练已提交", response);
                }
                else
                {
                    string errorMsg = $"克隆失败 (Code: {response.status_code}): {response.status_msg}";
                    Debug.LogError($"DoubaoVoiceClone: {errorMsg}");
                    OnCloneComplete?.Invoke(false, errorMsg, response);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"DoubaoVoiceClone: 解析响应异常: {e.Message}");
                OnCloneComplete?.Invoke(false, $"解析响应异常: {e.Message}", null);
            }
        }
    }
    
    /// <summary>
    /// 查询克隆状态协程
    /// </summary>
    private IEnumerator QueryStatusCoroutine(string speakerId)
    {
        // 构建请求数据
        StatusQueryRequest request = new StatusQueryRequest
        {
            appid = appId,
            speaker_id = speakerId
        };
        
        string jsonRequest = JsonConvert.SerializeObject(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
        
        if (enableDebug)
        {
            Debug.Log($"DoubaoVoiceClone: 查询克隆状态 - Speaker ID: {speakerId}");
        }
        
        // 发送请求
        using (UnityWebRequest www = new UnityWebRequest(STATUS_ENDPOINT, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer;{token}");
            www.SetRequestHeader("Resource-Id", "volc.megatts.voiceclone");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"请求失败: {www.error}";
                if (enableDebug)
                {
                    Debug.LogError($"DoubaoVoiceClone: {errorMsg}");
                    Debug.LogError($"响应内容: {www.downloadHandler?.text}");
                }
                OnStatusQueryComplete?.Invoke(false, errorMsg, null);
                yield break;
            }
            
            string responseText = www.downloadHandler.text;
            
            if (enableDebug)
            {
                Debug.Log($"DoubaoVoiceClone: 状态响应: {responseText}");
            }
            
            // 解析响应
            try
            {
                CloneStatusResponse response = JsonConvert.DeserializeObject<CloneStatusResponse>(responseText);
                
                if (response == null)
                {
                    Debug.LogError("DoubaoVoiceClone: 响应解析失败，响应为null");
                    OnStatusQueryComplete?.Invoke(false, "响应解析失败", null);
                    yield break;
                }
                
                // 检查状态码
                if (response.status_code == 0)
                {
                    string statusText = "";
                    if (response.data != null)
                    {
                        switch (response.data.status)
                        {
                            case 0:
                                statusText = "训练中";
                                break;
                            case 1:
                                statusText = "训练成功";
                                break;
                            case 2:
                                statusText = "训练失败";
                                break;
                            default:
                                statusText = $"未知状态({response.data.status})";
                                break;
                        }
                    }
                    
                    if (enableDebug)
                    {
                        Debug.Log($"DoubaoVoiceClone: 克隆状态: {statusText}");
                    }
                    OnStatusQueryComplete?.Invoke(true, statusText, response);
                }
                else
                {
                    string errorMsg = $"查询失败 (Code: {response.status_code}): {response.status_msg}";
                    Debug.LogError($"DoubaoVoiceClone: {errorMsg}");
                    OnStatusQueryComplete?.Invoke(false, errorMsg, response);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"DoubaoVoiceClone: 解析响应异常: {e.Message}");
                OnStatusQueryComplete?.Invoke(false, $"解析响应异常: {e.Message}", null);
            }
        }
    }
    
    /// <summary>
    /// 将AudioClip转换为WAV格式的字节数组
    /// </summary>
    private byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        try
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            // 转换为16位PCM
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(samples[i] * 32767f);
                pcmData[i * 2] = (byte)(sample & 0xFF);
                pcmData[i * 2 + 1] = (byte)(sample >> 8);
            }
            
            // 构建WAV文件头
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            
            // RIFF头
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            
            // fmt chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // fmt chunk size
            writer.Write((ushort)1); // audio format (1 = PCM)
            writer.Write((ushort)clip.channels); // channels
            writer.Write(clip.frequency); // sample rate
            writer.Write(clip.frequency * clip.channels * 2); // byte rate
            writer.Write((ushort)(clip.channels * 2)); // block align
            writer.Write((ushort)16); // bits per sample
            
            // data chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmData.Length);
            writer.Write(pcmData);
            
            return stream.ToArray();
        }
        catch (Exception e)
        {
            Debug.LogError($"DoubaoVoiceClone: AudioClip转换异常: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 设置AppID和Token
    /// </summary>
    public void SetCredentials(string appId, string token)
    {
        this.appId = appId;
        this.token = token;
        if (enableDebug)
        {
            Debug.Log("DoubaoVoiceClone: 凭证已更新");
        }
    }
    
    // 请求数据结构
    [System.Serializable]
    private class CloneVoiceRequest
    {
        public string appid;
        public string speaker_id;
        public List<AudioData> audios;
        public int source;
        public int language;
        public int model_type;
    }
    
    [System.Serializable]
    private class AudioData
    {
        public string audio_bytes;
        public string audio_format;
    }
    
    [System.Serializable]
    private class StatusQueryRequest
    {
        public string appid;
        public string speaker_id;
    }
}
