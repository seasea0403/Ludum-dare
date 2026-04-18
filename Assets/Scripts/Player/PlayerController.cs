using System.Collections;
using UnityEngine;

public enum SignalFrequency { High, Low }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("自动移动")]
    [SerializeField] private float moveSpeed = 5f;

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
    [SerializeField] private GameObject signalWavePrefab;

    [Header("射击（激光）")]
    [SerializeField] private Transform  firePoint;
    [SerializeField] private float      fireRate = 0.3f;

    private LaserBeam laserBeam;

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

    void Awake()
    {
        rb            = GetComponent<Rigidbody2D>();
        sr            = GetComponentInChildren<SpriteRenderer>();
        fogManager    = FindObjectOfType<FogManager>();
        laserBeam     = GetComponentInChildren<LaserBeam>();
        if (laserBeam == null)
        {
            var lbObj = new GameObject("LaserBeam");
            lbObj.transform.SetParent(transform);
            lbObj.transform.localPosition = Vector3.zero;
            laserBeam = lbObj.AddComponent<LaserBeam>();
        }
        CurrentHealth = maxHealth;
        startX        = transform.position.x;

        // 确保物理引擎在 transform 移动后自动同步碰撞体
        Physics2D.autoSyncTransforms = true;
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
        CheckGround();
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
        rb.velocity = new Vector2(moveSpeed, rb.velocity.y);
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
        }
    }

    // ───── E 切换频段 ─────
    void HandleFrequencySwitch()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        CurrentFrequency = CurrentFrequency == SignalFrequency.High
            ? SignalFrequency.Low
            : SignalFrequency.High;

        EventBus.Publish(GameEvents.FrequencyChanged, CurrentFrequency);
    }

    // ───── 高频：Q 发脉冲清除白雾 + 生成信号波动画 ─────
    void HandleHighFrequency()
    {
        if (Input.GetKeyDown(KeyCode.Q) && fireTimer <= 0f)
        {
            fireTimer = fireRate;
            if (fogManager) fogManager.EmitPulse(transform.position, signalRadius);
            if (signalWavePrefab)
            {
                var wave = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(signalWavePrefab, transform.position, Quaternion.identity)
                    : Instantiate(signalWavePrefab, transform.position, Quaternion.identity);
                var sw = wave.GetComponent<SignalWave>();
                if (sw) sw.Init(signalRadius);
            }
        }
    }

    // ───── 低频：Q 发射激光 ─────
    void HandleLowFrequency()
    {
        if (Input.GetKeyDown(KeyCode.Q) && fireTimer <= 0f)
        {
            Vector3 origin = firePoint ? firePoint.position : transform.position;
            if (laserBeam && laserBeam.Fire(origin))
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

    public void TakeDamage()
    {
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
