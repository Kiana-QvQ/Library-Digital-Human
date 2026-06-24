#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 保证 Editor 程序集存在，避免 Burst 在找不到 Assembly-CSharp-Editor 时报错。
/// </summary>
[InitializeOnLoad]
internal static class ProjectEditorStub
{
	static ProjectEditorStub()
	{
	}
}
#endif
