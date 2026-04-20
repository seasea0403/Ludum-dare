using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 按钮动画交互处理器
/// 鼠标悬停 → 播放循环 Hover 动画
/// 鼠标移出 → 回到 Idle 静止状态
/// 鼠标按下 → 播放一次性 Click 动画，播完后执行回调
/// 
/// Animator 需要包含三个状态：
///   "Idle"  — 静止（默认）
///   "Hover" — 循环动画
///   "Click" — 一次性动画（播放完自动结束）
/// </summary>
public class AnimatedButtonHandler : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Animator animator;
    private Action onClick;
    private bool isClicked;
    private bool isReady;      // Init 完成且强制 Idle 后才接受事件

    // Animator 状态名
    private static readonly int IdleHash  = Animator.StringToHash("Idle");
    private static readonly int HoverHash = Animator.StringToHash("Hover");
    private static readonly int ClickHash = Animator.StringToHash("Click");

    public void Init(Animator anim, Action clickCallback)
    {
        animator = anim;
        onClick  = clickCallback;
        isClicked = false;
        isReady   = false;

        if (animator)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            // 临时禁用组件，强制切断任何默认进入的错误状态，下一帧再开启
            animator.enabled = false;
            StartCoroutine(ForceIdleNextFrame());
        }
        else
        {
            isReady = true;
        }
    }

    void OnEnable()
    {
        isReady = false;
        isClicked = false;
        if (animator != null)
        {
            animator.enabled = false;
            StartCoroutine(ForceIdleNextFrame());
        }
    }

    void OnDisable()
    {
        isClicked = false;
        isReady = false;
    }

    IEnumerator ForceIdleNextFrame()
    {
        yield return null;  // 等一帧再开
        if (animator)
        {
            animator.enabled = true;
            animator.Play(IdleHash, 0, 0f);
            animator.Update(0f);
        }
        isReady = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isReady || isClicked) return;
        if (animator) animator.Play(HoverHash, 0, 0f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isReady || isClicked) return;
        if (animator) animator.Play(IdleHash, 0, 0f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isReady || isClicked) return;
        isClicked = true;

        if (animator)
        {
            animator.Play(ClickHash, 0, 0f);
            StartCoroutine(WaitForClickAnimation());
        }
        else
        {
            onClick?.Invoke();
            isClicked = false;
        }
    }

    IEnumerator WaitForClickAnimation()
    {
        // 等一帧让 Animator 切换到 Click 状态
        yield return null;

        if (animator)
        {
            // 等待 Click 动画播放完毕
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            while (stateInfo.shortNameHash == ClickHash && stateInfo.normalizedTime < 1f)
            {
                yield return null;
                stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            }
        }

        onClick?.Invoke();
        
        // 恢复状态，允许多次点击
        isClicked = false;
        
        // 如果鼠标仍在按钮上，恢复悬停动画，否则空闲
        if (animator) animator.Play(IdleHash, 0, 0f);
    }
}
