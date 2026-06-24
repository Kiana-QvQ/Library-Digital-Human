// based on the original game.Yen Chezky(yenichw)
using UnityEngine;

public static class ConsoleMain
{
	public static bool active;

	public static bool dev = true;

	public static void ConsolePrintGame(string message)
	{
		if (dev)
		{
			Debug.Log("[Console] " + message);
		}
	}
}
