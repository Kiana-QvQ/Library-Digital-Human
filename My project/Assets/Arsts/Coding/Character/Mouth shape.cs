using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Audio2LipScript : MonoBehaviour
{
    [Tooltip("用于计算视位素（viseme）的唇形同步提供器。")]
    public OVRLipSync.ContextProviders provider = OVRLipSync.ContextProviders.Enhanced;
    
    [Tooltip("在支持的安卓设备上启用DSP卸载加速。")]
    public bool enableAcceleration = true;
    
    [SerializeField] private uint Context = 0;
    
    [Tooltip("调整输入音频的增益值。默认初始值为 0，可调整至 2.5。")]
    [SerializeField] public float gain = 2.5f;

    /// <summary>
    /// 用于处理的音频源，建议与 TTS 使用的 AudioSource 放在同一个物体上。
    /// </summary>
    [SerializeField] private AudioSource m_AudioSource;

    /// <summary>
    /// 角色的蒙皮网格渲染器组件
    /// </summary>
    [Header("角色对应的 MeshRenderer")]
    public SkinnedMeshRenderer meshRenderer;

    /// <summary>
    /// BlendShape 权重的放大系数（0-100，一般保持 100 即可）
    /// </summary>
    public float blendWeightMultiplier = 100f;

    /// <summary>
    /// 元音 viseme（A/I/U/E/O）对应的 BlendShape 索引映射。
    /// </summary>
    [Header("元音 Viseme 与 BlendShape 索引映射")]
    public VisemeBlenderShapeIndexMap m_VisemeIndex;

    [Header("调试")]
    [Tooltip("是否在控制台输出调试日志。默认关闭，调试时可临时开启。")]
    public bool enableDebugLog = false;

    private float _logAudioFilterNextTime;
    private int _logAudioFilterCallCount;
    private int _lastAudioDataLength;
    private int _lastAudioChannels;
    private volatile bool _logInitFailedFlag;
    private float _logVisemeNextTime;

    /// <summary>
    /// 当前帧的唇形数据（由 OVRLipSync 计算）。
    /// </summary>
    private OVRLipSync.Frame frame = new OVRLipSync.Frame();
    protected OVRLipSync.Frame Frame
    {
        get
        {
            return frame;
        }
    }

    private void Awake()
    {
        Debug.Log("=== Audio2LipScript Awake 初始化 ===");
        if (enableDebugLog)
            Debug.Log($"[Audio2Lip] Awake on {gameObject.name}");

        m_AudioSource = this.GetComponent<AudioSource>();
        if (m_AudioSource == null && enableDebugLog)
            Debug.LogWarning("[Audio2Lip] 未找到 AudioSource 组件，请确保 TTS 发声时，将脚本挂载到 AudioSource 所在的同一个物体上。");
        else if (enableDebugLog)
            Debug.Log($"[Audio2Lip] AudioSource 数据源：{m_AudioSource.gameObject.name}");

        if (meshRenderer == null && enableDebugLog)
            Debug.LogWarning("[Audio2Lip] 未在 Inspector 中指定 meshRenderer（SkinnedMeshRenderer），无法更新 BlendShape 形状。");

        if (Context == 0)
        {
            var result = OVRLipSync.CreateContext(ref Context, provider, 0, enableAcceleration);
            if (result != OVRLipSync.Result.Success)
            {
                Debug.LogError($"[Audio2Lip] OVRLipSync.CreateContext 失败：{result}，请检查 OVRLipSync 是否正确初始化。");
                return;
            }
            if (enableDebugLog)
                Debug.Log($"[Audio2Lip] OVRLipSync 上下文创建成功, Context={Context}");
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // 此处避免频繁调用 Time.time / Debug.Log，防止产生过多 GC 影响性能
        if (enableDebugLog && data != null && data.Length > 0)
        {
            _logAudioFilterCallCount++;
            _lastAudioDataLength = data.Length;
            _lastAudioChannels = channels;
        }
        ProcessAudioSamplesRaw(data, channels);
    }

    /// <summary>
    /// 预处理音频：应用增益（与 OVRLipSyncContext 相同），确保唇形同步引擎获得足够的音量。
    /// </summary>
    private void PreprocessAudioSamples(float[] data, int channels)
    {
        if (data == null || gain <= 0f) return;
        for (int i = 0; i < data.Length; i++)
            data[i] = data[i] * gain;
    }

    /// <summary>
    /// 将 F32 PCM 音频缓冲区传递给唇形同步模块
    /// </summary>
    /// <param name="data">音频数据数组</param>
    /// <param name="channels">音频声道数</param>
    public void ProcessAudioSamplesRaw(float[] data, int channels)
    {
        if (data == null || data.Length == 0) return;

        PreprocessAudioSamples(data, channels);

        lock (this)
        {
            if (OVRLipSync.IsInitialized() != OVRLipSync.Result.Success)
            {
                if (enableDebugLog)
                    _logInitFailedFlag = true;
                return;
            }
            if (Context == 0)
                return;
            var frame = this.Frame;
            var result = OVRLipSync.ProcessFrame(Context, data, frame, channels == 2);
            if (enableDebugLog && result != OVRLipSync.Result.Success)
                Debug.LogWarning($"[Audio2Lip] ProcessFrame 调用失败：{result}");
        }
    }

    private void Update()
    {
        if (enableDebugLog)
        {
            if (_logInitFailedFlag)
            {
                _logInitFailedFlag = false;
                Debug.LogWarning("[Audio2Lip] OVRLipSync.IsInitialized() 失败，唇形同步功能未初始化，请确保 OVRLipSync 完全正确初始化。");
            }
            if (_logAudioFilterCallCount > 0)
            {
                float t = Time.time;
                if (t >= _logAudioFilterNextTime)
                {
                    Debug.Log($"[Audio2Lip] 当前处理的音频：每帧 {_lastAudioDataLength} 样本，{_lastAudioChannels} 声道，累计调用={_logAudioFilterCallCount}（来自 AudioSource.OnAudioFilterRead）");
                    _logAudioFilterNextTime = t + 2f;
                }
            }
        }
        if (this.Frame != null)
        {
            SetBlenderShapes();
        }
    }


    private void SetBlenderShapes()
    {
        if (meshRenderer == null)
        {
            if (enableDebugLog)
            {
                float t = Time.time;
                if (t >= _logVisemeNextTime)
                {
                    Debug.LogWarning("[Audio2Lip] SetBlenderShapes 失败：未在 Inspector 中指定 meshRenderer（SkinnedMeshRenderer），无法更新 BlendShape 形状。");
                    _logVisemeNextTime = t + 2f;
                }
            }
            return;
        }
        for (int i = 0; i < this.Frame.Visemes.Length; i++)
        {
            string _name = ((OVRLipSync.Viseme)i).ToString();
            int blendShapeIndex = GetBlenderShapeIndexByName(_name);
            if (blendShapeIndex < 0)
                continue;

            int blendWeight = (int)(blendWeightMultiplier * this.Frame.Visemes[i]);
            meshRenderer.SetBlendShapeWeight(blendShapeIndex, blendWeight);
        }
        if (enableDebugLog)
        {
            float t = Time.time;
            if (t >= _logVisemeNextTime)
            {
                var v = this.Frame.Visemes;
                Debug.Log($"[Audio2Lip] Viseme 数据(部分声道)：v1={v[1]:F2} v2={v[2]:F2} v3={v[3]:F2} v4={v[4]:F2} ... 通道 0 一般为静默声道");
                _logVisemeNextTime = t + 2f;
            }
        }
    }

    /// <summary>
    /// 将 viseme 名称映射到 a / i / u / e / o 对应的 BlendShape 索引
    /// </summary>
    /// <param name="_name">viseme 名称</param>
    /// <returns>对应的 BlendShape 索引</returns>
    private int GetBlenderShapeIndexByName(string _name)
    {
        // 静音不对应任何嘴型
        if (_name == "sil")
        {
            return -1;
        }
        if (_name == "aa")
        {
            return m_VisemeIndex.useA ? m_VisemeIndex.A : -1;
        }
        if (_name == "ih")
        {
            return m_VisemeIndex.useI ? m_VisemeIndex.I : -1;
        }
        if (_name == "E")
        {
            return m_VisemeIndex.useE ? m_VisemeIndex.E : -1;
        }
        if (_name == "oh")
        {
            return m_VisemeIndex.useO ? m_VisemeIndex.O : -1;
        }

        // 其它音素默认映射到 U，可通过 useU 控制是否使用
        return m_VisemeIndex.useU ? m_VisemeIndex.U : -1;
    }

    [System.Serializable]
    public class VisemeBlenderShapeIndexMap
    {
        [Header("A 元音")]
        public bool useA = true;
        public int A;

        [Header("I 元音")]
        public bool useI = true;
        public int I;

        [Header("U 元音")]
        public bool useU = true;
        public int U;

        [Header("E 元音")]
        public bool useE = true;
        public int E;

        [Header("O 元音")]
        public bool useO = true;
        public int O;
    }
}