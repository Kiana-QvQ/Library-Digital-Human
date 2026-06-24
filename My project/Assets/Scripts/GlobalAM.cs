// based on the original game.Yen Chezky(yenichw)
using System.IO;
using UnityEngine;

public static class GlobalAM
{
	private static string PathFor(string key)
	{
		string safe = key.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
		return Path.Combine(Application.persistentDataPath, safe + ".sav");
	}

	public static bool ExistsData(string key)
	{
		return File.Exists(PathFor(key));
	}

	public static void SaveData(string key, string data)
	{
		File.WriteAllText(PathFor(key), data ?? string.Empty);
	}

	public static void DeleteData(string key)
	{
		string path = PathFor(key);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public static string[] LoadData(string key)
	{
		string path = PathFor(key);
		if (!File.Exists(path))
		{
			return new string[0];
		}

		return File.ReadAllLines(path);
	}

	public static int StringToInt(string value)
	{
		return int.TryParse(value, out int result) ? result : 0;
	}

	public static Vector3 NormalizeFloor(Vector3 vector)
	{
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.0001f)
		{
			return Vector3.forward;
		}

		return vector.normalized;
	}

	public static Vector3 TransformPivot(Transform pivot, Vector3 localPosition)
	{
		if (pivot == null)
		{
			return localPosition;
		}

		return pivot.TransformPoint(localPosition);
	}

	public static Vector3 Vector3Random(float min, float max)
	{
		return new Vector3(
			Random.Range(min, max),
			Random.Range(min, max),
			Random.Range(min, max)
		);
	}
}
