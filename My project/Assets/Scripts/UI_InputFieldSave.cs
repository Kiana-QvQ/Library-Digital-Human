using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把 UGUI InputField 的内容保存到 PlayerPrefs，并在启动时自动回填。
/// 用法：挂到 InputField 同物体上，设置 prefsKey，然后把 InputField 的 OnEndEdit 绑定到 SaveFromString。
/// </summary>
public class UI_InputFieldSave : MonoBehaviour
{
    [Header("PlayerPrefs Key")]
    public string prefsKey = "UserInput";

    [Header("默认值（首次没有存档时使用）")]
    public string defaultValue = "";

    private InputField _input;

    private void Awake()
    {
        _input = GetComponent<InputField>();
    }

    private void Start()
    {
        if (_input == null)
        {
            return;
        }

        _input.text = PlayerPrefs.GetString(prefsKey, defaultValue);
    }

    // 给 UnityEvent 绑定：InputField.OnEndEdit(string)
    public void SaveFromString(string value)
    {
		string v = value ?? "";
		PlayerPrefs.SetString(prefsKey, v);
		PlayerPrefs.Save();
		Debug.Log($"[API Settings] InputField '{prefsKey}' 保存内容 = '{v}'", this);
    }

    // 如果你更喜欢点“确认按钮”保存，可让按钮调用这个无参方法
    public void SaveCurrent()
    {
        if (_input == null)
        {
            return;
        }

        SaveFromString(_input.text);
    }
}

