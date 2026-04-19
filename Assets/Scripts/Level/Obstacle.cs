using System.Collections;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Random Appearance")]
    [SerializeField] private Sprite[] variants;

    [Header("Indestructible")]
    [SerializeField] private bool isIndestructible;

    [Header("Destruction Animation")]
    [SerializeField] private float destructionAnimationDuration = 0.6f;

    private SpriteRenderer sr;
    private Collider2D col;
    private Animator animator;
    private bool isDestroying;

    /// <summary>是否为不可摧毁障碍物</summary>
    public bool IsIndestructible => isIndestructible;

    /// <summary>运行时设置外观 Sprite 列表（由 LevelManager/TerrainChunker 调用）</summary>
    public void SetVariants(Sprite[] sprites)
    {
        variants = sprites;
        // 立即刷新显示，因为 OnEnable 可能已经用旧 variants 设过 sprite
        if (sr != null && variants != null && variants.Length > 0)
            sr.sprite = variants[Random.Range(0, variants.Length)];
    }

    /// <summary>运行时设置是否可摧毁</summary>
    public void SetIndestructible(bool value)
    {
        isIndestructible = value;
    }

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        isDestroying = false;

        // 禁用Animator，防止在Idle状态下Animator锁定覆盖我们动态设置的Sprite
        if (animator) animator.enabled = false;

        if (sr)
        {
            sr.enabled = true;
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
        if (col) col.enabled = true;

        // 清理池复用时可能残留的雾子物体
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "Fog")
                DestroyImmediate(child.gameObject);
        }

        // Random sprite each time
        if (sr != null && variants != null && variants.Length > 0)
            sr.sprite = variants[Random.Range(0, variants.Length)];
    }

    /// <summary>Called by laser — play destruction animation</summary>
    public void Shatter()
    {
        if (isDestroying) return;
        if (isIndestructible) return;  // 不可摧毁障碍物无法被击碎
        
        isDestroying = true;
        
        // Disable collider so obstacle won't hurt the player during destruction
        if (col) col.enabled = false;
        
        // Play destruction animation
        if (animator)
        {
            animator.enabled = true; // 启用Animator以播放动画
            animator.SetTrigger("Destroy");
        }
        
        // Wait for animation to finish, then destroy
        StartCoroutine(WaitForDestructionAnimation());
    }

    /// <summary>Called by bullet (legacy) — just return to pool</summary>
    public void onDestr()
    {
        if (ObjectPool.Instance)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    IEnumerator WaitForDestructionAnimation()
    {
        // If animator exists, wait for the destroy animation to finish
        if (animator)
        {
            yield return new WaitForSeconds(destructionAnimationDuration);
        }
        else
        {
            // Fallback if no animator
            yield return new WaitForSeconds(0.1f);
        }
        
        // Return to pool or destroy
        if (ObjectPool.Instance)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }


    void OnCollisionEnter2D(Collision2D collision)
    {
        // Don't damage if currently being destroyed
        if (isDestroying) return;
        
        PlayerController player = collision.collider.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Don't damage if currently being destroyed
        if (isDestroying) return;
        
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage();
    }
}
