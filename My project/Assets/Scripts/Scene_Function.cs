// based on the original game.Yen Chezky(yenichw)
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景跳转（开场 Timeline 结束时调用 NextScene）。
/// </summary>
public class Scene_Function : MonoBehaviour
{
	public void NextScene(string sceneName)
	{
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			sceneName = "SceneLoading";
		}

		Debug.Log($"[Scene_Function] NextScene -> {sceneName}", this);

		// 开场动画结束后进入加载页，再由 SceneLoading 默认进 SceneMenu
		if (sceneName == "SceneLoading")
		{
			GlobalGame.LoadingLevel = null;
			SceneManager.LoadScene("SceneLoading", LoadSceneMode.Single);
			return;
		}

		BlackScreen blackScreen = FindObjectOfType<BlackScreen>(true);
		if (blackScreen != null)
		{
			blackScreen.NextLevel(sceneName);
			return;
		}

		GlobalGame.LoadingLevel = sceneName;
		SceneManager.LoadScene("SceneLoading", LoadSceneMode.Single);
	}
}
