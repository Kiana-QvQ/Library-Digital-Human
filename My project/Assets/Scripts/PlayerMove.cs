// based on the original game.Yen Chezky(yenichw)
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
	public void UpdateSettingsCamera()
	{
		Camera cam = GlobalTag.cameraPlayer;
		if (cam != null)
		{
			cam.fieldOfView = GlobalGame.playerFov;
		}
	}
}
