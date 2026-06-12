using UnityEngine;

/// <summary>
/// TTS脚本诊断工具
/// 用于检查TTS相关脚本是否可以正常编译和挂载
/// </summary>
public class TTSScriptDiagnostic : MonoBehaviour
{
    [ContextMenu("诊断TTS脚本")]
    public void DiagnoseTTSScripts()
    {
        Debug.Log("=== TTS脚本诊断开始 ===");
        
        // 检查TTS基类
        bool ttsBaseExists = typeof(TTS) != null;
        Debug.Log($"TTS基类: {(ttsBaseExists ? "✅ 存在" : "❌ 不存在")}");
        
        // 检查Coze相关类
        try
        {
            var cozeTTS = typeof(CozeTextToSpeech);
            Debug.Log($"CozeTextToSpeech: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"CozeTextToSpeech: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var cozeStream = typeof(CozeStreamTTS);
            Debug.Log($"CozeStreamTTS: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"CozeStreamTTS: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var cozeSettings = typeof(CozeSettings);
            Debug.Log($"CozeSettings: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"CozeSettings: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var apiManager = typeof(APIManager);
            Debug.Log($"APIManager: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"APIManager: ❌ 编译失败 - {ex.Message}");
        }
        
        // 检查Doubao相关类
        try
        {
            var doubaoTTS = typeof(DoubaoTextToSpeech);
            Debug.Log($"DoubaoTextToSpeech: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DoubaoTextToSpeech: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var doubaoStream = typeof(DoubaoStreamTTS);
            Debug.Log($"DoubaoStreamTTS: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DoubaoStreamTTS: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var doubaoStreamV3 = typeof(DoubaoStreamTTSV3);
            Debug.Log($"DoubaoStreamTTSV3: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DoubaoStreamTTSV3: ❌ 编译失败 - {ex.Message}");
        }
        
        try
        {
            var doubaoSettings = typeof(DoubaoSettings);
            Debug.Log($"DoubaoSettings: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DoubaoSettings: ❌ 编译失败 - {ex.Message}");
        }
        
        // 检查Newtonsoft.Json
        try
        {
            var jsonConvert = typeof(Newtonsoft.Json.JsonConvert);
            Debug.Log($"Newtonsoft.Json: ✅ 可用");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Newtonsoft.Json: ❌ 不可用 - {ex.Message}");
        }
        
        // 检查TTSManager
        try
        {
            var ttsManager = typeof(TTSManager);
            Debug.Log($"TTSManager: ✅ 可以编译");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"TTSManager: ❌ 编译失败 - {ex.Message}");
        }
        
        Debug.Log("=== TTS脚本诊断完成 ===");
        Debug.Log("如果看到❌，说明对应的类无法编译，请检查Console中的编译错误");
    }
    
    void Start()
    {
        // 自动运行诊断
        DiagnoseTTSScripts();
    }
}

