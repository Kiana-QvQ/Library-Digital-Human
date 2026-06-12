using System;
using UnityEngine;

/// <summary>
/// TTS（文本转语音）提供者接口
/// 用于统一不同TTS提供者的接口，便于管理和切换
/// </summary>
public interface ITTSProvider
{
    /// <summary>
    /// 提供者名称
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// 是否可用
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// 文本转语音
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="callback">合成完成回调，返回音频片段和错误信息</param>
    void Speak(string text, Action<AudioClip, string> callback);
}

