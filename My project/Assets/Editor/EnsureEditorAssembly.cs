#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// 确保 Editor 程序集正常生成，缓解 Burst 找不到 Assembly-CSharp-Editor 的问题。
/// </summary>
[InitializeOnLoad]
internal static class EnsureEditorAssembly
{
	static EnsureEditorAssembly()
	{
		CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
		EditorApplication.delayCall += TouchRuntimeAssembly;
	}

	private static void TouchRuntimeAssembly()
	{
		_ = typeof(GlobalGame);
		_ = typeof(VoiceControlManager);
	}

	private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
	{
		if (!assemblyPath.Contains("Assembly-CSharp-Editor"))
		{
			return;
		}

		foreach (CompilerMessage message in messages)
		{
			if (message.type == CompilerMessageType.Error)
			{
				Debug.LogError($"[EnsureEditorAssembly] {message.message}");
			}
		}
	}
}
#endif
