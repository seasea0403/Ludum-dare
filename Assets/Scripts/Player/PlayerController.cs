using System.Collections;
using UnityEngine;

public enum SignalFrequency { High, Low }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("自动移动")]
    [SerializeField]  private float moveSpeed = 5f;

    [Header("跳跃")]
    [SerializeField] private float     jumpForce          = 11f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float     groundCheckRadius  = 0.3f;
    [SerializeField] private int       maxJumpCount       = 2;
    [SerializeField] private float     fallMultiplier     = 2.5f;
    [SerializeField] private float     lowJumpMultiplier  = 2f;

    [Header("信号")]
    [SerializeField] private float signalRadius   = 8f;

    [Header("射击")]
    [SerializeField] private Transform  firePoint;
    [SerializeField] private float      fireRate = 0.3f;

    private WaveAnimator waveAnimator;

    [Header("血量")]
    [SerializeField] private int   maxHealth       = 3;
    [SerializeField] private float invincibleTime  = 2f;

    public SignalFrequency CurrentFrequency { get; private set; } = SignalFrequency.High;
    public int   CurrentHealth { get; private set; }
    public int   CoinCount     { get; private set; }
    public int   CrownCount    { get; private set; }
    public float Distance      { get; private set; }
    public bool  IsGrounded    { get; private set; }
    public bool  HasShield     { get; private set; }

    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private FogManager     fogManager;
    private int   jumpCount;
    private float signalTimer;
    private float fireTimer;
    private bool  isInvincible;
    private float startX;
    private Coroutine shieldRoutine;
    private Vector3 spawnPosition;
    private float timer = 0f;

    /// <summary>最终关模式：手动 A/D 移动，无伤害</summary>
    [HideInInspector] public bool isFinalLevel;

    /// <summary>教学关暂停：冻结移动和输入，由 TutorialController 控制</summary>
    [HideInInspector] public bool isTutorialPaused;

    /// <summary>教学关启用后，仅在引导暂停时允许指定按键生效</summary>
    [HideInInspector] public bool isTutorialInputRestricted;

    void Awake()
    {
        rb            = GetComponent<Rigidbody2D>();
        sr            = GetComponentInChildren<SpriteRenderer>();
        fogManager    = FindObjectOfType<FogManager>();
        waveAnimator  = FindObjectOfType<WaveAnimator>();
        CurrentHealth = maxHealth;
        startX        = transform.position.x;
        spawnPosition = transform.position;

        // 确保物理引擎在 transform 移动后自动同步碰撞体
        Physics2D.autoSyncTransforms = true;
    }

    /// <summary>重置玩家状态到出生点（关卡重试/下一关时调用）</summary>
    public void ResetState()
    {
        StopAllCoroutines();
        transform.position = spawnPosition;
        if (rb) rb.velocity = Vector2.zero;
        CurrentHealth  = maxHealth;
        CoinCount      = 0;
        CrownCount     = 0;
        Distance       = 0;
        startX         = spawnPosition.x;
        isInvincible   = false;
        HasShield      = false;
        shieldRoutine  = null;
        jumpCount      = 0;
        fireTimer      = 0;
        isTutorialPaused = false;
        isTutorialInputRestricted = false;

        // 每次重置都恢复到低频（红波/attack），与 WeaponSwitchUI 初始状态保持一致
        CurrentFrequency = SignalFrequency.Low;

        if (sr) sr.color = Color.white;
        enabled = true;

        // 同步 UI
        EventBus.Publish(GameEvents.PlayerHit,        CurrentHealth);
        EventBus.Publish(GameEvents.CoinCollected,    CoinCount);
        EventBus.Publish(GameEvents.CrownCountChanged, CrownCount);
        // FrequencyChanged 由 LevelManager.BeginLevel 中统一调用 WeaponSwitchUI.ForceReset() 处理，避免触发动画
        EventBus.Publish(GameEvents.ShieldBroken);
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.ChestOpened, OnChestOpened);
        EventBus.Subscribe(GameEvents.CrownCollected, OnCrownCollected);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.ChestOpened, OnChestOpened);
        EventBus.Unsubscribe(GameEvents.CrownCollected, OnCrownCollected);
    }

    void Update()
    {
        if (isTutorialPaused) return;

        timer += Time.deltaTime;
        if(timer>20f) moveSpeed += Time.deltaTime * 0.018f;
        CheckGround();

        if (isTutorialInputRestricted)
        {
            UpdateDistance();
            fireTimer -= Time.deltaTime;
            return;
        }

        HandleJump();
        HandleFrequencySwitch();

        if (CurrentFrequency == SignalFrequency.High)
            HandleHighFrequency();
        else
            HandleLowFrequency();

        UpdateDistance();
        fireTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (isTutorialPaused)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            ApplyBetterGravity();
            return;
        }

        if (isFinalLevel)
        {
            float h = Input.GetAxisRaw("Horizontal");
            rb.velocity = new Vector2(h * moveSpeed, rb.velocity.y);

            // 限制活动范围 -10 ~ 80
            Vector3 pos = transform.position;
            if (pos.x < -10f) { pos.x = -10f; transform.position = pos; rb.velocity = new Vector2(0, rb.velocity.y); }
            else if (pos.x > 80f) { pos.x = 80f; transform.position = pos; rb.velocity = new Vector2(0, rb.velocity.y); }
        }
        else
        {
            rb.velocity = new Vector2(moveSpeed, rb.velocity.y);
        }
        ApplyBetterGravity();
    }

    void ApplyBetterGravity()
    {
        if (rb.velocity.y < 0f)
            rb.velocity += Vector2.up * Physics2D.gravity.y
                               * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        else if (rb.velocity.y > 0f && !Input.GetButton("Jump"))
            rb.velocity += Vector2.up * Physics2D.gravity.y
                               * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
    }

    void CheckGround()
    {
        bool was = IsGrounded;
        // 从 groundCheck 位置向下发射短射线，检测是否碰到 Ground Layer
        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckRadius, groundLayer);
        IsGrounded = hit.collider != null;

        if (!was && IsGrounded) jumpCount = 0;

        // 调试：在 Console 里确认检测状态（确认没问题后可以删掉这行）
        if (was && !IsGrounded)
            Debug.Log($"[GroundCheck] 离地! jumpCount={jumpCount} pos={groundCheck.position}");
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && (IsGrounded || jumpCount < maxJumpCount))
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpCount++;
            if (AudioManager.Instance) AudioManager.Instance.PlayJump();
        }
    }

    // ───── E 切换频段 ─────
    void HandleFrequencySwitch()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        CurrentFrequency = CurrentFrequency == SignalFrequency.High
            ? SignalFrequency.Low
            : SignalFrequency.High;

        // 切换频段时立即重置冷却，避免切换后第一次攻击被延迟
        fireTimer = 0f;

        EventBus.Publish(GameEvents.FrequencyChanged, CurrentFrequency);
    }

    // ───── 高频：Q 发脉冲清除白雾 ─────
    void HandleHighFrequency()
    {
        if (Input.GetKeyDown(KeyCode.Q) && fireTimer <= 0f)
        {
            fireTimer = fireRate;
            if (waveAnimator)
                waveAnimator.Pulse(transform.position);
            else if (fogManager)
                fogManager.EmitPulse(transform.position, signalRadius);
        }
    }

    // ───── 低频：Q 攻击波 ─────
    void HandleLowFrequency()
    {
        if (Input.GetKeyDown(KeyCode.Q) && fireTimer <= 0f)
        {
            Vector3 origin = firePoint ? firePoint.position : transform.position;
            if (waveAnimator && waveAnimator.Shoot(origin))
            {
                fireTimer = fireRate;
            }
        }
    }

    void UpdateDistance()
    {
        Distance = transform.position.x - startX;
        EventBus.Publish(GameEvents.DistanceUpdated, Distance);
    }

    public void CollectCoin()
    {
        CoinCount++;
        EventBus.Publish(GameEvents.CoinCollected, CoinCount);
    }

    // ───── 教学关强制操作（绕过 isTutorialPaused 检查）─────

    /// <summary>教学关强制切换频段</summary>
    public void TutorialForceSwitch()
    {
        CurrentFrequency = CurrentFrequency == SignalFrequency.High
            ? SignalFrequency.Low : SignalFrequency.High;
        fireTimer = 0f;
        EventBus.Publish(GameEvents.FrequencyChanged, CurrentFrequency);
    }

    /// <summary>教学关强制发射高频探测波</summary>
    public void TutorialForceSignal()
    {
        if (waveAnimator)
            waveAnimator.Pulse(transform.position);
        else if (fogManager)
            fogManager.EmitPulse(transform.position, signalRadius);
    }

    /// <summary>教学关强制发射低频攻击波</summary>
    public void TutorialForceAttack()
    {
        Vector3 origin = firePoint ? firePoint.position : transform.position;
        if (waveAnimator) waveAnimator.Shoot(origin);
    }

    /// <summary>教学关强制跳跃</summary>
    public void TutorialForceJump()
    {
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpCount++;
        if (AudioManager.Instance) AudioManager.Instance.PlayJump();
    }

    public void TakeDamage()
    {
        if (isFinalLevel) return; // 最终关无伤害
        if (isInvincible || HasShield) 
        {
            if (HasShield)
            {
                HasShield = false;
                if (shieldRoutine != null) StopCoroutine(shieldRoutine);
                EventBus.Publish(GameEvents.ShieldBroken);
            }
            return;
        }
        CurrentHealth--;
        EventBus.Publish(GameEvents.PlayerHit, CurrentHealth);
        if (RunnerCamera.Instance) RunnerCamera.Instance.Shake();

        if (CurrentHealth <= 0)
        {
            EventBus.Publish(GameEvents.PlayerDied);
            return;
        }
        StartCoroutine(InvincibleRoutine());
    }

    IEnumerator InvincibleRoutine()
    {
        isInvincible = true;
        // 半透明表示无敌状态
        Color c = sr.color;
        Color transparent = new Color(c.r, c.g, c.b, 0.35f);
        sr.color = transparent;

        yield return new WaitForSeconds(invincibleTime);

        sr.color = new Color(c.r, c.g, c.b, 1f);
        isInvincible = false;
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Obstacle"))
            TakeDamage();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 被雾覆盖的物体：触碰即扣血，不触发正常逻辑
        var fog = other.GetComponent<FogCover>();
        if (fog != null && !fog.IsRevealed)
        {
            if (!isFinalLevel)
            {
                TakeDamage();
                other.gameObject.SetActive(false);
            }
            return;
        }

        if (other.CompareTag("Coin"))
        {
            CollectCoin();
            if (ObjectPool.Instance)
                ObjectPool.Instance.Return(other.gameObject);
            else
                Destroy(other.gameObject);
        }
        // LevelEnd 由 LevelEndTrigger 脚本独立广播事件，这里不重复发布
        // Crown 和 Chest 自己处理碰撞并广播事件，PlayerController 通过 EventBus 监听
    }

    // ───── EventBus 回调（解耦）─────
    private void OnChestOpened(object data)
    {
        var reward = data as ChestReward;
        if (reward == null) return;

        // 加金币
        for (int i = 0; i < reward.coins; i++)
            CollectCoin();

        // 启动护盾
        if (shieldRoutine != null) StopCoroutine(shieldRoutine);
        shieldRoutine = StartCoroutine(ShieldRoutine(reward.shieldDuration));
    }

    private void OnCrownCollected(object data)
    {
        CrownCount++;
        EventBus.Publish(GameEvents.CrownCountChanged, CrownCount);
    }

    IEnumerator ShieldRoutine(float duration)
    {
        HasShield = true;
        EventBus.Publish(GameEvents.ShieldActivated, duration);
        yield return new WaitForSeconds(duration);
        HasShield = false;
        EventBus.Publish(GameEvents.ShieldBroken);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        // 画射线方向和长度
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckRadius);
        Gizmos.DrawWireSphere(groundCheck.position + Vector3.down * groundCheckRadius, 0.05f);
    }
}
