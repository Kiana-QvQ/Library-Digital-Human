// based on the original game.Yen Chezky(yenichw)
using System.Collections.Generic;
using UnityEngine;

public class GlobalLanguage : MonoBehaviour
{
	public static List<LanguageFilesText> languageText;

	public static string GetString(string _name, int _string)
	{
		string result = null;
		if (languageText == null) return null;
		for (int i = 0; i < languageText.Count; i++)
		{
			if (languageText[i].name == _name && languageText[i].strings != null && _string >= 0 && _string < languageText[i].strings.Length)
			{
				result = languageText[i].strings[_string];
				break;
			}
		}
		return result;
	}
}
