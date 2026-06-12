using UnityEngine;

/// <summary>
/// 动画控制器
/// 负责管理角色动画状态的切换
/// </summary>
public class AnimationController : MonoBehaviour
{
    [Header("动画控制")]
    [Tooltip("角色Animator组件，用于控制动画状态转换")]
    [SerializeField] private Animator characterAnimator;
    
    [Tooltip("Animator参数名称（用于控制动画状态）")]
    [SerializeField] private string animatorStateParameter = "state";

    // 事件：动画状态变化时触发
    public System.Action<int, string> OnAnimationStateChanged; // state, stateName

    /// <summary>
    /// 初始化动画控制器
    /// </summary>
    /// <param name="animator">Animator组件，如果为null则自动查找</param>
    public void Initialize(Animator animator = null)
    {
        if (animator != null)
        {
            characterAnimator = animator;
        }
        else if (characterAnimator == null)
        {
            // 如果没有手动指定Animator，尝试从当前GameObject或子对象中查找
            characterAnimator = GetComponent<Animator>();
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }
        }

        // 初始化动画状态为待机（state = 0）
        SetAnimationState(0);
    }

    /// <summary>
    /// 设置动画状态
    /// </summary>
    /// <param name="state">动画状态值：0=待机, 1=思考, 2=回答动作</param>
    public void SetAnimationState(int state)
    {
        if (characterAnimator != null)
        {
            characterAnimator.SetInteger(animatorStateParameter, state);
            string stateName = GetStateName(state);
            Debug.Log($"动画状态切换: {stateName} (state = {state})");
            
            // 触发事件
            OnAnimationStateChanged?.Invoke(state, stateName);
        }
        else
        {
            Debug.LogWarning("Animator组件未设置，无法控制动画状态");
        }
    }

    /// <summary>
    /// 获取状态名称（用于日志）
    /// </summary>
    private string GetStateName(int state)
    {
        switch (state)
        {
            case 0: return "待机";
            case 1: return "思考";
            case 2: return "回答动作";
            default: return $"未知状态({state})";
        }
    }

    /// <summary>
    /// 获取当前Animator组件
    /// </summary>
    public Animator GetAnimator()
    {
        return characterAnimator;
    }

    /// <summary>
    /// 获取当前动画状态参数名称
    /// </summary>
    public string GetStateParameterName()
    {
        return animatorStateParameter;
    }
}

