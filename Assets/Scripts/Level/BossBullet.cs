using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 发射的泡泡子弹
/// 外层 FogCover 泡泡包裹内部子弹
/// 蓝波击破泡泡后子弹加速暴露，可被跳跃躲避或红波击破
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class BossBullet : MonoBehaviour
{
    [Header("速度")]
    public float slowSpeed = 3f;    // 泡泡状态的飞行速度
    public float fastSpeed = 10f;   // 暴露后的加速飞行速度

    [Header("泡泡外观")]
    public float bubbleScale = 2f;  // 泡泡缩放

    [Header("销毁")]
    [SerializeField] private float lifeTime = 10f;    // 超时自动销毁

    private bool revealed;
    private float speed;
    private float timer;
    private FogCover fogCover;
    private Boss owner;

    /// <summary>泡泡是否已被击破</summary>
    public bool IsRevealed => revealed;

    /// <summary>初始化（由 Boss 调用）</summary>
    public void Init(Boss boss)
    {
        owner = boss;
        revealed = false;
        speed = slowSpeed;
        timer = 0f;

        // 应用泡泡缩放
        transform.localScale = Vector3.one * bubbleScale;

        // FogCover 在 OnEnable 中自动初始化
        fogCover = GetComponent<FogCover>();

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = true;

        // 不要重置 alpha，FogCover.OnEnable 已经把子弹 sprite 设为透明
        // 玩家只能看到泡泡（雾），蓝波击破后才显示子弹
    }

    void Update()
    {
        // 向左飞行
        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        timer += Time.deltaTime;
        if (timer > lifeTime)
        {
            DestroySelf();
            return;
        }

        // 检测 FogCover 是否被蓝波击破
        if (!revealed && fogCover != null && fogCover.IsRevealed)
        {
            OnFogRevealed();
        }
    }

    /// <summary>泡泡被蓝波击破后，子弹暴露并加速</summary>
    private void OnFogRevealed()
    {
        revealed = true;
        speed = fastSpeed;
    }

    /// <summary>被红波击破（和障碍物同逻辑）</summary>
    public void Shatter()
    {
        if (!revealed) return; // 只有暴露后才能被红波击破

        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        if (AudioManager.Instance) AudioManager.Instance.PlayObstacleShatter();

        // 通知 Boss 计数
        if (owner) owner.OnBulletDestroyed();

        DestroySelf();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 碰到玩家扣血
        var player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage();
            DestroySelf();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        var player = collision.collider.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeDamage();
            DestroySelf();
        }
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
