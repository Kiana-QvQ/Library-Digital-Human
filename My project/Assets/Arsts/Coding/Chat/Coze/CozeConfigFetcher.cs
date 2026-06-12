// // NOTE:
// // 该脚本已废弃，逻辑由 CozeAgentClient.RefreshConfigFromBackend 替代。
// // 按照需求保留文件但不再参与编译，方便日后参考或恢复。

// #if false
// using System;
// using System.Collections;
// using UnityEngine;
// using UnityEngine.Networking;

// /// <summary>
// /// 从 digital_human_backend 拉取 Coze 配置并写入 PlayerPrefs（作为轻量持久化）。
// /// 失败时不会修改任何现有配置，保证当前逻辑正常工作。
// /// </summary>
// public class CozeConfigFetcher : MonoBehaviour
// {
//     [Header("后端配置接口")]
//     [Tooltip("例如：http://127.0.0.1:8000/api/config/coze")]
//     [SerializeField] private string backendConfigUrl = "http://127.0.0.1:8000/api/config/coze";

//     [Serializable]
//     private class CozeConfigDto
//     {
//         public string cozeApiKey;
//         public string cozeAgentId;
//         public string cozeBaseUrl;
//     }

//     private void Start()
//     {
//         if (string.IsNullOrEmpty(backendConfigUrl))
//         {
//             Debug.LogWarning("CozeConfigFetcher: backendConfigUrl 未配置，将使用本地默认配置。");
//             return;
//         }

//         Debug.Log($"[CozeConfigFetcher] Fetching Coze config from '{backendConfigUrl}'", this);
//         StartCoroutine(FetchCozeConfig());
//     }

//     private IEnumerator FetchCozeConfig()
//     {
//         using (UnityWebRequest www = UnityWebRequest.Get(backendConfigUrl))
//         {
//             yield return www.SendWebRequest();

//             if (www.result != UnityWebRequest.Result.Success)
//             {
//                 // 后端未启动或网络错误时，直接返回，不修改任何现有配置
//                 Debug.LogWarning($"[CozeConfigFetcher] 获取 Coze 配置失败，将继续使用本地默认配置。url='{backendConfigUrl}', error='{www.error}'", this);
//                 yield break;
//             }

//             string json = www.downloadHandler.text;
//             CozeConfigDto config;

//             try
//             {
//                 config = JsonUtility.FromJson<CozeConfigDto>(json);
//             }
//             catch (Exception e)
//             {
//                 Debug.LogWarning($"CozeConfigFetcher: 解析后端返回 JSON 失败，将继续使用本地默认配置。异常: {e.Message}");
//                 yield break;
//             }

//             if (config == null || string.IsNullOrEmpty(config.cozeApiKey))
//             {
//                 Debug.LogWarning($"[CozeConfigFetcher] 后端返回的 Coze 配置为空，将继续使用本地默认配置。url='{backendConfigUrl}'", this);
//                 yield break;
//             }

//             // 写入 PlayerPrefs（供 SceneChat 中的 CozeAgentClient 启动时读取并覆盖 Inspector 默认值）
//             PlayerPrefs.SetString("CozeApi.ApiKey", config.cozeApiKey);
//             if (!string.IsNullOrEmpty(config.cozeAgentId))
//             {
//                 PlayerPrefs.SetString("CozeApi.AgentId", config.cozeAgentId);
//             }
//             if (!string.IsNullOrEmpty(config.cozeBaseUrl))
//             {
//                 PlayerPrefs.SetString("CozeApi.BaseUrl", config.cozeBaseUrl);
//             }
//             PlayerPrefs.Save();

//             string masked = config.cozeApiKey.Length > 8
//                 ? config.cozeApiKey.Substring(0, 4) + "****" + config.cozeApiKey.Substring(config.cozeApiKey.Length - 4)
//                 : "****";

//             Debug.Log(
//                 $"[CozeConfigFetcher] 已从后端更新 Coze 配置: apiKey='{masked}', agentId='{config.cozeAgentId}', baseUrl='{config.cozeBaseUrl}'",
//                 this
//             );
//         }
//     }
// }
// #endif

