// based on the original game.Yen Chezky(yenichw)
// 菜单按钮：处理鼠标/手柄悬停、点击，并转发给同物体上的 MenuCaseOption / MenuNextLocation；Inspector 中可配置 Event Click 等。
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class ButtonMouseClick : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	[Header("菜单与交互")]
	[Tooltip("关联的菜单（如 MenuGame），用于焦点、音效等")]
	public ButtonMouseMenu menuButton;

	[Tooltip("是否可交互；为 false 时按钮灰显且不响应点击")]
	public bool interactable = true;

	[Tooltip("用于高亮/阴影效果的子物体（需带 UIShadow、UIShiny 等）")]
	public GameObject[] elements;

	[Tooltip("为 true 时点击播放切换类音效（ClickButtonToggle），否则普通点击音效（ClickButton）")]
	public bool toggleSound;

	[Tooltip("为 true 时点击不播放音效")]
	public bool offSound;

	[Header("事件（可在 Inspector 中绑定，如加载场景等）")]
	[Tooltip("点击时触发，先于 MenuCaseOption.Click() 执行")]
	public UnityEvent eventClick;

	[Tooltip("鼠标/焦点进入时触发")]
	public UnityEvent eventEnter;

	[Tooltip("鼠标/焦点离开时触发")]
	public UnityEvent eventExit;

	[Tooltip("鼠标抬起时触发")]
	public UnityEvent eventUp;

	[Header("切换/导航")]
	[Tooltip("上/下/左/右方向键时切换到的相邻按钮")]
	public ButtonMouseClick changeUp;

	public ButtonMouseClick changeDown;

	public ButtonMouseClick changeLeft;

	public ButtonMouseClick changeRight;

	[Header("颜色")]
	[Tooltip("悬停/获得焦点时的 tint 颜色")]
	public Color colorEnter = new Color(0.8f, 0f, 1f, 0.3f);

	[Tooltip("未悬停时的 tint 颜色")]
	public Color colorExit = new Color(0.8f, 0f, 1f, 0f);

	private bool stopKey;

	private bool firstStart;

	[HideInInspector]
	public bool changeNow;

	private float timeAnimation;

	private bool lockButton;

	private RectTransform change;

	private bool inputPosition;

	private int timeStart;

	public void Start()
	{
		if (firstStart)
		{
			return;
		}
		if (!interactable)
		{
			ActivationInteractive(x: false);
		}
		firstStart = true;
		timeAnimation = 1f;
		for (int i = 0; i < elements.Length; i++)
		{
			elements[i].GetComponent<UIShadow>().style = ShadowStyle.Shadow;
			elements[i].GetComponent<UIShadow>().effectColor = new Color(0f, 0f, 0f, 0f);
			elements[i].GetComponent<UIShadow>().effectDistance = new Vector2(3f, -3f);
			elements[i].GetComponent<UIShiny>().effectFactor = 0f;
		}
		if (GetComponent<UI_Colors>() != null)
		{
			GetComponent<UI_Colors>().SetColorImage(0, colorExit);
		}
		if (base.transform.parent.Find("Change") != null)
		{
			change = base.transform.parent.Find("Change").GetComponent<RectTransform>();
		}
		if (GetComponent<UI_Colors>() != null)
		{
			if (!interactable)
			{
				GetComponent<UI_Colors>().ShareAlpha(4f);
			}
			else
			{
				GetComponent<UI_Colors>().ShareAlpha(1f);
			}
		}
	}

	private void OnDisable()
	{
		PointerExit();
	}

	private void OnEnable()
	{
		timeStart = 2;
	}

	private void Update()
	{
		if (timeAnimation < 1f)
		{
			timeAnimation += Time.unscaledDeltaTime * 3f;
			if (timeAnimation > 1f)
			{
				timeAnimation = 1f;
			}
			for (int i = 0; i < elements.Length; i++)
			{
				if (changeNow)
				{
					elements[i].GetComponent<UIShadow>().effectColor = new Color(elements[i].GetComponent<UIShadow>().effectColor.r, elements[i].GetComponent<UIShadow>().effectColor.g, elements[i].GetComponent<UIShadow>().effectColor.b, timeAnimation);
					if (elements[i].GetComponent<UIShiny>().effectFactor < 1f)
					{
						elements[i].GetComponent<UIShiny>().effectFactor += Time.unscaledDeltaTime * 2f;
					}
				}
				else
				{
					elements[i].GetComponent<UIShadow>().effectColor = new Color(elements[i].GetComponent<UIShadow>().effectColor.r, elements[i].GetComponent<UIShadow>().effectColor.g, elements[i].GetComponent<UIShadow>().effectColor.b, 1f - timeAnimation);
					if (elements[i].GetComponent<UIShiny>().effectFactor < 1f)
					{
						elements[i].GetComponent<UIShiny>().effectFactor += Time.unscaledDeltaTime * 2f;
					}
				}
				if (elements[i].GetComponent<UIShiny>().brightness > 0f)
				{
					elements[i].GetComponent<UIShiny>().brightness -= Time.unscaledDeltaTime * 3.5f;
					if (elements[i].GetComponent<UIShiny>().brightness < 0f)
					{
						elements[i].GetComponent<UIShiny>().brightness = 0f;
					}
				}
			}
		}
		if (timeStart > 0)
		{
			timeStart--;
		}
		if (!(menuButton == null) && (!(menuButton != null) || !menuButton.keyMove))
		{
			return;
		}
		if (inputPosition && (double)Input.GetAxis("Vertical") > -0.5 && (double)Input.GetAxis("Vertical") < 0.5 && (double)Input.GetAxis("Horizontal") < 0.5 && (double)Input.GetAxis("Horizontal") > -0.5)
		{
			inputPosition = false;
		}
		if (changeNow && !stopKey)
		{
			if (Input.GetButtonDown("Submit") || Input.GetButtonDown("Interactive"))
			{
				PointerDown();
			}
			if (changeUp != null && !inputPosition && (double)Input.GetAxis("Vertical") > 0.5)
			{
				ButtonMouseClick buttonMouseClick = changeUp;
				while (!buttonMouseClick.interactable)
				{
					buttonMouseClick = buttonMouseClick.changeUp;
				}
				buttonMouseClick.keyDown();
				buttonMouseClick.PointerEnter();
			}
			if (changeDown != null && !inputPosition && (double)Input.GetAxis("Vertical") < -0.5)
			{
				ButtonMouseClick buttonMouseClick2 = changeDown;
				while (!buttonMouseClick2.interactable)
				{
					buttonMouseClick2 = buttonMouseClick2.changeDown;
				}
				buttonMouseClick2.keyDown();
				buttonMouseClick2.PointerEnter();
			}
			if (changeLeft != null && !inputPosition && (double)Input.GetAxis("Horizontal") < -0.5)
			{
				ButtonMouseClick buttonMouseClick3 = changeLeft;
				while (!buttonMouseClick3.interactable)
				{
					buttonMouseClick3 = buttonMouseClick3.changeLeft;
				}
				buttonMouseClick3.keyDown();
				buttonMouseClick3.PointerEnter();
			}
			if (changeRight != null && !inputPosition && (double)Input.GetAxis("Horizontal") > 0.5)
			{
				ButtonMouseClick buttonMouseClick4 = changeRight;
				while (!buttonMouseClick4.interactable)
				{
					buttonMouseClick4 = buttonMouseClick4.changeRight;
				}
				buttonMouseClick4.keyDown();
				buttonMouseClick4.PointerEnter();
			}
		}
		stopKey = false;
	}

	public void keyDown()
	{
		if (base.transform.parent.GetComponent<MenuScrolac>() != null)
		{
			base.transform.parent.GetComponent<MenuScrolac>().UpdateScrolac();
		}
	}

	public void LockClick(bool x)
	{
		lockButton = x;
	}

	public void ActivationInteractive(bool x)
	{
		interactable = x;
		if (!x)
		{
			changeNow = false;
			timeAnimation = 0f;
			if (GetComponent<UI_Colors>() != null)
			{
				GetComponent<UI_Colors>().ShareAlpha(4f);
			}
		}
		else if (GetComponent<UI_Colors>() != null)
		{
			GetComponent<UI_Colors>().ShareAlpha(1f);
		}
		if (GetComponent<Button>() != null)
		{
			GetComponent<Button>().enabled = x;
		}
	}

	/// <summary> 鼠标/焦点离开时：触发 eventExit、恢复颜色，并调用 MenuCaseOption.MouseExit() 做预览还原。 </summary>
	public void PointerExit()
	{
		if (interactable)
		{
			eventExit.Invoke();
			changeNow = false;
			if (GetComponent<UI_Colors>() != null)
			{
				GetComponent<UI_Colors>().SetColorImage(0, colorExit);
			}
			timeAnimation = 0f;
		}
		if (GetComponent<MenuCaseOption>() != null)
			GetComponent<MenuCaseOption>().MouseExit();
	}

	/// <summary> 鼠标/焦点进入时：触发 eventEnter、设置高亮，并调用 MenuCaseOption.MouseEnter() 做悬停预览。 </summary>
	public void PointerEnter()
	{
		if (!firstStart)
		{
			Start();
		}
		if (interactable && !lockButton)
		{
			eventEnter.Invoke();
			changeNow = true;
			if (GetComponent<UI_Colors>() != null)
			{
				GetComponent<UI_Colors>().SetColorImage(0, colorEnter);
			}
			stopKey = true;
			timeAnimation = 0f;
			if (menuButton != null)
			{
				menuButton.EnterButton();
			}
			for (int i = 0; i < elements.Length; i++)
			{
				elements[i].GetComponent<UIShiny>().effectFactor = 0f;
				elements[i].GetComponent<UIShiny>().brightness = 1f;
			}
			if (menuButton != null)
			{
				menuButton.ChangeCase(base.gameObject);
			}
			if (change != null)
			{
				change.anchoredPosition = GetComponent<RectTransform>().anchoredPosition;
				if (base.transform.parent.GetComponent<MenuScrolac>() != null && base.transform.parent.GetComponent<MenuScrolac>().changeCopyRect)
				{
					change.sizeDelta = GetComponent<RectTransform>().sizeDelta;
				}
			}
		}
		if (Input.GetAxis("Vertical") != 0f || Input.GetAxis("Horizontal") != 0f)
		{
			inputPosition = true;
		}
		if (GetComponent<MenuCaseOption>() != null)
		{
			MenuCaseOption component = GetComponent<MenuCaseOption>();
			if (component.isActiveAndEnabled)
			{
				component.MouseEnter();
			}
		}
	}

	/// <summary> 点击/确认键按下时调用：先 eventClick，再 MenuNextLocation.Click()，再 MenuCaseOption.Click()，最后音效。 </summary>
	public void PointerDown()
	{
		if (lockButton || !interactable)
		{
			return;
		}
		eventClick.Invoke();
		if (GetComponent<MenuNextLocation>() != null)
		{
			MenuNextLocation component = GetComponent<MenuNextLocation>();
			if (component.isActiveAndEnabled)
			{
				component.Click();
			}
		}
		if (GetComponent<MenuCaseOption>() != null)
		{
			MenuCaseOption component2 = GetComponent<MenuCaseOption>();
			if (component2.isActiveAndEnabled)
			{
				component2.Click();
			}
		}
		if (menuButton != null && !offSound)
		{
			if (!toggleSound)
			{
				menuButton.ClickButton();
			}
			else
			{
				menuButton.ClickButtonToggle();
			}
		}
		if (GetComponent<ButtonMouseClickBubble>() != null)
		{
			GetComponent<ButtonMouseClickBubble>().Click();
		}
	}

	public void PointerUp()
	{
		eventUp.Invoke();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		PointerEnter();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (timeStart == 0)
		{
			PointerDown();
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		PointerExit();
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		PointerUp();
	}
}
