// based on the original game.Yen Chezky(yenichw)
using System.IO;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Functions/Localization/Localization UI Text")]
public class Localization_UIText : MonoBehaviour
{
	public bool EveryEnable;

	public string NameFile;

	public int StringNumber = 1;

	public bool GrandSymbol = true;

	public bool data;

	public bool dontAutoTranslate;

	public bool dontAutoFont;

	private bool fs;

	public void OnEnable()
	{
		if (!dontAutoTranslate && fs && EveryEnable)
		{
			TextTranslate();
		}
	}

	private void Start()
	{
		if (!dontAutoTranslate)
		{
			fs = true;
			TextTranslate();
		}
	}

	public void TextTranslate()
	{
		fs = true;
		Text textComponent = GetComponent<Text>();
		if (textComponent == null) return;
		if (GlobalGame.fontUse != null && !dontAutoFont)
		{
			textComponent.font = GlobalGame.fontUse;
		}
		string text = null;
		int index = StringNumber - 1;
		if (index < 0) index = 0;
		if (!data)
		{
			if (GlobalLanguage.languageText != null)
			{
				for (int i = 0; i < GlobalLanguage.languageText.Count; i++)
				{
					if (GlobalLanguage.languageText[i].name == NameFile && GlobalLanguage.languageText[i].strings != null && index < GlobalLanguage.languageText[i].strings.Length)
					{
						text = GlobalLanguage.languageText[i].strings[index];
						break;
					}
				}
			}
		}
		else if (!string.IsNullOrEmpty(NameFile))
		{
			string path = Path.Combine(Application.streamingAssetsPath, "Data", NameFile + ".txt");
			if (File.Exists(path))
			{
				string[] lines = File.ReadAllLines(path);
				if (lines != null && index < lines.Length)
					text = lines[index];
			}
		}
		if (text != null)
		{
			if (GrandSymbol)
			{
				text = text.ToUpper();
			}
			textComponent.text = text;
		}
		else
		{
			textComponent.text = "???";
		}
	}

	public void ReString(int x)
	{
		StringNumber = x;
	}

	public void ReFile(string x)
	{
		NameFile = x;
	}

	public void DestroyObject()
	{
		Object.Destroy(base.gameObject);
	}
}
