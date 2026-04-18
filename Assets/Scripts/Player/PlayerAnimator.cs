using UnityEngine;

/// <summary>
/// 动画驱动器：读取 PlayerController 和 Rigidbody2D 状态，同步给 Animator
/// 挂在 Player 根节点上（和 PlayerController 同一 GameObject）
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator         anim;
    private PlayerController controller;
    private Rigidbody2D      rb;

    // ── Animator 参数哈希 ──
    private static readonly int HashSpeed      = Animator.StringToHash("Speed");
    private static readonly int HashYVelocity  = Animator.StringToHash("YVelocity");
    private static readonly int HashGrounded   = Animator.StringToHash("Grounded");
    private static readonly int HashJump       = Animator.StringToHash("Jump");
    private static readonly int HashShoot      = Animator.StringToHash("Shoot");
    private static readonly int HashPulse      = Animator.StringToHash("Pulse");
    private static readonly int HashHit        = Animator.StringToHash("Hit");
    private static readonly int HashDead       = Animator.StringToHash("Dead");
    private static readonly int HashSuccess    = Animator.StringToHash("Success");

    private int lastHealth;
    private bool isDead;
    private bool wasGrounded = true;

    void Awake()
    {
        anim       = GetComponent<Animator>();
        controller = GetComponent<PlayerController>();
        rb         = GetComponent<Rigidbody2D>();

        if (!anim)       Debug.LogError("PlayerAnimator: Animator 组件缺失!", this);
        if (!controller) Debug.LogError("PlayerAnimator: PlayerController 组件缺失!", this);
        if (!rb)         Debug.LogError("PlayerAnimator: Rigidbody2D 组件缺失!", this);
    }

    void Start()
    {
        lastHealth = controller.CurrentHealth;
        // 订阅事件
        EventBus.Subscribe(GameEvents.LevelCompleted, OnLevelCompleted);
        EventBus.Subscribe(GameEvents.FrequencyChanged, OnFrequencyChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe(GameEvents.LevelCompleted, OnLevelCompleted);
        EventBus.Unsubscribe(GameEvents.FrequencyChanged, OnFrequencyChanged);
    }

    void Update()
    {
        if (isDead) return;

        // ── 持续同步的参数 ──
        anim.SetFloat(HashSpeed,     Mathf.Abs(rb.velocity.x));
        anim.SetFloat(HashYVelocity, rb.velocity.y);
        anim.SetBool(HashGrounded,   controller.IsGrounded);

        // ── 跳跃（离地瞬间触发一次）──
        if (wasGrounded && !controller.IsGrounded && rb.velocity.y > 0.1f)
        {
            anim.SetTrigger(HashJump);
        }
        wasGrounded = controller.IsGrounded;

        // ── 死亡 ──
        if (controller.CurrentHealth <= 0)
        {
            isDead = true;
            anim.SetBool(HashDead, true);
            return;
        }

        // ── 受伤（血量减少时触发一次）──
        if (controller.CurrentHealth < lastHealth)
        {
            anim.SetTrigger(HashHit);
        }
        lastHealth = controller.CurrentHealth;

        // ── 射击（低频 + 按 Q）──
        if (controller.CurrentFrequency == SignalFrequency.Low
            && Input.GetKeyDown(KeyCode.Q))
        {
            anim.SetTrigger(HashShoot);
        }

        // ── 脉冲（高频 + 按 Q）──
        if (controller.CurrentFrequency == SignalFrequency.High
            && Input.GetKeyDown(KeyCode.Q))
        {
            anim.SetTrigger(HashPulse);
        }
    }

    // ── 高频脉冲动画（由 EventBus 触发也可，此处提供手动调用接口）──
    public void PlayPulse()
    {
        if (!isDead) anim.SetTrigger(HashPulse);
    }

    // ── 通关成功 ──
    private void OnLevelCompleted(object _)
    {
        if (!isDead) anim.SetBool(HashSuccess, true);
    }

    // ── 切换频段时可附加效果 ──
    private void OnFrequencyChanged(object freq)
    {
        // 频段切换的视觉反馈可以在这里加额外逻辑
    }
}
