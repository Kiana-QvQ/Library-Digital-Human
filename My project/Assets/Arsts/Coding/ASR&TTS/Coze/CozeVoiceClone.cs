using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;
using System.IO;

/// <summary>
/// Coze 语音克隆功能
/// 用于复刻指定音频文件中人声的音色
/// </summary>
public class CozeVoiceClone : MonoBehaviour
{
    [Header("API配置")]
    [Tooltip("Coze API Key (Personal Access Token). 获取方式: https://www.coze.cn -> 开发者中心 -> API管理")]
    [SerializeField] private string apiKey = "";
    
    [Tooltip("克隆音色保存的扣子编程工作空间 ID，默认保存在当前账号的个人空间中")]
    [SerializeField] private string spaceId = "";
    
    [Header("克隆设置")]
    [Tooltip("音频文件对应的文案。需要和音频文件中人声朗读的内容大致一致")]
    [SerializeField] private string text = "";
    
    [Tooltip("音频文件中的语种")]
    [SerializeField] private LanguageType language = LanguageType.zh;
    
    [Tooltip("预览音频的文案")]
    [SerializeField] private string previewText = "你好，我是你的专属AI克隆声音，希望未来可以一起好好相处哦";
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // API端点
    private const string CLONE_VOICE_ENDPOINT = "https://api.coze.cn/v1/audio/voices/clone";
    
    // 支持的语言类型
    public enum LanguageType
    {
        zh,  // 中文
        en,  // 英文
        ja,  // 日语
        es,  // 西班牙语
        id,  // 印度尼西亚语
        pt   // 葡萄牙语
    }
    
    // 克隆结果回调
    public System.Action<bool, string, CloneVoiceResponse> OnCloneComplete;
    
    // 克隆响应数据结构
    [System.Serializable]
    public class CloneVoiceResponse
    {
        public long code;
        public string msg;
        public CloneVoiceData data;
        public ResponseDetail detail;
    }
    
    [System.Serializable]
    public class CloneVoiceData
    {
        public string voice_id;
    }
    
    [System.Serializable]
    public class ResponseDetail
    {
        public string logid;
    }
    
    /// <summary>
    /// 从文件路径克隆音色
    /// </summary>
    /// <param name="audioFilePath">音频文件路径（支持 wav、mp3、ogg、m4a、aac、pcm）</param>
    /// <param name="voiceName">音色名称（必选，长度限制128字节）</param>
    /// <param name="text">音频文件对应的文案（可选）</param>
    /// <param name="language">语种（可选）</param>
    /// <param name="voiceId">需要训练的音色ID（可选，用于重新训练）</param>
    /// <param name="previewText">预览音频的文案（可选）</param>
    /// <param name="spaceId">工作空间ID（可选）</param>
    public void CloneVoiceFromFile(string audioFilePath, string voiceName, 
        string text = null, LanguageType? language = null, 
        string voiceId = null, string previewText = null, string spaceId = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("CozeVoiceClone: API Key未配置！");
            OnCloneComplete?.Invoke(false, "API Key未配置", null);
            return;
        }
        
        if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
        {
            Debug.LogError($"CozeVoiceClone: 音频文件不存在: {audioFilePath}");
            OnCloneComplete?.Invoke(false, "音频文件不存在", null);
            return;
        }
        
        // 验证文件格式
        if (!IsSupportedAudioFormat(audioFilePath))
        {
            Debug.LogError($"CozeVoiceClone: 不支持的音频格式。支持格式: wav, mp3, ogg, m4a, aac, pcm");
            OnCloneComplete?.Invoke(false, "不支持的音频格式", null);
            return;
        }
        
        // 验证文件大小
        if (!ValidateFileSize(audioFilePath, out long fileSize))
        {
            Debug.LogError($"CozeVoiceClone: 音频文件过大 ({fileSize / 1024 / 1024}MB)，最大支持10MB");
            OnCloneComplete?.Invoke(false, "音频文件过大，最大支持10MB", null);
            return;
        }
        
        if (string.IsNullOrEmpty(voiceName))
        {
            Debug.LogError("CozeVoiceClone: 音色名称不能为空！");
            OnCloneComplete?.Invoke(false, "音色名称不能为空", null);
            return;
        }
        
