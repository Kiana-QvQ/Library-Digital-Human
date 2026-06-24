// based on the original game.Yen Chezky(yenichw)
using UnityEngine;

public class Achievement_function : MonoBehaviour
{
	public void AchievementComplete(int x)
	{
		if (GlobalTag.gameOptions == null)
		{
			return;
		}

		GlobalTag.gameOptions.GetComponent<AchievementsController>().AchievementCompleted(x);
	}
}
