using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Baidu speech API credentials and OAuth token management.
/// When Qt saves keys to app_config.json, BackendConnectionMonitor applies them at runtime.
/// If Qt has no keys, Unity uses Inspector / scene defaults.
/// </summary>
public class BaiduSettings : MonoBehaviour
{
    [Header("Baidu AI credentials")]
    [Tooltip("API Key from Baidu console")]
    public string m_API_key;

    [Tooltip("Secret Key from Baidu console")]
    public string m_Client_secret;

    [Header("Token")]
    public bool m_GetTokenFromServer = true;
    public string m_Token;
    public string m_AuthorizeURL = "https://aip.baidubce.com/oauth/2.0/token";

    private bool _tokenRefreshing;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(m_API_key) && !string.IsNullOrWhiteSpace(m_Client_secret);

    public void ApplyRuntimeCredentials(string apiKey, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            return;
        }

        m_API_key = apiKey.Trim();
        m_Client_secret = secretKey.Trim();
        m_Token = null;
    }

    public static void ApplyRuntimeCredentialsToAll(string apiKey, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            return;
        }

        BaiduSettings[] all = FindObjectsOfType<BaiduSettings>(true);
        foreach (BaiduSettings settings in all)
        {
            if (settings != null)
            {
                settings.ApplyRuntimeCredentials(apiKey, secretKey);
            }
        }

        Debug.Log($"[BaiduSettings] Applied runtime credentials to {all.Length} component(s)");
    }

    public IEnumerator RefreshToken()
    {
        if (!m_GetTokenFromServer)
        {
            yield break;
        }

        if (!HasCredentials)
        {
            Debug.LogError("[BaiduSettings] API Key or Secret Key is not configured");
            yield break;
        }

        if (!string.IsNullOrEmpty(m_Token))
        {
            yield break;
        }

        if (_tokenRefreshing)
        {
            while (_tokenRefreshing)
            {
                yield return null;
            }
            yield break;
        }

        _tokenRefreshing = true;

        string baseUrl = string.IsNullOrWhiteSpace(m_AuthorizeURL)
            ? "https://aip.baidubce.com/oauth/2.0/token"
            : m_AuthorizeURL.Trim();
        string url =
            $"{baseUrl}?grant_type=client_credentials&client_id={UnityWebRequest.EscapeURL(m_API_key)}&client_secret={UnityWebRequest.EscapeURL(m_Client_secret)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[BaiduSettings] Token request failed: {req.error} {req.downloadHandler?.text}");
                _tokenRefreshing = false;
                yield break;
            }

            try
            {
                JObject response = JObject.Parse(req.downloadHandler.text);
                string token = response["access_token"]?.ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    m_Token = token;
                }
                else
                {
                    string err = response["error_description"]?.ToString()
                        ?? response["error"]?.ToString()
                        ?? req.downloadHandler.text;
                    Debug.LogError($"[BaiduSettings] Invalid token response: {err}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BaiduSettings] Failed to parse token: {ex.Message}");
            }
        }

        _tokenRefreshing = false;
    }
}
