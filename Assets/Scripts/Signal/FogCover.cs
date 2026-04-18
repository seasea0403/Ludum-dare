using System.Collections;
using UnityEngine;

/// <summary>
/// 白雾覆盖组件：挂在需要被雾遮住的物体上（Coin / Obstacle）
/// 运行时自动生成白雾子物体；高频脉冲到达后播放消散动画
/// </summary>
public class FogCover : MonoBehaviour
{
    [Header("雾的外观")]
    [SerializeField] private Sprite fogSprite;           // 白色圆形/云形 Sprite（留空则用内置）
    [SerializeField] private Color  fogColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private float  fogScale = 2f;       // 雾 Sprite 缩放（相对被覆盖物体）
    [SerializeField] private int    fogSortingOrder = 50; // 确保雾渲染在物体前面

    [Header("消散动画")]
    [SerializeField] private float revealDuration = 0.4f;

    private SpriteRenderer fogRenderer;
    private bool isRevealed;

    /// <summary>雾是否已被脉冲清除</summary>
    public bool IsRevealed => isRevealed;

    /// <summary>代码动态添加时调用，设置雾的 Sprite</summary>
    public void SetFogSprite(Sprite sprite)
    {
        fogSprite = sprite;
    }

    void Start()
    {
        CreateFogVisual();
        if (FogManager.Instance)
            FogManager.Instance.Register(this);
    }

    void OnDestroy()
    {
        if (FogManager.Instance)
            FogManager.Instance.Unregister(this);
    }

    void CreateFogVisual()
    {
        var fogObj = new GameObject("Fog");
        fogObj.transform.SetParent(transform);
        fogObj.transform.localPosition = Vector3.zero;
        fogObj.transform.localScale    = Vector3.one * fogScale;

        fogRenderer = fogObj.AddComponent<SpriteRenderer>();
        fogRenderer.sprite       = fogSprite;   // 如果为 null，在 Inspector 里拖入
        fogRenderer.color        = fogColor;
        fogRenderer.sortingOrder = fogSortingOrder;
    }

    /// <summary>被高频脉冲清除时调用</summary>
    public void Reveal()
    {
        if (isRevealed) return;
        isRevealed = true;
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        float timer = 0f;
        Color startColor = fogRenderer.color;

        while (timer < revealDuration)
        {
            timer += Time.deltaTime;
            float t = timer / revealDuration;
            // 雾淡出 + 放大消散
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            fogRenderer.color = c;
            fogRenderer.transform.localScale = Vector3.one * fogScale * (1f + t * 0.5f);
            yield return null;
        }

        // 销毁雾的视觉，物体本身保留
        Destroy(fogRenderer.gameObject);

        if (FogManager.Instance)
            FogManager.Instance.Unregister(this);
    }
}
