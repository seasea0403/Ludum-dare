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
    [SerializeField] private float  fogScale = 1.3f;       // 雾 Sprite 缩放（相对被覆盖物体）
    [SerializeField] private int    fogSortingOrder = 50; // 确保雾渲染在物体前面

    [Header("消散动画")]
    [SerializeField] private float revealDuration = 0.4f;

    private SpriteRenderer fogRenderer;
    private GameObject fogChild;
    private bool isRevealed;

    /// <summary>雾是否已被脉冲清除</summary>
    public bool IsRevealed => isRevealed;

    /// <summary>代码动态添加时调用，设置雾的 Sprite</summary>
    public void SetFogSprite(Sprite sprite)
    {
        fogSprite = sprite;
        if (fogRenderer != null)
        {
            fogRenderer.sprite = sprite;
        }
    }

    void OnEnable()
    {
        isRevealed = false;
        
        if (fogChild != null)
        {
            Destroy(fogChild);
            fogChild = null;
        }

        CreateFogVisual();
        if (FogManager.Instance)
            FogManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (FogManager.Instance)
            FogManager.Instance.Unregister(this);

        // 恢复所有子物体的透明度，确保池复用正常
        SetAllSpriteRenderersAlpha(1f);
    }

    void OnDestroy()
    {
        // 兜底清理（虽然通常被 OnDisable 处理了）
        if (fogChild != null)
            Destroy(fogChild);
    }

    void CreateFogVisual()
    {
        // 让所有子物体的SpriteRenderer都变透明
        SetAllSpriteRenderersAlpha(0f);

        // 如果有 fogSprite，仍然显示雾的视觉效果
        fogChild = new GameObject("Fog");
        fogChild.transform.SetParent(transform);
        fogChild.transform.localPosition = Vector3.zero;
        fogChild.transform.localScale    = Vector3.one * fogScale;

        fogRenderer = fogChild.AddComponent<SpriteRenderer>();
        fogRenderer.sprite       = fogSprite;
        fogRenderer.color        = fogColor;
        fogRenderer.sortingOrder = fogSortingOrder;
        fogRenderer.sortingLayerName = "Ground";
    }

    // 递归设置所有子物体的SpriteRenderer透明度
    private void SetAllSpriteRenderersAlpha(float alpha)
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            // 不处理雾自身
            if (fogChild != null && sr.gameObject == fogChild) continue;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    /// <summary>被高频脉冲清除时调用</summary>
    public void Reveal()
    {
        if (isRevealed || !gameObject.activeInHierarchy) return;
        isRevealed = true;
        if (AudioManager.Instance) AudioManager.Instance.PlayBubblePop();
        StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        float timer = 0f;
        Color startColor = fogRenderer != null ? fogRenderer.color : Color.white;
        // 记录所有SpriteRenderer初始色
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            startColors[i] = renderers[i].color;

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

            // 物体从透明逐渐恢复可见（所有子物体）
            for (int i = 0; i < renderers.Length; i++)
            {
                if (fogChild != null && renderers[i].gameObject == fogChild) continue;
                Color pc = startColors[i];
                pc.a = Mathf.Lerp(0f, 1f, t);
                renderers[i].color = pc;
            }

            yield return null;
        }

        // 确保所有物体完全可见
        for (int i = 0; i < renderers.Length; i++)
        {
            if (fogChild != null && renderers[i].gameObject == fogChild) continue;
            Color fc = renderers[i].color;
            fc.a = 1f;
            renderers[i].color = fc;
        }

        // 销毁雾的视觉，物体本身保留
        if (fogRenderer != null)
        {
            Destroy(fogRenderer.gameObject);
            fogChild = null;
        }

        if (FogManager.Instance)
            FogManager.Instance.Unregister(this);
    }
}
