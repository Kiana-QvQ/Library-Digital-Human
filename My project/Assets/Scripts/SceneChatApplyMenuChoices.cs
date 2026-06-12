// // 在 SceneChat 等场景中应用 SceneMenu 中用户选择的背景、人物模型等信息
// using UnityEngine;
// using UnityEngine.UI;

// public class SceneChatApplyMenuChoices : MonoBehaviour
// {
// 	[Header("背景设置")]
// 	[Tooltip("场景中的背景 Image，将应用菜单选择的 Sprite")]
// 	public Image backgroundImageTarget;
// 	[Tooltip("背景 Sprite 列表，顺序需与 SceneMenu 中 BackgroundImage 选项的 backgroundIndex 对应（0,1,2...）")]
// 	public Sprite[] backgroundSprites;
// 	[Tooltip("使用默认背景时的 Sprite（当 UseDefaultBackgroundAndModel=1 时使用）")]
// 	public Sprite defaultBackgroundSprite;

// 	[Header("人物模型设置")]
// 	[Tooltip("人物模型容器，子物体为不同模型变体，按索引激活对应子物体")]
// 	public Transform characterModelContainer;
// 	[Tooltip("使用默认模型时的子物体索引（当 UseDefaultBackgroundAndModel=1 时使用）")]
// 	public int defaultModelIndex;

// 	private void Start()
// 	{
// 		ApplyBackground();
// 		ApplyCharacterModel();
// 	}

// 	private void ApplyBackground()
// 	{
// 		if (backgroundImageTarget == null) return;
// 		bool useDefault = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1) == 1;
// 		Sprite spriteToApply = null;
// 		if (useDefault && defaultBackgroundSprite != null)
// 		{
// 			spriteToApply = defaultBackgroundSprite;
// 		}
// 		else if (backgroundSprites != null && backgroundSprites.Length > 0)
// 		{
// 			int index = PlayerPrefs.GetInt("MenuBackgroundIndex", 0);
// 			index = Mathf.Clamp(index, 0, backgroundSprites.Length - 1);
// 			spriteToApply = backgroundSprites[index];
// 		}
// 		else
// 		{
// 			return;
// 		}
// 		if (spriteToApply != null)
// 			backgroundImageTarget.sprite = spriteToApply;
// 	}

// 	private void ApplyCharacterModel()
// 	{
// 		if (characterModelContainer == null) return;
// 		int childCount = characterModelContainer.childCount;
// 		if (childCount == 0) return;
// 		bool useDefault = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1) == 1;
// 		int index = useDefault ? defaultModelIndex : PlayerPrefs.GetInt("CharacterModelIndex", 0);
// 		index = Mathf.Clamp(index, 0, childCount - 1);
// 		for (int i = 0; i < childCount; i++)
// 			characterModelContainer.GetChild(i).gameObject.SetActive(i == index);
// 	}
// }
// 在 SceneChat 等场景中应用 SceneMenu 中用户选择的背景、人物模型等信息
using UnityEngine;
using UnityEngine.UI;

public class SceneChatApplyMenuChoices : MonoBehaviour
{
	[Header("背景设置")]
	[Tooltip("场景中的背景 Image，将应用菜单选择的 Sprite")]
	public Image backgroundImageTarget;
	[Tooltip("背景 Sprite 列表，顺序需与 SceneMenu 中 BackgroundImage 选项的 backgroundIndex 对应（0,1,2...）")]
	public Sprite[] backgroundSprites;
	[Tooltip("使用默认背景时的 Sprite（当 UseDefaultBackgroundAndModel=1 时使用）")]
	public Sprite defaultBackgroundSprite;
	[Tooltip("每个背景对应的 Image.type（可选，长度与 backgroundSprites 对应，不填则不改变类型）")]
	public Image.Type[] backgroundImageTypes;
	[Tooltip("每个背景对应的颜色（可选，长度与 backgroundSprites 对应，不填则不改变颜色）")]
	public Color[] backgroundColors;
	[Tooltip("每个背景对应的每单位像素倍率（可选，长度与 backgroundSprites 对应，不填则不改变像素倍率）")]
	public float[] backgroundPixelsPerUnitMultipliers;
	
