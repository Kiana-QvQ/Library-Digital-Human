using UnityEngine;

/// <summary>
/// 根据当前激活的人物模型，将语音播放 AudioSource 和嘴型脚本绑定到对应模型。
/// 场景：SceneChat
/// - 人物父物体使用 SceneChatApplyMenuChoices.characterModelContainer（例如 Mita）
/// - 子物体：不同人物模型（例如 MitaPerson Mita、Shenh）
/// 使用方式：
/// 1. 将本脚本挂在包含 VoiceControlManager 的物体上（SceneChat 里的 MenuGame）。
/// 2. 在 Inspector 中拖入：
///    - characterModelContainer：人物父物体 Transform
///    - voiceControlManager：同物体上的 VoiceControlManager 组件
///    - cozeClientOrAgent：场景中负责 Coze 对话的组件（CozeClient 或 CozeAgentClient）
/// 3. 在 SceneChatApplyMenuChoices 中调用 OnCharacterModelChanged(index) 完成绑定。
/// </summary>
public class CharacterTTSBinder : MonoBehaviour
{
    [Header("人物模型容器（与 SceneChatApplyMenuChoices 的 characterModelContainer 一致）")]
    public Transform characterModelContainer;

    [Header("语音控制管理器（VoiceControlManager）")]
    public VoiceControlManager voiceControlManager;

    [Header("Coze 客户端（含 outputAudioSource 字段的脚本）")]
    [Tooltip("如果项目使用 CozeClient，就拖 CozeClient；若使用 CozeAgentClient，就拖 CozeAgentClient。")]
    public MonoBehaviour cozeClientOrAgent;

    /// <summary>
    /// 根据人物索引绑定当前模型的音源和嘴型。
    /// index：characterModelContainer 下子物体索引（与 PlayerPrefs.CharacterModelIndex 一致）。
    /// </summary>
    public void OnCharacterModelChanged(int index)
    {
        if (characterModelContainer == null)
        {
            Debug.LogWarning("[CharacterTTSBinder] characterModelContainer 未设置");
            OperationLogger.LogSystem("[CharacterTTSBinder] characterModelContainer 未设置");
            return;
        }

        int childCount = characterModelContainer.childCount;
        if (childCount == 0)
        {
            Debug.LogWarning("[CharacterTTSBinder] characterModelContainer 下没有子物体");
            OperationLogger.LogSystem("[CharacterTTSBinder] characterModelContainer 下没有子物体");
            return;
        }

        index = Mathf.Clamp(index, 0, childCount - 1);
        Transform activeModel = characterModelContainer.GetChild(index);

        // 1. 获取当前模型上的 AudioSource
        AudioSource modelAudio = activeModel.GetComponent<AudioSource>();
        if (modelAudio == null)
        {
            Debug.LogWarning($"[CharacterTTSBinder] 模型 {activeModel.name} 上没有 AudioSource，无法输出 TTS 音频");
            OperationLogger.LogSystem($"[CharacterTTSBinder] 模型 {activeModel.name} 缺少 AudioSource，绑定失败（index={index}）");
            return;
        }

        // 2. 将 VoiceControlManager 的 audioSource 指向当前模型
        if (voiceControlManager != null)
        {
            voiceControlManager.audioSource = modelAudio;
        }
        else
        {
            Debug.LogWarning("[CharacterTTSBinder] voiceControlManager 未设置，无法同步 audioSource");
            OperationLogger.LogSystem("[CharacterTTSBinder] voiceControlManager 未设置，无法同步 audioSource");
        }

        // 3. 将 CozeClient / CozeAgentClient 的 outputAudioSource 指向当前模型
        if (cozeClientOrAgent != null)
        {
            bool cozeBound = false;
            // 通过反射统一兼容 CozeClient 和 CozeAgentClient
            var type = cozeClientOrAgent.GetType();
            var field = type.GetField("outputAudioSource");
            if (field != null && field.FieldType == typeof(AudioSource))
            {
                field.SetValue(cozeClientOrAgent, modelAudio);
                cozeBound = true;
            }
            else
            {
                var prop = type.GetProperty("outputAudioSource");
                if (prop != null && prop.PropertyType == typeof(AudioSource) && prop.CanWrite)
                {
                    prop.SetValue(cozeClientOrAgent, modelAudio, null);
                    cozeBound = true;
                }
                else
                {
                    Debug.LogWarning($"[CharacterTTSBinder] {type.Name} 上未找到 outputAudioSource 字段/属性，无法绑定 Coze 音源");
                    OperationLogger.LogSystem($"[CharacterTTSBinder] {type.Name} 未找到 outputAudioSource 字段/属性，无法绑定 Coze 音源");
                }
            }

            if (cozeBound)
            {
                OperationLogger.LogSystem($"[CharacterTTSBinder] 已绑定 {type.Name}.outputAudioSource -> {activeModel.name}/{modelAudio.GetType().Name}");
            }
        }
        else
        {
            Debug.LogWarning("[CharacterTTSBinder] cozeClientOrAgent 未设置，跳过 Coze 音源绑定");
            OperationLogger.LogSystem("[CharacterTTSBinder] cozeClientOrAgent 未设置，跳过 Coze 音源绑定");
        }

        // 4. 处理嘴型脚本：仅启用当前模型上的，关闭其它模型上的
        int enabledLipCount = 0;
        int enabledSimpleMouthCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = characterModelContainer.GetChild(i);

            // OVR LipSync 方案
            Audio2LipScript lip = child.GetComponent<Audio2LipScript>();
            if (lip != null)
            {
                lip.enabled = (i == index);
                if (lip.enabled) enabledLipCount++;
            }

            // 简单音量驱动方案
            AudioMouthController simpleMouth = child.GetComponent<AudioMouthController>();
            if (simpleMouth != null)
            {
                simpleMouth.enabled = (i == index);
                if (simpleMouth.enabled) enabledSimpleMouthCount++;
            }
        }

        string msg =
            $"[CharacterTTSBinder] 已将 TTS 输出与嘴型绑定到模型：{activeModel.name} (index={index}) " +
            $"AudioSource={(modelAudio != null ? modelAudio.name : "null")} " +
            $"LipSyncEnabled={enabledLipCount} SimpleMouthEnabled={enabledSimpleMouthCount}";
        Debug.Log(msg, this);
        OperationLogger.LogSystem(msg);
    }

    /// <summary>
    /// 场景进入时，根据 PlayerPrefs 自动绑定一次当前模型。
    /// </summary>
    private void Start()
    {
        int useDefault = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1);
        int index = (useDefault == 1)
            ? 0
            : PlayerPrefs.GetInt("CharacterModelIndex", 0);

        OperationLogger.LogSystem($"[CharacterTTSBinder] Start auto-bind: UseDefaultBackgroundAndModel={useDefault}, CharacterModelIndex={index}");
        OnCharacterModelChanged(index);
    }
}

