using UnityEngine;

/// <summary>
/// 测试Newtonsoft.Json是否可用
/// 如果这个脚本可以编译并运行，说明Newtonsoft.Json配置正确
/// </summary>
public class TestNewtonsoftJson : MonoBehaviour
{
    void Start()
    {
        // 尝试使用Newtonsoft.Json
        try
        {
            #if UNITY_EDITOR
            // 在Editor中测试
            var testObj = new { name = "test", value = 123 };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(testObj);
            Debug.Log($"✅ Newtonsoft.Json可用！测试JSON: {json}");
            #else
            Debug.Log("⚠️ 此测试脚本仅在Editor模式下运行");
            #endif
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Newtonsoft.Json不可用！错误: {ex.Message}");
            Debug.LogError($"请检查Newtonsoft.Json.dll是否正确配置，或通过Package Manager安装com.unity.nuget.newtonsoft-json");
        }
    }
}

