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
        // 直接让被覆盖物体透明（不依赖雾 Sprite 遮挡）
        SpriteRenderer parentSR = GetComponent<SpriteRenderer>();
        if (parentSR != null)
        {
            Color c = parentSR.color;
            c.a = 0f;
            parentSR.color = c;
        }

        // 如果有 fogSprite，仍然显示雾的视觉效果
        var fogObj = new GameObject("Fog");
        fogObj.transform.SetParent(transform);
        fogObj.transform.localPosition = Vector3.zero;
        fogObj.transform.localScale    = Vector3.one * fogScale;

        fogRenderer = fogObj.AddComponent<SpriteRenderer>();
        fogRenderer.sprite       = fogSprite;
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
        Color startColor = fogRenderer != null ? fogRenderer.color : Color.white;
        SpriteRenderer parentSR = GetComponent<SpriteRenderer>();

        while (timer < revealDuration)
        {
            timer += Time.deltaTime;
            float t = timer / revealDuration;

            // 雾淡出 + 放大消散
            if (fogRenderer != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                fogRenderer.color = c;
                fogRenderer.transform.localScale = Vector3.one * fogScale * (1f + t * 0.5f);
            }

            // 物体从透明逐渐恢复可见
            if (parentSR != null)
            {
                Color pc = parentSR.color;
                pc.a = Mathf.Lerp(0f, 1f, t);
                parentSR.color = pc;
            }

            yield return null;
        }

        // 确保物体完全可见
        if (parentSR != null)
        {
            Color fc = parentSR.color;
            fc.a = 1f;
            parentSR.color = fc;
        }

        // 销毁雾的视觉，物体本身保留
        if (fogRenderer != null)
            Destroy(fogRenderer.gameObject);

        if (FogManager.Instance)
            FogManager.Instance.Unregister(this);
    }
}
