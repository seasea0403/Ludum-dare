using System.Collections;
using UnityEngine;

/// <summary>
/// 宝箱：固定位置奖励，碰到后给玩家金币 + 短暂护盾
/// Tag: "Chest"，Collider2D: Trigger
/// 自身只负责视觉和碰撞检测，通过 EventBus 广播
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("奖励")]
    [SerializeField] private int   coinReward    = 5;
    [SerializeField] private float shieldTime    = 3f;

    [Header("视觉")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;

    private SpriteRenderer sr;
    private bool isOpened;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpened) return;
        if (!other.CompareTag("Player")) return;

        isOpened = true;

        // 切换为打开的 Sprite
        if (sr && openedSprite)
            sr.sprite = openedSprite;

        // 广播宝箱事件，传递奖励数据
        var reward = new ChestReward { coins = coinReward, shieldDuration = shieldTime };
        EventBus.Publish(GameEvents.ChestOpened, reward);

        // 延迟销毁，让玩家看到打开效果
        StartCoroutine(DestroyAfterDelay(0.5f));
    }

    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
}

/// <summary>宝箱奖励数据，通过 EventBus 传递</summary>
public class ChestReward
{
    public int   coins;
    public float shieldDuration;
}
