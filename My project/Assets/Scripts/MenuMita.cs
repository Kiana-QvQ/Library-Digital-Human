// based on the original game.Yen Chezky(yenichw)
using RootMotion.FinalIK;
using UnityEngine;

public class MenuMita : MonoBehaviour
{
	public Animator mitaAnim;

	public Transform cameraT;

	public Transform headIk;

	public Transform eyesIk;

	public ButtonMouseMenu changeMenu;

	public AnimationCurve animationActive;

	private float weightActive;

	public MenuMitaDance mitaDance;

	public float intensityJumpHeadDance;

	public AnimationCurve headDance;

	public float timeDance;

	private bool active;

	private float lookEyesOnCamera;

	private float timeRandomlookOnCamera;

	private float timeBlnk;

	private bool alternative;

	private float timeStopLookOnCamera;

	private static readonly int BlinkTrigger = Animator.StringToHash("Blink");
	private bool hasBlinkTrigger;

	private void Awake()
	{
		if (mitaAnim == null)
		{
			mitaAnim = GetComponent<Animator>();
			if (mitaAnim == null)
			{
				mitaAnim = GetComponentInChildren<Animator>(true);
			}
			if (mitaAnim == null)
			{
				mitaAnim = GetComponentInParent<Animator>();
			}
			if (mitaAnim == null)
			{
				// Menu 场景里脚本可能挂在 UI/Manager 上，不在角色层级下。
				// 这里尽量自动找到带 Blink Trigger 的 Animator，减少手动拖引用。
				var animators = FindObjectsOfType<Animator>(true);
				for (var i = 0; i < animators.Length; i++)
				{
					var a = animators[i];
					if (HasTrigger(a, "Blink"))
					{
						mitaAnim = a;
						break;
					}
				}
				if (mitaAnim == null && animators.Length > 0)
				{
					mitaAnim = animators[0];
				}
			}
		}

		if (mitaAnim != null)
		{
			// 只做一次参数存在性检查，避免运行时“触发了但没效果”难排查
			hasBlinkTrigger = HasTrigger(mitaAnim, "Blink");
			if (!hasBlinkTrigger)
			{
				Debug.LogWarning($"{nameof(MenuMita)}: Animator 上不存在 Trigger 参数 'Blink'，Blink() 将不会生效。", this);
			}
		}
	}

	private void Start()
	{
		timeRandomlookOnCamera = Random.Range(10f, 40f);
		timeBlnk = 10f;
	}

	private void Update()
	{
		if (active)
		{
			if (weightActive < 1f)
			{
				weightActive += Time.deltaTime * 0.7f;
				if (weightActive > 1f)
				{
					weightActive = 1f;
				}
				headIk.GetComponent<LookAtIK>().solver.SetIKPositionWeight(animationActive.Evaluate(weightActive));
				eyesIk.GetComponent<LookAtIK>().solver.SetIKPositionWeight(animationActive.Evaluate(weightActive));
			}
		}
		else if (weightActive > 0f)
		{
			weightActive -= Time.deltaTime * 2f;
			if (weightActive < 0f)
			{
				weightActive = 0f;
			}
			headIk.GetComponent<LookAtIK>().solver.SetIKPositionWeight(animationActive.Evaluate(weightActive));
			eyesIk.GetComponent<LookAtIK>().solver.SetIKPositionWeight(animationActive.Evaluate(weightActive));
		}
		timeRandomlookOnCamera -= Time.deltaTime;
		if (timeRandomlookOnCamera < 0f)
		{
			if (alternative)
			{
				timeStopLookOnCamera = Random.Range(0.15f, 1f);
				timeRandomlookOnCamera = Random.Range(5f, 15f);
			}
			else
			{
				LookEyesOnCamera();
			}
		}
		if (lookEyesOnCamera > 0f)
		{
			lookEyesOnCamera -= Time.deltaTime;
			if (lookEyesOnCamera < 0f)
			{
				lookEyesOnCamera = 0f;
			}
			if (lookEyesOnCamera > 0.2f)
			{
				eyesIk.position = cameraT.position;
			}
		}
		if (lookEyesOnCamera == 0f)
		{
			eyesIk.position = changeMenu.caseChangeNow.transform.position;
			timeDance += Time.deltaTime;
			if (timeDance > 1f)
			{
				timeDance -= 1f;
			}
			headIk.position = Vector3.Lerp(headIk.position, changeMenu.caseChangeNow.transform.position + Vector3.up * (headDance.Evaluate(timeDance) * (mitaDance.lerpJump * intensityJumpHeadDance)), Time.deltaTime);
		}
		else if (!alternative)
		{
			headIk.position = Vector3.Lerp(headIk.position, Vector3.MoveTowards(changeMenu.caseChangeNow.transform.position, cameraT.position, 0.2f), Time.deltaTime);
		}
		else
		{
			timeDance += Time.deltaTime;
			if (timeDance > 1f)
			{
				timeDance -= 1f;
			}
			headIk.position = Vector3.Lerp(headIk.position, Vector3.MoveTowards(changeMenu.caseChangeNow.transform.position, cameraT.position + Vector3.up * (headDance.Evaluate(timeDance) * (mitaDance.lerpJump * intensityJumpHeadDance)), 0.2f), Time.deltaTime);
		}
		headIk.position = new Vector3(headIk.position.x, Mathf.Clamp(headIk.position.y, 1.55f, 1.72f), headIk.position.z);
		eyesIk.position = new Vector3(eyesIk.position.x, Mathf.Clamp(eyesIk.position.y, 1.55f, 1.72f), eyesIk.position.z);
		if (timeBlnk > 0f)
		{
			timeBlnk -= Time.deltaTime;
			if (timeBlnk < 0f)
			{
				Blink();
			}
		}
		if (timeStopLookOnCamera > 0f)
		{
			lookEyesOnCamera = 0f;
			timeStopLookOnCamera -= Time.deltaTime;
			if (timeStopLookOnCamera < 0f)
			{
				timeStopLookOnCamera = 0f;
			}
		}
		if (alternative && timeStopLookOnCamera == 0f)
		{
			lookEyesOnCamera = 1f;
		}
	}

	private void LookEyesOnCamera()
	{
		lookEyesOnCamera = Random.Range(0.4f, 0.6f);
		if (!alternative)
		{
			timeRandomlookOnCamera = Random.Range(10f, 40f);
		}
		else
		{
			timeRandomlookOnCamera = Random.Range(2f, 10f);
		}
	}

	public void StartActive()
	{
		active = true;
		headIk.position = cameraT.position;
		eyesIk.position = cameraT.position;
	}

	public void Alternative()
	{
		alternative = true;
		timeRandomlookOnCamera = Random.Range(2f, 10f);
		intensityJumpHeadDance = 4f;
	}

	public void Activation(bool x)
	{
		active = false;
	}

	private void Blink()
	{
		if (mitaAnim == null)
		{
			Debug.LogError($"{nameof(MenuMita)}: mitaAnim 未赋值，无法触发眨眼。请在 Inspector 赋值或确保物体/子物体上有 Animator。", this);
			timeBlnk = Random.Range(0.5f, 10f);
			return;
		}

		if (hasBlinkTrigger)
		{
			mitaAnim.SetTrigger(BlinkTrigger);
		}
		timeBlnk = Random.Range(0.5f, 10f);
	}

	private static bool HasTrigger(Animator animator, string name)
	{
		if (animator == null) return false;
		var parameters = animator.parameters;
		for (var i = 0; i < parameters.Length; i++)
		{
			var p = parameters[i];
			if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
			{
				return true;
			}
		}
		return false;
	}
}
