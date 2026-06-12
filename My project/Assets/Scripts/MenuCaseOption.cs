// based on the original game.Yen Chezky(yenichw)
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary> 一组 Sprite，用于多图替换（与 objectsAddone 一一对应）. </summary>
[System.Serializable]
public class SpriteSet
{
	public Sprite[] sprites;
}

public class MenuCaseOption : MonoBehaviour
{
	public enum TypeCaseOption
	{
		// 游戏音量调节（滑动条控制，0~1 浮点值）
		Volume = 0,
		// 后期处理总开关（开启/关闭，控制画面后处理效果）
		PostProcessing = 1,
		// 色彩效果开关（开启/关闭画面色彩滤镜/特效）
		ColorEffect = 2,
		// 环境光遮蔽（AO）效果开关（开启/关闭，提升画面光影层次感）
		AO = 3,
		// 垂直同步（VSync）开关（开启/关闭，同步帧率与显示器刷新率，防止画面撕裂）
		VSync = 4,
		// 抗锯齿等级调节（滑动条控制，整数档位，如0=无、1=MSAAx2等）
		Antialiasing = 5,
		// 屏幕分辨率设置（下拉选择，读取系统分辨率列表并应用）
		Resolution = 6,
		// 窗口模式开关（窗口/全屏切换，0=窗口、1=全屏）
		WindowMode = 7,
		// 游戏语言选择（读取Data/Languages目录下的语言包，切换界面文本语言）
		Language = 8,
		// 鼠标移动速度调节（滑动条控制，浮点值，基础速度+偏移量）
		SpeedMouse = 9,
		// 鼠标移动插值开关（开启/关闭鼠标移动平滑插值，0=关闭、1=开启）
		SpeedMouseLerp = 10,
		// 永久字幕开关（开启/关闭全局永久显示字幕，0=关闭、1=开启）
		EverSub = 11,
		// 画面亮度调节（滑动条控制，整数档位，调节全局画面亮度）
		Bright = 12,
		// 头部移动开关（开启/关闭角色头部跟随鼠标/视角移动，0=关闭、1=开启）
		HeadMove = 13,
		// 屏幕提示开关（开启/关闭游戏内操作提示、指引文本，0=关闭、1=开启）
		HintScreen = 14,
		// 语音播放器音量/档位调节（滑动条控制，整数档位，切换语音包并预览）
		VoicePlayer = 15,
		// 阴影质量调节（滑动条控制，整数档位，0=无阴影、1=低、2=中等、3=高等）
		Shadow = 16,
		// 粒子效果开关/等级（控制游戏内粒子特效如烟雾、火焰的显示等级）
		Particles = 17,
		// 泛光（Bloom）效果开关（开启/关闭画面高光泛光效果，提升画面亮度层次感）
		Bloom = 18,
		// 世界画质等级调节（滑动条控制，整数档位，全局场景画质总开关）
		QualityWorld = 19,
		// 加载游戏存档（点击触发存档加载逻辑，鼠标悬停显示交互状态）
		Loadgame = 20,
		// 视野（FOV）调节（滑动条控制，浮点值，调整相机视野角度，影响画面可视范围）
		FOV = 21,
		// 退出游戏（点击触发Application.Quit()，关闭游戏进程）
		ExitGame = 22,

		// 使用默认模型（API 总开关）
		ApiUseDefault = 23,

		// 知识库 / RAG：单独控制三个 Memory
		Memory1 = 24,
		Memory2 = 25,
		Memory3 = 26,

		// 悬停预览、点击后替换（先做预览）
		BackgroundImage = 27,
		CharacterModel = 28,

		// 使用默认背景和模型：开=应用默认并禁用背景1/模型1，关=启用二者
		UseDefaultBackgroundAndModel = 29
	}

	public TypeCaseOption option;

	[Header("Addone")]
	public GameObject[] objectsAddone;

	public GameObject locationChangeOption;

	public List<Interface_ChangeScreenButton_Class_ButtonInfo> scrIccb;

	/// <summary> 通用预览图（单张）：BackgroundImage 用于背景预览/应用，CharacterModel 用于模型预览图。与 objectsAddone[0] 的 Image 绑定。 </summary>
	[Header("预览图（单张，通用）")]
	public Sprite previewSprite;

