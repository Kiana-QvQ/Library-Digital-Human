using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 统一的操作日志记录器，生成 exe 后仍会写入本地文件
/// </summary>
public static class OperationLogger
{
    private static readonly string logFilePath;
    private const string LogFileName = "操作日志.log";

    static OperationLogger()
    {
        string directory = Application.persistentDataPath;
        if (string.IsNullOrEmpty(directory))
        {
            directory = Application.dataPath;
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        logFilePath = Path.Combine(directory, LogFileName);
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="category">日志类别（USER/SYSTEM/TTS等）</param>
    /// <param name="message">具体内容</param>
    public static void Log(string category, string message)
    {
        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}";
        try
        {
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogError($"写入操作日志失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 便捷方法：记录用户操作
    /// </summary>
    public static void LogUser(string message) => Log("USER", message);

    /// <summary>
    /// 便捷方法：记录程序反馈
    /// </summary>
    public static void LogSystem(string message) => Log("SYSTEM", message);

    /// <summary>
    /// 便捷方法：记录语音播报
    /// </summary>
    public static void LogTTS(string message) => Log("TTS", message);
}

