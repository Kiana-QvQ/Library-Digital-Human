using System;
using UnityEngine;

/// <summary>
/// STT（语音转文本）提供者接口
/// 用于统一不同STT提供者的接口，便于管理和切换
/// </summary>
public interface ISTTProvider
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
    /// 语音转文本（通过AudioClip）
    /// </summary>
    /// <param name="clip">音频片段</param>
    /// <param name="callback">识别结果回调</param>
    void SpeechToText(AudioClip clip, Action<string> callback);
    
    /// <summary>
    /// 语音转文本（通过字节数组）
    /// </summary>
    /// <param name="audioData">音频数据</param>
    /// <param name="callback">识别结果回调</param>
    void SpeechToText(byte[] audioData, Action<string> callback);
}

