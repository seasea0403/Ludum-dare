using UnityEngine;

/// <summary>
/// 挂在猫预制体上，玩家碰触后触发结局白屏渐变 + 结局面板
/// </summary>
public class CatTrigger : MonoBehaviour
{
    private bool triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        // 禁用玩家移动
        var player = other.GetComponent<PlayerController>();
        if (player)
        {
            player.enabled = false;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb) rb.velocity = Vector2.zero;
        }

        // 触发结局 UI 白屏渐变
        if (EndingUI.Instance)
            EndingUI.Instance.StartEnding();
    }
}
