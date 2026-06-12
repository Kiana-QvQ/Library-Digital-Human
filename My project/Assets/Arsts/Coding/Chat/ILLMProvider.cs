using System;

/// <summary>
/// LLM（大语言模型）提供者接口
/// 用于统一不同LLM提供者的接口，便于管理和切换
/// </summary>
public interface ILLMProvider
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
    /// 发送消息
    /// </summary>
    /// <param name="message">要发送的消息</param>
    /// <param name="callback">回复回调</param>
    void PostMessage(string message, Action<string> callback);
}

