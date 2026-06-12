using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 与 Unity UI Dropdown 配合：选项列表可配置，选中项会保存到 PlayerPrefs，启动时自动回填。
/// 用于“对话模型选择”“语言模型选择”等滚动选择。
/// </summary>
public class UI_DropdownSave : MonoBehaviour
{
    [Header("PlayerPrefs Key（存选中项的索引或 value 字符串）")]
    public string prefsKey = "DialogueModel";

    [Header("保存方式")]
    public bool saveAsIndex = true;

    [Header("选项列表（显示名）")]
    public List<string> options = new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-3.5-turbo" };

    [Header("默认选中索引（0 起）")]
    public int defaultIndex = 0;

    private Dropdown _dropdown;

    private void Awake()
    {
        _dropdown = GetComponent<Dropdown>();
    }

    private void Start()
    {
        if (_dropdown == null) return;

        _dropdown.ClearOptions();
        _dropdown.AddOptions(options);
        _dropdown.RefreshShownValue();

		if (saveAsIndex)
        {
            int saved = PlayerPrefs.GetInt(prefsKey, defaultIndex);
            saved = Mathf.Clamp(saved, 0, options.Count - 1);
            _dropdown.value = saved;
            _dropdown.RefreshShownValue();
			Debug.Log($"[API Settings] Dropdown '{prefsKey}' 加载索引 = {saved}, 文本 = {GetSelectedLabel()}", this);
        }
        else
        {
            string saved = PlayerPrefs.GetString(prefsKey, options.Count > defaultIndex ? options[defaultIndex] : "");
            int idx = options.IndexOf(saved);
            if (idx < 0) idx = defaultIndex;
            _dropdown.value = idx;
            _dropdown.RefreshShownValue();
			Debug.Log($"[API Settings] Dropdown '{prefsKey}' 加载文本 = '{saved}', 实际索引 = {idx}", this);
        }

        _dropdown.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnValueChanged(int index)
    {
		if (saveAsIndex)
		{
			PlayerPrefs.SetInt(prefsKey, index);
			Debug.Log($"[API Settings] Dropdown '{prefsKey}' 保存索引 = {index}", this);
		}
		else
		{
			string label = options.Count > index ? options[index] : "";
			PlayerPrefs.SetString(prefsKey, label);
			Debug.Log($"[API Settings] Dropdown '{prefsKey}' 保存文本 = '{label}', 索引 = {index}", this);
		}
        PlayerPrefs.Save();
    }

    /// <summary> 当前选中的显示文本 </summary>
    public string GetSelectedLabel()
    {
        if (_dropdown == null || options == null || _dropdown.value < 0 || _dropdown.value >= options.Count)
            return "";
        return options[_dropdown.value];
    }

    /// <summary> 当前选中的索引 </summary>
    public int GetSelectedIndex()
    {
        return _dropdown != null ? _dropdown.value : 0;
    }
}
