using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class STT : MonoBehaviour
{

    /// <summary>
    /// 语音识别API地址
    /// </summary>
    [SerializeField] protected string m_SpeechRecognizeURL = String.Empty;
    /// <summary>
    /// 计算方法运行的时间
    /// </summary>
    [SerializeField] protected Stopwatch stopwatch = new Stopwatch();
    /// <summary>
    /// 语音识别（通过AudioClip）
    /// </summary>
    /// <param name="_clip">音频片段</param>
    /// <param name="_callback">识别结果回调</param>
    public virtual void SpeechToText(AudioClip _clip,Action<string> _callback)
    {
       
    }

    /// <summary>
    /// 语音识别（通过字节数组）
    /// </summary>
    /// <param name="_audioData">音频数据</param>
    /// <param name="_callback">识别结果回调</param>
    public virtual void SpeechToText(byte[] _audioData, Action<string> _callback)
    {

    }


}