	[Header("BackgroundImage 额外参数（可选）")]
	[Tooltip("用于持久化到 PlayerPrefs，SceneChat 根据此索引应用背景。每个背景选项需配置唯一索引（0,1,2...）")]
	public int backgroundIndex;
	public bool overrideBackgroundColor;
	public Color backgroundColor = Color.white;
	public bool overrideBackgroundImageType;
	public Image.Type backgroundImageType = Image.Type.Simple;
	public bool overrideBackgroundPixelsPerUnitMultiplier;
	public float backgroundPixelsPerUnitMultiplier = 1f;

	public Sprite spriteToggleY;

	public Sprite spriteToggleN;

	public float defaultSlide;

	private int intiResolution;

	private Resolution[] resolutions;

	private string[] languages;

	private int iLanguage;

	private float secondF;

	private int frames;

	private bool inputPosition;

	private float timeStartSec;

	private int timeStart;

	private Slider slider;

	private bool sliderFloat;

	private float sliderInt;

	private GameObject toggleImg;

	private int toggleActive;

	private ButtonMouseClick scrbmc;

	private bool firstStart;

	// 预览用：鼠标移出时恢复
	private static Image _currentBackgroundImage;
	private static Sprite _currentBackgroundSprite;
	// 多图预览恢复（通用）
	private static Image[] _revertImages;
	private static Sprite[] _revertSprites;

	[Header("UseDefaultBackgroundAndModel 用")]
	public Sprite defaultBackgroundSprite;
	public GameObject defaultBackgroundImageTarget;
	public Transform defaultModelContainer;
	public int defaultModelIndex = 0;
	public Transform actualSceneModelRoot;

	[Header("CharacterModel 点击替换场景模型用")]
	public int characterModelIndex = 0;
	public Transform actualPlayerModelRoot;

