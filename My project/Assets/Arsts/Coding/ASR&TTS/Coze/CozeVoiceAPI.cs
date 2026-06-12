using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Coze 语音API统一管理类
/// 包含音色列表查询等功能
/// </summary>
public class CozeVoiceAPI : MonoBehaviour
{
    [Header("API配置")]
    [Tooltip("Coze API Key (Personal Access Token). 获取方式: https://www.coze.cn -> 开发者中心 -> API管理")]
    [SerializeField] private string apiKey = "";
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebug = true;
    
    // API端点
    private const string LIST_VOICES_ENDPOINT = "https://api.coze.cn/v1/audio/voices";
    
    // 查询结果回调
    public System.Action<bool, string, ListVoiceResponse> OnListVoicesComplete;
    
    // 音色列表响应数据结构
    [System.Serializable]
    public class ListVoiceResponse
    {
        public long code;
        public string msg;
        public ListVoiceData data;
        public ResponseDetail detail;
    }
    
    [System.Serializable]
    public class ListVoiceData
    {
        public bool has_more;
        public List<OpenAPIVoiceData> voice_list;
    }
    
    [System.Serializable]
    public class OpenAPIVoiceData
    {
        public string name;
        public string state;
        public string voice_id;
        public string model_name;
        public string model_type;
        public long create_time;
        public long update_time;
        public string preview_text;
        public string language_code;
        public string language_name;
        public string preview_audio;
        public bool is_system_voice;
        public List<EmotionInfo> support_emotions;
        public int available_training_times;
    }
    
    [System.Serializable]
    public class EmotionInfo
    {
        public string emotion;
        public string display_name;
        public EmotionScaleInterval emotion_scale_interval;
    }
    
    [System.Serializable]
    public class EmotionScaleInterval
    {
        public double max;
        public double min;
        public double @default;
    }
    
    [System.Serializable]
    public class ResponseDetail
    {
        public string logid;
    }
    
    // 查询参数结构
    [System.Serializable]
    public class VoiceListQueryParams
    {
        public bool? filter_system_voice = null;  // 是否过滤系统音色
        public string model_type = null;           // 模型类型: big/small
        public string voice_state = null;          // 音色状态: init/cloned/all
        public int page_num = 1;                   // 页码，最小值为1
        public int page_size = 100;                // 每页数量，1~100
    }
    
