using System.Collections;
using UnityEngine;

/// <summary>
/// 关卡 Boss
/// 阶段一：锁定在屏幕右侧（跟随摄像机），定时发射泡泡子弹
/// 阶段二：脱离摄像机变为世界静止，可被红波击破
/// </summary>
public class Boss : MonoBehaviour
{
    [Header("外观")]
    [SerializeField] private Sprite bossSprite;

    [Header("阶段一：射击")]
    [SerializeField] private GameObject bulletPrefab;      // 泡泡子弹预制体
    [SerializeField] private Transform  firePoint;         // 子弹发射点
    [SerializeField] private float firstFireDelay = 3f;    // 出现后多久开始第一射
    [SerializeField] private float fireInterval = 3f;      // 发射间隔
    [SerializeField] private int   totalBullets = 3;       // 总共发射的泡泡数量
    [SerializeField] private float screenRightOffset = 1f; // Boss 距屏幕右边缘的距离

    [Header("阶段二：可被击破")]
    [SerializeField] private float deathAnimDuration = 0.6f;

    private Camera cam;
    private SpriteRenderer sr;
    private Collider2D col;
    private Animator animator;

    private int destroyedBulletCount;
    private int firedBulletCount;
    private bool isPhaseTwo;
    private bool isDying;
    private Coroutine fireRoutine;
    private Sprite fogSprite; // 用于给子弹的 FogCover 设置雾的外观

    /// <summary>由 TerrainChunker 在生成 Boss 后调用，传入 fogSprite</summary>
    public void SetFogSprite(Sprite sprite) => fogSprite = sprite;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        cam = Camera.main;
        destroyedBulletCount = 0;
        firedBulletCount = 0;
        isPhaseTwo = false;
        isDying = false;

        if (sr && bossSprite) sr.sprite = bossSprite;
        if (col) col.enabled = false; // 阶段一不参与碰撞
        if (sr)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }

        Debug.Log($"[Boss] OnEnable  pos={transform.position}  bulletPrefab={(bulletPrefab != null ? bulletPrefab.name : "NULL")}  firePoint={(firePoint != null ? "OK" : "NULL")}");

        // 开始射击
        fireRoutine = StartCoroutine(FireLoop());
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // 阶段一：每帧锁定在屏幕右侧，y 固定为 -1
        if (!isPhaseTwo && !isDying)
        {
            float rightEdge = cam.transform.position.x + cam.orthographicSize * cam.aspect;
            transform.position = new Vector3(
                rightEdge - screenRightOffset,
                -1f,
                0f);
        }
        // 阶段二：不再更新位置，Boss 世界静止，被摄像机甩在后面
    }

    /// <summary>阶段一：发射固定数量的泡泡后进入阶段二</summary>
    private IEnumerator FireLoop()
    {
        yield return new WaitForSeconds(firstFireDelay);

        while (!isPhaseTwo && firedBulletCount < totalBullets)
        {
            FireBullet();
            firedBulletCount++;
            yield return new WaitForSeconds(fireInterval);
        }

        // 所有泡泡发射完毕，进入阶段二
        if (!isPhaseTwo)
            EnterPhaseTwo();
    }

    private void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[Boss] FireBullet: bulletPrefab 为空！无法发射子弹。");
            return;
        }
        Vector3 spawnPos = firePoint ? firePoint.position : transform.position;
        Debug.Log($"[Boss] FireBullet at {spawnPos}");
        var obj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        obj.SetActive(true);

        // 给子弹的 FogCover 设置 fogSprite，否则雾不可见
        var fog = obj.GetComponent<FogCover>();
        if (fog != null && fogSprite != null)
            fog.SetFogSprite(fogSprite);

        var bullet = obj.GetComponent<BossBullet>();
        if (bullet) bullet.Init(this);
    }

    /// <summary>BossBullet 被红波击破时回调（仅计数用）</summary>
    public void OnBulletDestroyed()
    {
        destroyedBulletCount++;
    }

    /// <summary>进入阶段二：脱离摄像机锁定，变为可破坏障碍物</summary>
    private void EnterPhaseTwo()
    {
        isPhaseTwo = true;
        // isPhaseTwo = true 后 LateUpdate 不再锁定位置，Boss 世界静止

        // 停止射击
        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

        // 开启碰撞，让红波可以命中
        if (col) col.enabled = true;
    }

    /// <summary>被红波击破（由 WaveAnimator 调用）</summary>
    public void Shatter()
    {
        if (!isPhaseTwo || isDying) return;
        isDying = true;

        if (col) col.enabled = false;
        if (AudioManager.Instance) AudioManager.Instance.PlayObstacleShatter();

        if (animator)
        {
            animator.enabled = true;
            animator.SetTrigger("Destroy");
        }

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathAnimDuration);

        // 恢复物体生成
        var chunker = FindObjectOfType<TerrainChunker>();
        if (chunker) chunker.SetSpawnPaused(false);

        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDying) return;
        var player = collision.collider.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage();
    }
}