        // 验证音色名称长度（128字节）
        if (Encoding.UTF8.GetByteCount(voiceName) > 128)
        {
            Debug.LogError("CozeVoiceClone: 音色名称长度超过128字节限制！");
            OnCloneComplete?.Invoke(false, "音色名称长度超过128字节限制", null);
            return;
        }
        
        StartCoroutine(CloneVoiceCoroutine(audioFilePath, voiceName, text, language, voiceId, previewText, spaceId));
    }
    
    /// <summary>
    /// 从AudioClip克隆音色
    /// </summary>
    /// <param name="audioClip">Unity AudioClip对象</param>
    /// <param name="voiceName">音色名称（必选）</param>
    /// <param name="text">音频文件对应的文案（可选）</param>
    /// <param name="language">语种（可选）</param>
    /// <param name="voiceId">需要训练的音色ID（可选）</param>
    /// <param name="previewText">预览音频的文案（可选）</param>
    /// <param name="spaceId">工作空间ID（可选）</param>
    /// <param name="audioFormat">音频格式（wav/mp3等）</param>
    public void CloneVoiceFromAudioClip(AudioClip audioClip, string voiceName,
        string text = null, LanguageType? language = null,
        string voiceId = null, string previewText = null, string spaceId = null,
        string audioFormat = "wav")
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("CozeVoiceClone: API Key未配置！");
            OnCloneComplete?.Invoke(false, "API Key未配置", null);
            return;
        }
        
        if (audioClip == null)
        {
            Debug.LogError("CozeVoiceClone: AudioClip为空！");
            OnCloneComplete?.Invoke(false, "AudioClip为空", null);
            return;
        }
        
        if (string.IsNullOrEmpty(voiceName))
        {
            Debug.LogError("CozeVoiceClone: 音色名称不能为空！");
            OnCloneComplete?.Invoke(false, "音色名称不能为空", null);
            return;
        }
        
        // 验证音色名称长度（128字节）
        if (Encoding.UTF8.GetByteCount(voiceName) > 128)
        {
            Debug.LogError("CozeVoiceClone: 音色名称长度超过128字节限制！");
            OnCloneComplete?.Invoke(false, "音色名称长度超过128字节限制", null);
            return;
        }
        
        // 将AudioClip转换为WAV字节数组
        byte[] audioData = ConvertAudioClipToWAV(audioClip);
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("CozeVoiceClone: AudioClip转换失败！");
            OnCloneComplete?.Invoke(false, "AudioClip转换失败", null);
            return;
        }
        
        StartCoroutine(CloneVoiceFromBytesCoroutine(audioData, voiceName, audioFormat, 
            text, language, voiceId, previewText, spaceId));
    }
    
    /// <summary>
    /// 克隆音色协程（从文件路径）
    /// </summary>
    private IEnumerator CloneVoiceCoroutine(string audioFilePath, string voiceName,
        string text, LanguageType? language, string voiceId, 
        string previewText, string spaceId)
    {
        // 读取音频文件
        byte[] audioData = File.ReadAllBytes(audioFilePath);
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError($"CozeVoiceClone: 读取音频文件失败: {audioFilePath}");
            OnCloneComplete?.Invoke(false, "读取音频文件失败", null);
            yield break;
        }
        
        // 文件大小已在调用前验证，这里再次确认
        if (audioData.Length > 10 * 1024 * 1024)
        {
            Debug.LogError($"CozeVoiceClone: 音频文件过大 ({audioData.Length / 1024 / 1024}MB)，最大支持10MB");
            OnCloneComplete?.Invoke(false, "音频文件过大，最大支持10MB", null);
            yield break;
        }
        
        // 获取文件扩展名作为格式
        string audioFormat = Path.GetExtension(audioFilePath).TrimStart('.').ToLower();
        if (string.IsNullOrEmpty(audioFormat))
        {
            audioFormat = "wav"; // 默认格式
        }
        
        yield return StartCoroutine(CloneVoiceFromBytesCoroutine(audioData, voiceName, audioFormat,
            text, language, voiceId, previewText, spaceId));
    }
    
    /// <summary>
    /// 克隆音色协程（从字节数组）
    /// </summary>
    private IEnumerator CloneVoiceFromBytesCoroutine(byte[] audioData, string voiceName, string audioFormat,
        string text, LanguageType? language, string voiceId,
        string previewText, string spaceId)
    {
        // 构建multipart form data
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        
        // 必选参数：音色名称
        formData.Add(new MultipartFormDataSection("voice_name", voiceName));
        
        // 可选参数：文案（最大1024字节）
        string finalText = text ?? this.text;
        if (!string.IsNullOrEmpty(finalText))
        {
            int textByteLength = Encoding.UTF8.GetByteCount(finalText);
            if (textByteLength > 1024)
            {
                Debug.LogWarning($"CozeVoiceClone: 文案长度 ({textByteLength} 字节) 超过1024字节限制，将被截断");
                // 截断到1024字节
                byte[] textBytes = Encoding.UTF8.GetBytes(finalText);
                if (textBytes.Length > 1024)
                {
                    // 找到最后一个完整的UTF-8字符边界
                    int truncateLength = 1024;
                    while (truncateLength > 0 && (textBytes[truncateLength] & 0xC0) == 0x80)
                    {
                        truncateLength--;
                    }
                    finalText = Encoding.UTF8.GetString(textBytes, 0, truncateLength);
                }
            }
            formData.Add(new MultipartFormDataSection("text", finalText));
        }
        
        // 可选参数：语种
        string languageStr = (language ?? this.language).ToString();
        formData.Add(new MultipartFormDataSection("language", languageStr));
        
        // 可选参数：音色ID（用于重新训练）
        if (!string.IsNullOrEmpty(voiceId))
        {
            formData.Add(new MultipartFormDataSection("voice_id", voiceId));
        }
        
        // 可选参数：预览文案
        string finalPreviewText = previewText ?? this.previewText;
        if (!string.IsNullOrEmpty(finalPreviewText))
        {
            formData.Add(new MultipartFormDataSection("preview_text", finalPreviewText));
        }
        
        // 可选参数：工作空间ID
        string finalSpaceId = spaceId ?? this.spaceId;
        if (!string.IsNullOrEmpty(finalSpaceId))
        {
            formData.Add(new MultipartFormDataSection("space_id", finalSpaceId));
        }
        
        // 音频格式
        formData.Add(new MultipartFormDataSection("audio_format", audioFormat));
        
        // 音频文件（使用MultipartFormFileSection）
        // 文件名格式：audio.扩展名（例如：audio.mp3, audio.wav）
        string fileName = $"audio.{audioFormat}";
        string contentType = GetContentType(audioFormat);
        formData.Add(new MultipartFormFileSection("file", audioData, fileName, contentType));
        
        if (enableDebug)
        {
            Debug.Log($"CozeVoiceClone: 开始克隆音色 - 名称: {voiceName}, 格式: {audioFormat}, 大小: {audioData.Length} 字节");
        }
        
        // 发送请求
        using (UnityWebRequest www = UnityWebRequest.Post(CLONE_VOICE_ENDPOINT, formData))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"请求失败: {www.error}";
                if (enableDebug)
                {
                    Debug.LogError($"CozeVoiceClone: {errorMsg}");
                    Debug.LogError($"响应内容: {www.downloadHandler?.text}");
                }
                OnCloneComplete?.Invoke(false, errorMsg, null);
                yield break;
            }
            
            string responseText = www.downloadHandler.text;
            
            if (enableDebug)
            {
                Debug.Log($"CozeVoiceClone: 响应内容: {responseText}");
            }
            
            // 解析响应
            try
            {
                CloneVoiceResponse response = JsonConvert.DeserializeObject<CloneVoiceResponse>(responseText);
                
                if (response == null)
                {
                    Debug.LogError("CozeVoiceClone: 响应解析失败，响应为null");
                    OnCloneComplete?.Invoke(false, "响应解析失败", null);
                    yield break;
                }
                
                // 检查状态码
                if (response.code == 0)
                {
                    if (enableDebug)
                    {
                        Debug.Log($"CozeVoiceClone: 克隆成功！音色ID: {response.data?.voice_id}");
                        if (response.detail != null && !string.IsNullOrEmpty(response.detail.logid))
                        {
                            Debug.Log($"CozeVoiceClone: 日志ID: {response.detail.logid}");
                        }
                    }
                    OnCloneComplete?.Invoke(true, "克隆成功", response);
                }
                else
                {
                    string errorMsg = $"克隆失败 (Code: {response.code}): {response.msg}";
                    if (response.detail != null && !string.IsNullOrEmpty(response.detail.logid))
                    {
                        errorMsg += $"\n日志ID: {response.detail.logid}";
                    }
                    Debug.LogError($"CozeVoiceClone: {errorMsg}");
                    OnCloneComplete?.Invoke(false, errorMsg, response);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CozeVoiceClone: 解析响应异常: {e.Message}");
                OnCloneComplete?.Invoke(false, $"解析响应异常: {e.Message}", null);
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
        catch (System.Exception e)
        {
            Debug.LogError($"CozeVoiceClone: AudioClip转换异常: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 设置API Key
    /// </summary>
    public void SetAPIKey(string key)
    {
        apiKey = key;
        if (enableDebug)
        {
            Debug.Log("CozeVoiceClone: API Key已更新");
        }
    }
    
    /// <summary>
    /// 获取当前API Key（用于显示，隐藏敏感信息）
    /// </summary>
    public string GetAPIKey()
    {
        return apiKey;
    }
    
    /// <summary>
    /// 从CozeAgentClient同步API Key
    /// </summary>
    public void SyncAPIKeyFromCozeClient()
    {
        CozeAgentClient cozeClient = FindObjectOfType<CozeAgentClient>();
        if (cozeClient != null)
        {
            // 通过反射获取API Key（因为CozeAgentClient的apiKey是private）
            var apiKeyField = typeof(CozeAgentClient).GetField("apiKey", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (apiKeyField != null)
            {
                string key = apiKeyField.GetValue(cozeClient) as string;
                if (!string.IsNullOrEmpty(key))
                {
                    SetAPIKey(key);
                    if (enableDebug)
                    {
                        Debug.Log("CozeVoiceClone: 已从CozeAgentClient同步API Key");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 从CozeVoiceAPI同步API Key
    /// </summary>
    public void SyncAPIKeyFromVoiceAPI(CozeVoiceAPI voiceAPI)
    {
        if (voiceAPI != null)
        {
            string key = voiceAPI.GetAPIKey();
            if (!string.IsNullOrEmpty(key))
            {
                SetAPIKey(key);
                if (enableDebug)
                {
                    Debug.Log("CozeVoiceClone: 已从CozeVoiceAPI同步API Key");
                }
            }
        }
    }
    
    /// <summary>
    /// 设置工作空间ID
    /// </summary>
    public void SetSpaceId(string id)
    {
        spaceId = id;
        if (enableDebug)
        {
            Debug.Log($"CozeVoiceClone: 工作空间ID已更新: {id}");
        }
    }
    
    /// <summary>
    /// 根据音频格式获取Content-Type
    /// </summary>
    private string GetContentType(string audioFormat)
    {
        switch (audioFormat.ToLower())
        {
            case "wav":
                return "audio/wav";
            case "mp3":
                return "audio/mpeg";
            case "ogg":
                return "audio/ogg";
            case "m4a":
                return "audio/mp4";
            case "aac":
                return "audio/aac";
            case "pcm":
                return "audio/pcm";
            default:
                return "application/octet-stream";
        }
    }
    
    /// <summary>
    /// 验证音频文件格式是否支持
    /// </summary>
    public static bool IsSupportedAudioFormat(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
        
        string extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
        string[] supportedFormats = { "wav", "mp3", "ogg", "m4a", "aac", "pcm" };
        
        foreach (string format in supportedFormats)
        {
            if (extension == format)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 验证音频文件大小（最大10MB）
    /// </summary>
    public static bool ValidateFileSize(string filePath, out long fileSize)
    {
        fileSize = 0;
        
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;
        
        FileInfo fileInfo = new FileInfo(filePath);
        fileSize = fileInfo.Length;
        
        return fileSize <= 10 * 1024 * 1024; // 10MB
    }
}
