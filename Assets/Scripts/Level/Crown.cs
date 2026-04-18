using UnityEngine;

/// <summary>
/// 皇冠：稀有收集物，每关 3 个，代表人生圆满瞬间
/// Tag: "Crown"，Collider2D: Trigger
/// 自身只负责视觉和碰撞检测，通过 EventBus 广播
/// </summary>
public class Crown : MonoBehaviour
{
    [SerializeField] private float bobSpeed  = 1.5f;
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float rotateSpeed = 30f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 上下浮动
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * offset;

        // 缓慢旋转（Z 轴小角度来回摇摆）
        float angle = Mathf.Sin(Time.time * rotateSpeed * Mathf.Deg2Rad) * 10f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        EventBus.Publish(GameEvents.CrownCollected);
        Destroy(gameObject);
    }
}