    /// <summary>
    /// 查询音色列表
    /// </summary>
    /// <param name="filterSystemVoice">是否过滤系统音色</param>
    /// <param name="modelType">模型类型 (big/small)</param>
    /// <param name="voiceState">音色状态 (init/cloned/all)</param>
    /// <param name="pageNum">页码，最小值为1</param>
    /// <param name="pageSize">每页数量，1~100</param>
    public void ListVoices(bool? filterSystemVoice = null, string modelType = null, 
        string voiceState = null, int pageNum = 1, int pageSize = 100)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("CozeVoiceAPI: API Key未配置！");
            OnListVoicesComplete?.Invoke(false, "API Key未配置", null);
            return;
        }
        
        // 验证参数
        if (pageNum < 1)
        {
            Debug.LogWarning("CozeVoiceAPI: pageNum 最小值为1，已自动调整为1");
            pageNum = 1;
        }
        
        if (pageSize < 1 || pageSize > 100)
        {
            Debug.LogWarning("CozeVoiceAPI: pageSize 必须在1~100之间，已自动调整为100");
            pageSize = 100;
        }
        
        StartCoroutine(ListVoicesCoroutine(filterSystemVoice, modelType, voiceState, pageNum, pageSize));
    }
    
    /// <summary>
    /// 使用查询参数对象查询音色列表
    /// </summary>
    /// <param name="queryParams">查询参数</param>
    public void ListVoices(VoiceListQueryParams queryParams)
    {
        if (queryParams == null)
        {
            queryParams = new VoiceListQueryParams();
        }
        
        ListVoices(queryParams.filter_system_voice, queryParams.model_type, 
            queryParams.voice_state, queryParams.page_num, queryParams.page_size);
    }
    
    /// <summary>
    /// 查询所有音色（自动处理分页）
    /// </summary>
    /// <param name="filterSystemVoice">是否过滤系统音色</param>
    /// <param name="modelType">模型类型</param>
    /// <param name="voiceState">音色状态</param>
    /// <param name="onComplete">完成回调，返回所有音色列表</param>
    public void ListAllVoices(bool? filterSystemVoice = null, string modelType = null, 
        string voiceState = null, System.Action<bool, string, List<OpenAPIVoiceData>> onComplete = null)
    {
        StartCoroutine(ListAllVoicesCoroutine(filterSystemVoice, modelType, voiceState, onComplete));
    }
    
    /// <summary>
    /// 查询音色列表协程
    /// </summary>
    private IEnumerator ListVoicesCoroutine(bool? filterSystemVoice, string modelType, 
        string voiceState, int pageNum, int pageSize)
    {
        // 构建查询URL
        StringBuilder urlBuilder = new StringBuilder(LIST_VOICES_ENDPOINT);
        List<string> queryParams = new List<string>();
        
        if (filterSystemVoice.HasValue)
        {
            queryParams.Add($"filter_system_voice={filterSystemVoice.Value.ToString().ToLower()}");
        }
        
        if (!string.IsNullOrEmpty(modelType))
        {
            queryParams.Add($"model_type={UnityWebRequest.EscapeURL(modelType)}");
        }
        
        if (!string.IsNullOrEmpty(voiceState))
        {
            queryParams.Add($"voice_state={UnityWebRequest.EscapeURL(voiceState)}");
        }
        
        queryParams.Add($"page_num={pageNum}");
        queryParams.Add($"page_size={pageSize}");
        
        if (queryParams.Count > 0)
        {
            urlBuilder.Append("?");
            urlBuilder.Append(string.Join("&", queryParams));
        }
        
        string url = urlBuilder.ToString();
        
        if (enableDebug)
        {
            Debug.Log($"CozeVoiceAPI: 查询音色列表 - URL: {url}");
        }
        
        // 发送GET请求
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"请求失败: {www.error}";
                if (enableDebug)
                {
                    Debug.LogError($"CozeVoiceAPI: {errorMsg}");
                    Debug.LogError($"响应内容: {www.downloadHandler?.text}");
                }
                OnListVoicesComplete?.Invoke(false, errorMsg, null);
                yield break;
            }
            
            string responseText = www.downloadHandler.text;
            
            if (enableDebug)
            {
                Debug.Log($"CozeVoiceAPI: 响应内容: {responseText}");
            }
            
            // 解析响应
            try
            {
                ListVoiceResponse response = JsonConvert.DeserializeObject<ListVoiceResponse>(responseText);
                
                if (response == null)
                {
                    Debug.LogError("CozeVoiceAPI: 响应解析失败，响应为null");
                    OnListVoicesComplete?.Invoke(false, "响应解析失败", null);
                    yield break;
                }
                
                // 检查状态码
                if (response.code == 0)
                {
                    int voiceCount = response.data?.voice_list?.Count ?? 0;
                    if (enableDebug)
                    {
                        Debug.Log($"CozeVoiceAPI: 查询成功！找到 {voiceCount} 个音色");
                        if (response.data.has_more)
                        {
                            Debug.Log($"CozeVoiceAPI: 还有更多音色数据，请使用分页查询");
                        }
                        if (response.detail != null && !string.IsNullOrEmpty(response.detail.logid))
                        {
                            Debug.Log($"CozeVoiceAPI: 日志ID: {response.detail.logid}");
                        }
                    }
                    OnListVoicesComplete?.Invoke(true, "查询成功", response);
                }
                else
                {
                    string errorMsg = $"查询失败 (Code: {response.code}): {response.msg}";
                    if (response.detail != null && !string.IsNullOrEmpty(response.detail.logid))
                    {
                        errorMsg += $"\n日志ID: {response.detail.logid}";
                    }
                    Debug.LogError($"CozeVoiceAPI: {errorMsg}");
                    OnListVoicesComplete?.Invoke(false, errorMsg, response);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CozeVoiceAPI: 解析响应异常: {e.Message}");
                OnListVoicesComplete?.Invoke(false, $"解析响应异常: {e.Message}", null);
            }
        }
    }
    
    /// <summary>
    /// 查询所有音色协程（自动处理分页）
    /// </summary>
    private IEnumerator ListAllVoicesCoroutine(bool? filterSystemVoice, string modelType, 
        string voiceState, System.Action<bool, string, List<OpenAPIVoiceData>> onComplete)
    {
        List<OpenAPIVoiceData> allVoices = new List<OpenAPIVoiceData>();
        int currentPage = 1;
        const int pageSize = 100;
        bool hasMore = true;
        
        while (hasMore)
        {
            bool requestComplete = false;
            bool requestSuccess = false;
            string errorMessage = "";
            ListVoiceResponse response = null;
            
            // 查询当前页
            ListVoices(filterSystemVoice, modelType, voiceState, currentPage, pageSize);
            
            // 等待回调
            System.Action<bool, string, ListVoiceResponse> originalCallback = OnListVoicesComplete;
            OnListVoicesComplete = (success, message, resp) =>
            {
                requestComplete = true;
                requestSuccess = success;
                errorMessage = message;
                response = resp;
            };
            
            // 等待请求完成
            float timeout = 30f; // 30秒超时
            float elapsed = 0f;
            while (!requestComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 恢复原始回调
            OnListVoicesComplete = originalCallback;
            
            if (!requestComplete)
            {
                Debug.LogError("CozeVoiceAPI: 查询超时");
                onComplete?.Invoke(false, "查询超时", null);
                yield break;
            }
            
            if (!requestSuccess)
            {
                Debug.LogError($"CozeVoiceAPI: 查询失败: {errorMessage}");
                onComplete?.Invoke(false, errorMessage, null);
                yield break;
            }
            
            if (response?.data?.voice_list != null)
            {
                allVoices.AddRange(response.data.voice_list);
                hasMore = response.data.has_more;
                currentPage++;
                
                if (enableDebug)
                {
                    Debug.Log($"CozeVoiceAPI: 已获取 {allVoices.Count} 个音色，还有更多: {hasMore}");
                }
            }
            else
            {
                hasMore = false;
            }
        }
        
        if (enableDebug)
        {
            Debug.Log($"CozeVoiceAPI: 查询完成，共获取 {allVoices.Count} 个音色");
        }
        
        onComplete?.Invoke(true, "查询成功", allVoices);
    }
    
    /// <summary>
    /// 根据音色ID查找音色信息
    /// </summary>
    /// <param name="voiceId">音色ID</param>
    /// <param name="onComplete">完成回调</param>
    public void FindVoiceById(string voiceId, System.Action<bool, OpenAPIVoiceData> onComplete)
    {
        if (string.IsNullOrEmpty(voiceId))
        {
            Debug.LogError("CozeVoiceAPI: 音色ID不能为空");
            onComplete?.Invoke(false, null);
            return;
        }
        
        StartCoroutine(FindVoiceByIdCoroutine(voiceId, onComplete));
    }
    
    /// <summary>
    /// 根据音色ID查找音色信息协程
    /// </summary>
    private IEnumerator FindVoiceByIdCoroutine(string voiceId, System.Action<bool, OpenAPIVoiceData> onComplete)
    {
        bool found = false;
        OpenAPIVoiceData foundVoice = null;
        
        // 查询所有音色
        ListAllVoices(onComplete: (success, message, voices) =>
        {
            if (success && voices != null)
            {
                foundVoice = voices.Find(v => v.voice_id == voiceId);
                found = foundVoice != null;
            }
        });
        
        // 等待查询完成
        float timeout = 60f; // 60秒超时
        float elapsed = 0f;
        while (!found && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (found)
        {
            onComplete?.Invoke(true, foundVoice);
        }
        else
        {
            Debug.LogWarning($"CozeVoiceAPI: 未找到音色ID: {voiceId}");
            onComplete?.Invoke(false, null);
        }
    }
    
    /// <summary>
    /// 获取自定义音色列表（过滤系统音色）
    /// </summary>
    /// <param name="onComplete">完成回调</param>
    public void GetCustomVoices(System.Action<bool, string, List<OpenAPIVoiceData>> onComplete)
    {
        ListAllVoices(filterSystemVoice: true, onComplete: onComplete);
    }
    
    /// <summary>
    /// 获取系统预置音色列表
    /// </summary>
    /// <param name="onComplete">完成回调</param>
    public void GetSystemVoices(System.Action<bool, string, List<OpenAPIVoiceData>> onComplete)
    {
        StartCoroutine(GetSystemVoicesCoroutine(onComplete));
    }
    
    /// <summary>
    /// 获取系统预置音色列表协程
    /// </summary>
    private IEnumerator GetSystemVoicesCoroutine(System.Action<bool, string, List<OpenAPIVoiceData>> onComplete)
    {
        List<OpenAPIVoiceData> systemVoices = new List<OpenAPIVoiceData>();
        
        // 查询所有音色，然后过滤
        ListAllVoices(onComplete: (success, message, voices) =>
        {
            if (success && voices != null)
            {
                systemVoices = voices.FindAll(v => v.is_system_voice);
            }
        });
        
        // 等待查询完成
        float timeout = 60f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        onComplete?.Invoke(true, "查询成功", systemVoices);
    }
    
    /// <summary>
    /// 设置API Key
    /// </summary>
    public void SetAPIKey(string key)
    {
        apiKey = key;
        if (enableDebug)
        {
            Debug.Log("CozeVoiceAPI: API Key已更新");
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
                        Debug.Log("CozeVoiceAPI: 已从CozeAgentClient同步API Key");
                    }
                }
            }
        }
    }
}