	[Header("人物模型设置")]
	[Tooltip("人物模型容器，子物体为不同模型变体，按索引激活对应子物体")]
	public Transform characterModelContainer;
	[Tooltip("使用默认模型时的子物体索引（当 UseDefaultBackgroundAndModel=1 时使用）")]
	public int defaultModelIndex;

	[Header("人物语音与嘴型绑定")]
	[Tooltip("用于将当前激活模型的 AudioSource / 嘴型脚本绑定到 VoiceControlManager、Coze 等组件")]
	public CharacterTTSBinder characterTTSBinder;

	private void Start()
	{
		ApplyBackground();
		ApplyCharacterModel();
	}

	private void ApplyBackground()
	{
		if (backgroundImageTarget == null) return;
		bool useDefault = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1) == 1;
		Sprite spriteToApply = null;
		int index = 0;
		if (useDefault && defaultBackgroundSprite != null)
		{
			spriteToApply = defaultBackgroundSprite;
		}
		else if (backgroundSprites != null && backgroundSprites.Length > 0)
		{
			index = PlayerPrefs.GetInt("MenuBackgroundIndex", 0);
			index = Mathf.Clamp(index, 0, backgroundSprites.Length - 1);
			spriteToApply = backgroundSprites[index];
		}
		else
		{
			return;
		}
		if (spriteToApply == null) return;

		// 1) 替换 Sprite
		backgroundImageTarget.sprite = spriteToApply;

		// 日志：记录本次背景应用的配置来源与最终结果，方便测试截图
		Debug.Log(
			$"[SceneChatApplyMenuChoices] ApplyBackground: useDefault={useDefault}, " +
			$"MenuBackgroundIndex={PlayerPrefs.GetInt("MenuBackgroundIndex", 0)}, " +
			$"finalIndex={index}, sprite={spriteToApply.name}",
			this
		);

		// 2) 非默认模式下，可选：按索引覆盖 Image.type
		if (!useDefault && backgroundImageTypes != null && index >= 0 && index < backgroundImageTypes.Length)
		{
			backgroundImageTarget.type = backgroundImageTypes[index];
		}

		// 3) 非默认模式下，可选：按索引覆盖颜色
		if (!useDefault && backgroundColors != null && index >= 0 && index < backgroundColors.Length)
		{
			backgroundImageTarget.color = backgroundColors[index];
		}

		// 4) 非默认模式下，可选：按索引覆盖每单位像素倍率
		if (!useDefault && backgroundPixelsPerUnitMultipliers != null && index >= 0 && index < backgroundPixelsPerUnitMultipliers.Length)
		{
			backgroundImageTarget.pixelsPerUnitMultiplier = backgroundPixelsPerUnitMultipliers[index];
		}
	}

	private void ApplyCharacterModel()
	{
		if (characterModelContainer == null) return;
		int childCount = characterModelContainer.childCount;
		if (childCount == 0) return;
		bool useDefault = PlayerPrefs.GetInt("UseDefaultBackgroundAndModel", 1) == 1;
		int savedIndex = PlayerPrefs.GetInt("CharacterModelIndex", 0);
		int index = useDefault ? defaultModelIndex : savedIndex;
		index = Mathf.Clamp(index, 0, childCount - 1);
		for (int i = 0; i < childCount; i++)
			characterModelContainer.GetChild(i).gameObject.SetActive(i == index);

		// 日志：记录本次人物模型应用的配置来源与最终模型，方便测试截图
		string modelName = characterModelContainer.GetChild(index).name;
		Debug.Log(
			$"[SceneChatApplyMenuChoices] ApplyCharacterModel: useDefault={useDefault}, " +
			$"savedIndex={savedIndex}, finalIndex={index}, model={modelName}",
			this
		);

		// 通知绑定脚本：当前人物模型索引已更新，重绑定 TTS 输出与嘴型控制
		if (characterTTSBinder != null)
		{
			characterTTSBinder.OnCharacterModelChanged(index);
		}
	}

}