	private void OnEnable()
	{
		timeStart = 3;
		timeStartSec = 1f;
		if (!firstStart)
		{
			firstStart = true;
			if (base.transform.Find("Slider") != null)
			{
				slider = base.transform.Find("Slider").GetComponent<Slider>();
			}
			if (base.transform.Find("Case/CaseCheck") != null)
			{
				toggleImg = base.transform.Find("Case/CaseCheck").gameObject;
			}
			if (GetComponent<ButtonMouseClick>() != null)
			{
				scrbmc = GetComponent<ButtonMouseClick>();
				scrbmc.Start();
			}
			if (option == TypeCaseOption.Resolution)
			{
				int num = 0;
				int num2 = 0;
				resolutions = Screen.resolutions;
				for (int i = 0; i < resolutions.Length; i++)
				{
					if (resolutions[i].width == PlayerPrefs.GetInt("XScreen", resolutions[resolutions.Length - 1].width) && resolutions[i].height == PlayerPrefs.GetInt("XScreen", resolutions[resolutions.Length - 1].height))
					{
						intiResolution = i;
					}
					if (num != resolutions[i].width && num2 != resolutions[i].height)
					{
						num = resolutions[i].width;
						num2 = resolutions[i].height;
						Interface_ChangeScreenButton_Class_ButtonInfo item = new Interface_ChangeScreenButton_Class_ButtonInfo
						{
							buttonText = resolutions[i].width + ":" + resolutions[i].height,
							value_int = i
						};
						scrIccb.Add(item);
					}
				}
				base.transform.Find("Text Resolution").GetComponent<Text>().text = PlayerPrefs.GetInt("XScreen", resolutions[intiResolution].width) + ":" + PlayerPrefs.GetInt("YScreen", resolutions[intiResolution].height);
			}
			if (option == TypeCaseOption.WindowMode)
			{
				toggleActive = PlayerPrefs.GetInt("WindowMode", 0);
			}
			if (option == TypeCaseOption.VSync)
			{
				toggleActive = PlayerPrefs.GetInt("VSync", 0);
			}
			if (option == TypeCaseOption.PostProcessing)
			{
				toggleActive = PlayerPrefs.GetInt("PostProcessing", 1);
			}
			if (option == TypeCaseOption.Bright)
			{
				sliderInt = PlayerPrefs.GetInt("Bright", 0);
			}
			if (option == TypeCaseOption.ColorEffect)
			{
				toggleActive = PlayerPrefs.GetInt("ColorEffects", 1);
			}
			if (option == TypeCaseOption.AO)
			{
				toggleActive = PlayerPrefs.GetInt("AO", 1);
			}
			if (option == TypeCaseOption.Bloom)
			{
				toggleActive = PlayerPrefs.GetInt("Bloom", 1);
			}
			if (option == TypeCaseOption.Antialiasing)
			{
				sliderInt = PlayerPrefs.GetInt("Antialiasing", 1);
			}
			if (option == TypeCaseOption.Shadow)
			{
				sliderInt = PlayerPrefs.GetInt("Shadow", 2);
			}
			if (option == TypeCaseOption.QualityWorld)
			{
				sliderInt = PlayerPrefs.GetInt("QualityWorld", 2);
			}
			if (option == TypeCaseOption.Volume)
			{
				sliderFloat = true;
				sliderInt = PlayerPrefs.GetFloat("Volume", 1f);
			}
			if (option == TypeCaseOption.Language)
			{
				string languagesRoot = Path.Combine(Application.streamingAssetsPath, "Data", "Languages");
				languages = Directory.GetDirectories(languagesRoot);
				for (int j = 0; j < languages.Length; j++)
				{
					languages[j] = languages[j].Replace(languagesRoot + Path.DirectorySeparatorChar, "");
				}
				base.transform.Find("Text Language").GetComponent<Text>().text = PlayerPrefs.GetString("Language", "Chinese");
				for (int k = 0; k < languages.Length; k++)
				{
					Interface_ChangeScreenButton_Class_ButtonInfo item2 = new Interface_ChangeScreenButton_Class_ButtonInfo
					{
						buttonText = languages[k],
						value_int = k
					};
					scrIccb.Add(item2);
				}
			}
			if (option == TypeCaseOption.SpeedMouse)
			{
				sliderFloat = true;
				sliderInt = 1f + PlayerPrefs.GetFloat("MouseSpeed", 0f);
			}
			if (option == TypeCaseOption.SpeedMouseLerp)
			{
				toggleActive = PlayerPrefs.GetInt("MouseLerp", 0);
			}
			if (option == TypeCaseOption.EverSub)
			{
				toggleActive = PlayerPrefs.GetInt("EverSub", 0);
			}
			if (option == TypeCaseOption.HintScreen)
			{
				toggleActive = PlayerPrefs.GetInt("HintScreen", 1);
			}
			if (option == TypeCaseOption.HeadMove)
			{
				toggleActive = PlayerPrefs.GetInt("HeadMove", 1);
			}
			if (option == TypeCaseOption.VoicePlayer)
			{
				sliderFloat = true;
				sliderInt = PlayerPrefs.GetInt("VoicePlayer", 0);
			}
			if (option == TypeCaseOption.FOV)
			{
				sliderFloat = true;
				sliderInt = PlayerPrefs.GetFloat("FOV", 60f);
			}
			// API 默认模型总开关：1=使用默认模型，0=使用自定义设置
			if (option == TypeCaseOption.ApiUseDefault)
			{
				toggleActive = PlayerPrefs.GetInt("ApiUseDefault", 1);
			}
			// Memory1/2/3：1=使用该 Memory，0=不使用
			if (option == TypeCaseOption.Memory1)
			{
				toggleActive = PlayerPrefs.GetInt("Memory1", 0);
			}
			if (option == TypeCaseOption.Memory2)
			{
				toggleActive = PlayerPrefs.GetInt("Memory2", 0);
			}
			if (option == TypeCaseOption.Memory3)
			{
				toggleActive = PlayerPrefs.GetInt("Memory3", 0);
			}
			if (option == TypeCaseOption.UseDefaultBackgroundAndModel)
			{
				toggleActive = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1);
			}
			UpdateCase();
		}
		if (option == TypeCaseOption.UseDefaultBackgroundAndModel)
		{
			toggleActive = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1);
			UpdateCase();
			Debug.Log($"[MenuCaseOption] UseDefaultBackgroundAndModel OnEnable: 从 PlayerPrefs 读取 = {toggleActive}，即将应用状态", this);
			ApplyUseDefaultBackgroundAndModelState();
		}
		if (option == TypeCaseOption.VSync)
		{
			frames = 0;
			secondF = 0f;
			objectsAddone[0].GetComponent<Text>().text = "FPS";
		}
	}

	private void Update()
	{
		if (scrbmc != null && scrbmc.changeNow)
		{
			if (Input.GetAxis("Horizontal") == 0f)
			{
				inputPosition = false;
			}
			if (!inputPosition)
			{
				if ((double)Input.GetAxis("Horizontal") < -0.5)
				{
					inputPosition = true;
					if (slider != null)
					{
						if (sliderInt >= 0f)
						{
							slider.value -= 1f;
						}
						else
						{
							slider.value -= 0.1f;
						}
					}
					if (toggleImg != null)
					{
						Click();
					}
				}
				if ((double)Input.GetAxis("Horizontal") > 0.5)
				{
					inputPosition = true;
					if (slider != null)
					{
						if (sliderInt >= 0f)
						{
							slider.value += 1f;
						}
						else
						{
							slider.value += 0.1f;
						}
					}
					if (toggleImg != null)
					{
						Click();
					}
				}
			}
		}
		if (timeStart > 0)
		{
			timeStart--;
		}
		if (timeStartSec > 0f)
		{
			timeStartSec -= Time.fixedDeltaTime;
			if (timeStartSec <= 0f)
			{
				timeStartSec = 0f;
				if (option == TypeCaseOption.PostProcessing)
				{
					toggleActive = PlayerPrefs.GetInt("PostProcessing", 1);
					if (toggleActive == 0)
					{
						for (int i = 0; i < objectsAddone.Length; i++)
						{
							objectsAddone[i].GetComponent<ButtonMouseClick>().ActivationInteractive(x: false);
						}
					}
					else
					{
						for (int j = 0; j < objectsAddone.Length; j++)
						{
							objectsAddone[j].GetComponent<ButtonMouseClick>().ActivationInteractive(x: true);
						}
					}
				}
			}
		}
		if (option == TypeCaseOption.VSync)
		{
			secondF += Time.unscaledDeltaTime;
			frames++;
			if (secondF >= 1f)
			{
				objectsAddone[0].GetComponent<Text>().text = "FPS [" + frames + "]";
				frames = 0;
				secondF = 0f;
			}
		}
	}

	public void Click()
	{
		bool flag = false;
		if (slider != null)
		{
			if (!sliderFloat)
			{
				sliderInt = Mathf.FloorToInt(slider.value);
			}
			else
			{
				sliderInt = slider.value;
			}
		}
		if (toggleImg != null)
		{
			if (toggleActive == 1)
			{
				toggleActive = 0;
			}
			else
			{
				toggleActive = 1;
			}
		}
		if (option == TypeCaseOption.Resolution)
		{
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().eventReturn = new UnityEvent();
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().eventReturn.AddListener(ChangeResoulution);
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().Create(base.transform.Find("Text").GetComponent<Text>().text, scrIccb, Interface_ChangeScreenButton.TypeChangeScreenButton.ReturnInt, base.transform.parent.gameObject, base.gameObject);
		}
		if (option == TypeCaseOption.WindowMode)
		{
			flag = true;
			PlayerPrefs.SetInt("WindowMode", toggleActive);
		}
		if (option == TypeCaseOption.VSync)
		{
			flag = true;
			PlayerPrefs.SetInt("VSync", toggleActive);
			frames = 0;
			secondF = 0f;
			objectsAddone[0].GetComponent<Text>().text = "FPS";
		}
		if (option == TypeCaseOption.PostProcessing)
		{
			flag = true;
			PlayerPrefs.SetInt("PostProcessing", toggleActive);
			if (toggleActive == 0)
			{
				for (int i = 0; i < objectsAddone.Length; i++)
				{
					objectsAddone[i].GetComponent<ButtonMouseClick>().ActivationInteractive(x: false);
				}
			}
			else
			{
				for (int j = 0; j < objectsAddone.Length; j++)
				{
					objectsAddone[j].GetComponent<ButtonMouseClick>().ActivationInteractive(x: true);
				}
			}
		}
		if (option == TypeCaseOption.Bright)
		{
			flag = true;
			PlayerPrefs.SetInt("Bright", (int)sliderInt);
		}
		if (option == TypeCaseOption.ColorEffect)
		{
			flag = true;
			PlayerPrefs.SetInt("ColorEffects", toggleActive);
		}
		if (option == TypeCaseOption.AO)
		{
			flag = true;
			PlayerPrefs.SetInt("AO", toggleActive);
		}
		if (option == TypeCaseOption.Bloom)
		{
			flag = true;
			PlayerPrefs.SetInt("Bloom", toggleActive);
		}
		if (option == TypeCaseOption.Antialiasing)
		{
			flag = true;
			PlayerPrefs.SetInt("Antialiasing", (int)sliderInt);
		}
		if (option == TypeCaseOption.Shadow)
		{
			flag = true;
			PlayerPrefs.SetInt("Shadow", (int)sliderInt);
		}
		if (option == TypeCaseOption.QualityWorld)
		{
			flag = true;
			PlayerPrefs.SetInt("QualityWorld", (int)sliderInt);
		}
		if (option == TypeCaseOption.ApiUseDefault)
		{
			// 1 = 使用默认模型，0 = 使用自定义模型
			flag = true;
			PlayerPrefs.SetInt("ApiUseDefault", toggleActive);

			Debug.Log($"[API Settings] ApiUseDefault 切换为 {(toggleActive == 1 ? "使用默认模型" : "使用自定义设置")}，objectsAddone 数量 = {objectsAddone.Length}", this);

			// 当使用默认模型时：禁用 objectsAddone 里配置的按钮（对话模型 / API_Key / 语言模型 / 语音API_Key 等）
			if (toggleActive == 1)
			{
				for (int k = 0; k < objectsAddone.Length; k++)
				{
					if (objectsAddone[k] != null && objectsAddone[k].GetComponent<ButtonMouseClick>() != null)
					{
						Debug.Log($"[API Settings] 关闭交互: {objectsAddone[k].name}", objectsAddone[k]);
						objectsAddone[k].GetComponent<ButtonMouseClick>().ActivationInteractive(x: false);
					}
				}
			}
			else
			{
				for (int l = 0; l < objectsAddone.Length; l++)
				{
					if (objectsAddone[l] != null && objectsAddone[l].GetComponent<ButtonMouseClick>() != null)
					{
						Debug.Log($"[API Settings] 开启交互: {objectsAddone[l].name}", objectsAddone[l]);
						objectsAddone[l].GetComponent<ButtonMouseClick>().ActivationInteractive(x: true);
					}
				}
			}
		}
		if (option == TypeCaseOption.Memory1)
		{
			// 1 = 使用 Memory1，0 = 不使用
			PlayerPrefs.SetInt("Memory1", toggleActive);
			Debug.Log($"[RAG Settings] Memory1 切换为 {(toggleActive == 1 ? "启用" : "禁用")}", this);
		}
		if (option == TypeCaseOption.Memory2)
		{
			PlayerPrefs.SetInt("Memory2", toggleActive);
			Debug.Log($"[RAG Settings] Memory2 切换为 {(toggleActive == 1 ? "启用" : "禁用")}", this);
		}
		if (option == TypeCaseOption.Memory3)
		{
			PlayerPrefs.SetInt("Memory3", toggleActive);
			Debug.Log($"[RAG Settings] Memory3 切换为 {(toggleActive == 1 ? "启用" : "禁用")}", this);
		}
		if (option == TypeCaseOption.Volume)
		{
			GlobalGame.VolumeGame = slider.value;
			AudioListener.volume = GlobalGame.VolumeGame;
			PlayerPrefs.SetFloat("Volume", GlobalGame.VolumeGame);
		}
		if (option == TypeCaseOption.Language)
		{
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().eventReturn = new UnityEvent();
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().eventReturn.AddListener(ChangeLanguage);
			locationChangeOption.GetComponent<Interface_ChangeScreenButton>().Create(base.transform.Find("Text").GetComponent<Text>().text, scrIccb, Interface_ChangeScreenButton.TypeChangeScreenButton.ReturnInt, base.transform.parent.gameObject, base.gameObject);
		}
		if (option == TypeCaseOption.SpeedMouse)
		{
			GlobalGame.mouseSpeed = slider.value - 1f;
			PlayerPrefs.SetFloat("MouseSpeed", GlobalGame.mouseSpeed);
		}
		if (option == TypeCaseOption.SpeedMouseLerp)
		{
			if (toggleActive == 0)
			{
				GlobalGame.mouseSpeedLerp = false;
			}
			else
			{
				GlobalGame.mouseSpeedLerp = true;
			}
			PlayerPrefs.SetInt("MouseLerp", toggleActive);
		}
		if (option == TypeCaseOption.EverSub)
		{
			if (toggleActive == 0)
			{
				GlobalGame.everSub = false;
			}
			else
			{
				GlobalGame.everSub = true;
			}
			PlayerPrefs.SetInt("EverSub", toggleActive);
		}
		if (option == TypeCaseOption.HintScreen)
		{
			if (toggleActive == 0)
			{
				GlobalGame.hintScreen = false;
			}
			else
			{
				GlobalGame.hintScreen = true;
			}
			PlayerPrefs.SetInt("HintScreen", toggleActive);
		}
		if (option == TypeCaseOption.HeadMove)
		{
			if (toggleActive == 0)
			{
				GlobalGame.headMove = false;
			}
			else
			{
				GlobalGame.headMove = true;
			}
			PlayerPrefs.SetInt("HeadMove", toggleActive);
		}
		if (option == TypeCaseOption.VoicePlayer)
		{
			flag = true;
			GlobalGame.voicePlayer = Mathf.FloorToInt(slider.value);
			PlayerPrefs.SetInt("VoicePlayer", GlobalGame.voicePlayer);
			if (timeStart == 0)
			{
				GetComponent<AudioSource>().clip = (Resources.Load("DataVoicePlayer") as GameObject).GetComponent<Audio_Data>().sounds[GlobalGame.voicePlayer];
				GetComponent<AudioSource>().Play();
			}
		}
		if (option == TypeCaseOption.FOV)
		{
			GlobalGame.playerFov = slider.value;
			PlayerPrefs.SetFloat("FOV", GlobalGame.playerFov);
			GlobalTag.player.GetComponent<PlayerMove>().UpdateSettingsCamera();
		}
		if (option == TypeCaseOption.BackgroundImage)
		{
			if (previewSprite == null)
			{
				Debug.LogWarning("[MenuCaseOption] BackgroundImage 点击：未配置 previewSprite，无法替换背景", this);
			}
			else
			{
				// 优先使用实际背景目标（场景中的真正背景 Image），如果没配置则退回到预览目标 objectsAddone[0]
				Image img = null;
				if (defaultBackgroundImageTarget != null)
				{
					img = defaultBackgroundImageTarget.GetComponent<Image>();
				}
				else if (objectsAddone != null && objectsAddone.Length > 0)
				{
					img = objectsAddone[0].GetComponent<Image>();
				}

				if (img != null)
				{
					img.sprite = previewSprite;
					// 可选：一并修改颜色和 Image Type
					if (overrideBackgroundColor)
						img.color = backgroundColor;
					if (overrideBackgroundImageType)
						img.type = backgroundImageType;
					if (overrideBackgroundPixelsPerUnitMultiplier)
						img.pixelsPerUnitMultiplier = backgroundPixelsPerUnitMultiplier;

					// 若当前选项使用了悬停预览（MouseEnter/MouseExit），点击后重新保存当前状态，
					// 确保鼠标移出时不会把已经“确认”的背景又还原回去。
					if (option == TypeCaseOption.BackgroundImage)
						SaveRevertState();

					// 持久化背景索引，供 SceneChat 等场景读取
					PlayerPrefs.SetInt("MenuBackgroundIndex", backgroundIndex);

					Debug.Log($"[MenuCaseOption] 点击替换背景（单张） sprite={previewSprite.name}, target={img.gameObject.name}, backgroundIndex={backgroundIndex} 已写 PlayerPrefs", this);
				}
				else
				{
					Debug.LogWarning("[MenuCaseOption] BackgroundImage 点击：未找到可用的 Image 目标（请配置 defaultBackgroundImageTarget 或 objectsAddone[0]）", this);
				}
			}
		}
		if (option == TypeCaseOption.CharacterModel)
		{
			if (actualPlayerModelRoot != null)
			{
				ApplyCharacterModelToContainer(actualPlayerModelRoot, characterModelIndex);
				PlayerPrefs.SetInt("CharacterModelIndex", characterModelIndex);
				Debug.Log($"[MenuCaseOption] CharacterModel 点击: 已应用模型 actualPlayerModelRoot={actualPlayerModelRoot.name}, characterModelIndex={characterModelIndex}, 已写 PlayerPrefs", this);
			}
			else
				Debug.LogWarning("[MenuCaseOption] CharacterModel 点击: actualPlayerModelRoot 未配置", this);
			if (objectsAddone != null && objectsAddone.Length > 0 && previewSprite != null)
			{
				Image img = objectsAddone[0].GetComponent<Image>();
				if (img != null)
				{
					img.sprite = previewSprite;
					Debug.Log($"[MenuCaseOption] CharacterModel 点击: 已设预览图 previewSprite={previewSprite.name}, objectsAddone[0]={objectsAddone[0].name}", this);
				}
			}
		}
		if (option == TypeCaseOption.UseDefaultBackgroundAndModel)
		{
			PlayerPrefs.SetInt("UseDefaultBackgroundAndModel", toggleActive);
			Debug.Log($"[MenuCaseOption] UseDefaultBackgroundAndModel Click: 切换为 {toggleActive}，已写回 PlayerPrefs，即将应用状态", this);
			ApplyUseDefaultBackgroundAndModelState();
		}
		if (option == TypeCaseOption.ExitGame)
		{
			Debug.Log("[MenuCaseOption] ExitGame Click: 收到退出游戏点击，执行 Application.Quit()", this);
			Application.Quit();
		}
		if (flag)
		{
			GameObject.FindWithTag("Game").GetComponent<OptionsGame>().ReloadOptions();
		}
		UpdateCase();
	}

	public void ChangeResoulution()
	{
		intiResolution = locationChangeOption.GetComponent<Interface_ChangeScreenButton>().returnInt;
		base.transform.Find("Text Resolution").GetComponent<Text>().text = resolutions[intiResolution].width + ":" + resolutions[intiResolution].height;
		PlayerPrefs.SetInt("XScreen", resolutions[intiResolution].width);
		PlayerPrefs.SetInt("YScreen", resolutions[intiResolution].height);
		Screen.SetResolution(resolutions[intiResolution].width, resolutions[intiResolution].height, Screen.fullScreen, 60);
	}

	public void ChangeLanguage()
	{
		iLanguage = locationChangeOption.GetComponent<Interface_ChangeScreenButton>().returnInt;
		base.transform.Find("Text Language").GetComponent<Text>().text = languages[iLanguage];
		GlobalGame.Language = languages[iLanguage];
		PlayerPrefs.SetString("Language", languages[iLanguage]);
		GameObject.FindWithTag("Game").GetComponent<OptionsGame>().ReloadLanguage();
		Component[] componentsInChildren = GameObject.FindWithTag("World").GetComponentsInChildren(typeof(Localization_UIText), includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].GetComponent<Localization_UIText>().TextTranslate();
		}
		componentsInChildren = GameObject.FindWithTag("World").GetComponentsInChildren(typeof(UI_TextFontLanguage), includeInactive: true);
		for (int j = 0; j < componentsInChildren.Length; j++)
		{
			componentsInChildren[j].GetComponent<UI_TextFontLanguage>().FontUpdate();
		}
	}

	public void ResetDefault()
	{
		sliderInt = defaultSlide;
		slider.value = sliderInt;
		Click();
	}

	private static void ApplyCharacterModelToContainer(Transform container, int index)
	{
		if (container == null || index < 0) return;
		int count = container.childCount;
		if (count == 0) return;
		index = Mathf.Clamp(index, 0, count - 1);
		for (int i = 0; i < count; i++)
			container.GetChild(i).gameObject.SetActive(i == index);
	}

	private void ApplyUseDefaultBackgroundAndModelState()
	{
		bool useDefault = (toggleActive == 1);
		Debug.Log($"[MenuCaseOption] ApplyUseDefaultBackgroundAndModelState: useDefault={useDefault}", this);
		if (useDefault)
		{
			if (defaultBackgroundSprite != null && defaultBackgroundImageTarget != null)
			{
				Image img = defaultBackgroundImageTarget.GetComponent<Image>();
				if (img != null)
				{
					img.sprite = defaultBackgroundSprite;
					Debug.Log($"[MenuCaseOption] 已设默认背景: sprite={defaultBackgroundSprite.name}, target={defaultBackgroundImageTarget.name}", this);
				}
			}
			else if (defaultBackgroundSprite == null || defaultBackgroundImageTarget == null)
				Debug.LogWarning("[MenuCaseOption] 使用默认背景但未配置 defaultBackgroundSprite 或 defaultBackgroundImageTarget", this);
			if (actualSceneModelRoot != null)
			{
				ApplyCharacterModelToContainer(actualSceneModelRoot, defaultModelIndex);
				Debug.Log($"[MenuCaseOption] 已应用默认模型: actualSceneModelRoot={actualSceneModelRoot.name}, defaultModelIndex={defaultModelIndex}", this);
			}
			else
				Debug.LogWarning("[MenuCaseOption] 使用默认模型但 actualSceneModelRoot 未配置", this);
		}
		if (objectsAddone != null)
		{
			for (int i = 0; i < objectsAddone.Length; i++)
			{
				if (objectsAddone[i] == null) continue;
				ButtonMouseClick b = objectsAddone[i].GetComponent<ButtonMouseClick>();
				if (b != null)
				{
					b.ActivationInteractive(!useDefault);
					Debug.Log($"[MenuCaseOption] objectsAddone[{i}] ({objectsAddone[i].name}) 交互={(useDefault ? "禁用" : "启用")}", this);
				}
			}
		}
	}

	/// <summary> 通用：将一组 Sprite 应用到 objectsAddone 的 Image 上，一一对应。返回实际设置的个数。 </summary>
	private static int ApplySpritesToObjects(GameObject[] objects, Sprite[] sprites)
	{
		if (objects == null || sprites == null || sprites.Length == 0) return 0;
		int n = Mathf.Min(objects.Length, sprites.Length);
		for (int i = 0; i < n; i++)
		{
			if (objects[i] == null) continue;
			Image img = objects[i].GetComponent<Image>();
			if (img != null && sprites[i] != null)
				img.sprite = sprites[i];
		}
		return n;
	}

	/// <summary> 保存当前 objectsAddone 的 Image.sprite 到静态变量，用于鼠标移出时恢复。 </summary>
	private void SaveRevertState()
	{
		if (objectsAddone == null || objectsAddone.Length == 0) return;
		int n = 0;
		for (int i = 0; i < objectsAddone.Length; i++)
		{
			if (objectsAddone[i] != null && objectsAddone[i].GetComponent<Image>() != null) n++;
		}
		if (n == 0) return;
		_revertImages = new Image[n];
		_revertSprites = new Sprite[n];
		int idx = 0;
		for (int i = 0; i < objectsAddone.Length && idx < n; i++)
		{
			if (objectsAddone[i] == null) continue;
			Image img = objectsAddone[i].GetComponent<Image>();
			if (img != null)
			{
				_revertImages[idx] = img;
				_revertSprites[idx] = img.sprite;
				idx++;
			}
		}
	}

	/// <summary> 恢复之前保存的多图状态（鼠标移出预览时调用）。 </summary>
	private static void RevertToSavedState()
	{
		if (_revertImages == null || _revertSprites == null) return;
		int n = Mathf.Min(_revertImages.Length, _revertSprites.Length);
		for (int i = 0; i < n; i++)
		{
			if (_revertImages[i] != null && _revertSprites[i] != null)
				_revertImages[i].sprite = _revertSprites[i];
		}
	}

	public void MouseEnter()
	{
		if (option == TypeCaseOption.Loadgame)
		{
			if (scrbmc.interactable)
			{
				objectsAddone[0].GetComponent<Image>().sprite = spriteToggleY;
			}
			else
			{
				objectsAddone[0].GetComponent<Image>().sprite = spriteToggleN;
			}
		}
		if (option == TypeCaseOption.BackgroundImage || option == TypeCaseOption.CharacterModel)
		{
			if (objectsAddone == null || objectsAddone.Length == 0 || previewSprite == null)
			{
				return;
			}
			Image img = objectsAddone[0].GetComponent<Image>();
			if (img != null)
			{
				SaveRevertState();
				img.sprite = previewSprite;
			}
		}
	}

	public void MouseExit()
	{
		if (option == TypeCaseOption.BackgroundImage || option == TypeCaseOption.CharacterModel)
		{
			if (_revertImages != null && _revertImages.Length > 0)
			{
				RevertToSavedState();
			}
		}
	}

	/// <summary> 设置本选项的 CaseCheck 为选中/未选，并刷新显示（spriteToggleY / spriteToggleN）。 </summary>
	public void SetCaseCheckSelected(bool selected)
	{
		toggleActive = selected ? 1 : 0;
		ApplyCaseCheckVisual();
	}

	/// <summary> 仅刷新 CaseCheck 的图片与样式（根据 toggleActive）。 </summary>
	private void ApplyCaseCheckVisual()
	{
		if (toggleImg == null)
			toggleImg = base.transform.Find("Case/CaseCheck")?.gameObject;
		if (toggleImg == null) return;
		if (toggleActive == 1)
		{
			toggleImg.GetComponent<Image>().sprite = spriteToggleY;
			toggleImg.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 30f);
			if (GetComponent<UI_Colors>() != null)
				GetComponent<UI_Colors>().SetColorImage(toggleImg, new Color(1f, 1f, 1f, 1f));
			else
				toggleImg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
		}
		else
		{
			toggleImg.GetComponent<Image>().sprite = spriteToggleN;
			toggleImg.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
			if (GetComponent<UI_Colors>() != null)
				GetComponent<UI_Colors>().SetColorImage(toggleImg, new Color(0.6f, 0f, 0.6f, 1f));
			else
				toggleImg.GetComponent<Image>().color = new Color(0.6f, 0f, 0.6f, 1f);
		}
	}

	private void UpdateCase()
	{
		if (slider != null)
		{
			slider.value = sliderInt;
		}
		ApplyCaseCheckVisual();
	}
}
