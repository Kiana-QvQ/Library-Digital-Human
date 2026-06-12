// based on the original game.Yen Chezky(yenichw)
using UnityEngine;

public class MenuLocation : MonoBehaviour
{
	private bool active;

	private float countCases;

	private float timeDisable;

	public RectTransform[] objects;

	private void Start()
	{
		if (objects == null)
		{
			return;
		}
		for (int i = 0; i < objects.Length; i++)
		{
			if (objects[i] != null)
			{
				objects[i].anchoredPosition = new Vector2(-800f, objects[i].anchoredPosition.y);
			}
		}
	}

	private void Update()
	{
		if (objects == null || objects.Length == 0)
		{
			return;
		}
		if (active)
		{
			for (int i = 0; i < Mathf.FloorToInt(countCases); i++)
			{
				if (objects[i] != null)
				{
					objects[i].anchoredPosition = Vector2.Lerp(objects[i].anchoredPosition, new Vector2(0f, objects[i].anchoredPosition.y), Time.unscaledDeltaTime * 15f);
				}
			}
		}
		else
		{
			for (int j = 0; j < Mathf.FloorToInt(countCases); j++)
			{
				if (objects[j] != null)
				{
					objects[j].anchoredPosition = Vector2.Lerp(objects[j].anchoredPosition, new Vector2(300f, objects[j].anchoredPosition.y), Time.unscaledDeltaTime * 15f);
					objects[j].GetComponent<UI_Colors>().Hide(x: true, _fast: false);
				}
			}
			if (timeDisable > 0f)
			{
				timeDisable -= Time.deltaTime;
				if (timeDisable <= 0f)
				{
					base.gameObject.SetActive(value: false);
				}
			}
		}
		if (countCases < (float)objects.Length)
		{
			countCases += Time.unscaledDeltaTime * 40f;
			if (countCases > (float)objects.Length)
			{
				countCases = objects.Length;
				timeDisable = 1f;
			}
		}
	}

	public void Active(bool x)
	{
		if (objects == null)
		{
			return;
		}
		active = x;
		countCases = 0f;
		if (x)
		{
			base.gameObject.SetActive(value: true);
			for (int i = 0; i < objects.Length; i++)
			{
				RectTransform rt = objects[i];
				if (rt != null)
				{
					rt.anchoredPosition = new Vector2(-800f, rt.anchoredPosition.y);

					UI_Colors uiColors = rt.GetComponent<UI_Colors>();
					if (uiColors != null)
					{
						if (!uiColors.onEnableInvisible)
						{
							uiColors.Hide(x: false, _fast: true);
						}
						else
						{
							uiColors.Hide(x: false, _fast: false);
						}
					}

					ButtonMouseClick button = rt.GetComponent<ButtonMouseClick>();
					if (button != null)
					{
						button.LockClick(x: false);
					}
				}
			}
			return;
		}
		for (int j = 0; j < objects.Length; j++)
		{
			RectTransform rt = objects[j];
			if (rt != null)
			{
				ButtonMouseClick button = rt.GetComponent<ButtonMouseClick>();
				if (button != null)
				{
					button.LockClick(x: true);
				}
			}
		}
	}
}
