using UnityEngine;

/// <summary>
/// 最终关的照片泡泡收集物
/// 和 Crown 类似：FogCover 包裹，高频波击破后露出照片
/// 角色碰触自动收集，广播 PhotoCollected 事件
/// </summary>
public class PhotoBubble : MonoBehaviour
{
    [SerializeField] private float bobSpeed  = 1.5f;
    [SerializeField] private float bobHeight = 0.2f;

    private int photoIndex;
    private Vector3 startPos;

    /// <summary>由 FinalLevelController 在生成时调用</summary>
    public void Init(int index, Sprite photoSprite)
    {
        photoIndex = index;
        var sr = GetComponent<SpriteRenderer>();
        if (sr && photoSprite) sr.sprite = photoSprite;
    }

    void OnEnable()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * offset;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 必须先用高频波击破 FogCover 才能收集
        var fog = GetComponent<FogCover>();
        if (fog != null && !fog.IsRevealed) return;

        if (AudioManager.Instance) AudioManager.Instance.PlayBubblePop();
        EventBus.Publish(GameEvents.PhotoCollected, photoIndex);
        Destroy(gameObject);
    }
}
