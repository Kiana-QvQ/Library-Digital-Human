using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Wake-on-voice 语音唤醒 base类
/// </summary>
public class WOV : MonoBehaviour
{

    /// <summary>
    /// 关键词回调
    /// </summary>
    protected Action<string> OnKeywordRecognizer;
    /// <summary>
    /// 绑定唤醒回调
    /// </summary>
    /// <param name=""></param>
    /// <param name="_callback"></param>
    public virtual void OnBindAwakeCallBack(Action<string> _callback)
    {
        OnKeywordRecognizer += _callback;
    }
    /// <summary>
    /// 开始识别
    /// </summary>
    public virtual void StartRecognizer()
    {

    }
    /// <summary>
    /// 结束识别
    /// </summary>
    public virtual void StopRecognizer()
    {

    }
    /// <summary>
    /// 唤醒词回调
    /// </summary>
    /// <param name="_msg"></param>
    protected virtual void OnAwakeOnVoice(string _msg)
    {
        if (OnKeywordRecognizer == null)
            return;

        OnKeywordRecognizer(_msg);
    }




}
