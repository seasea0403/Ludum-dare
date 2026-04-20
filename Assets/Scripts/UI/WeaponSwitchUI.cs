using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 挂在 shift 节点上，监听频段切换事件，驱动 Front / Back 面板互换位置 + 明暗变化
/// </summary>
public class WeaponSwitchUI : MonoBehaviour
{
    [Header("面板引用")]
    [SerializeField] private RectTransform frontPanel;   // 初始在前面的面板（attack）
    [SerializeField] private RectTransform backPanel;    // 初始在后面的面板（scan）

    [Header("面板背景 Image（用于调亮/暗）")]
    [SerializeField] private Image frontBg;
    [SerializeField] private Image backBg;

    [Header("动画参数")]
    [SerializeField] private float swapDuration = 0.25f;
    [SerializeField] private Color brightColor = Color.white;
    [SerializeField] private Color dimColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private Coroutine swapCoroutine;

    // 保存原始引用和位置，用于强制复位
    private RectTransform originalFrontPanel;
    private RectTransform originalBackPanel;
    private Image         originalFrontBg;
    private Image         originalBackBg;
    private Vector2       originalFrontPos;
    private Vector2       originalBackPos;
    private bool          hasInitializedPos = false;

    void Awake()
    {
        originalFrontPanel = frontPanel;
        originalBackPanel  = backPanel;
        originalFrontBg    = frontBg;
        originalBackBg     = backBg;

        // 尽早记录锚点，避免有些位置恰好为 (0,0) 时被忽略
        if (originalFrontPanel != null) originalFrontPos = originalFrontPanel.anchoredPosition;
        if (originalBackPanel != null)  originalBackPos  = originalBackPanel.anchoredPosition;
        hasInitializedPos = true;
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.FrequencyChanged, OnFrequencyChanged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.FrequencyChanged, OnFrequencyChanged);
    }

    void Start()
    {
        ForceReset();
    }

    /// <summary>强制将 UI 恢复到初始状态（front=红波在前），不播放动画</summary>
    public void ForceReset()
    {
        if (swapCoroutine != null)
        {
            StopCoroutine(swapCoroutine);
            swapCoroutine = null;
        }

        // 恢复原始引用
        frontPanel = originalFrontPanel;
        backPanel  = originalBackPanel;
        frontBg    = originalFrontBg;
        backBg     = originalBackBg;

        // 恢复位置（确保哪怕位置刚好是 (0,0) 也能恢复）
        if (hasInitializedPos)
        {
            if (frontPanel != null) frontPanel.anchoredPosition = originalFrontPos;
            if (backPanel != null)  backPanel.anchoredPosition  = originalBackPos;
        }

        // 恢复颜色与层级
        ApplyVisual(frontPanel, frontBg, brightColor);
        ApplyVisual(backPanel,  backBg,  dimColor);
        if (frontPanel) frontPanel.SetAsLastSibling();
    }

    void OnFrequencyChanged(object data)
    {
        if (swapCoroutine != null) StopCoroutine(swapCoroutine);
        swapCoroutine = StartCoroutine(AnimateSwap());
    }

    IEnumerator AnimateSwap()
    {
        // 记录起始状态
        Vector2 posA = frontPanel.anchoredPosition;
        Vector2 posB = backPanel.anchoredPosition;
        Color colorA = frontBg ? frontBg.color : brightColor;
        Color colorB = backBg ? backBg.color : dimColor;

        // 动画过程中把即将到前面的面板提到最上层（在动画中间切换层级更自然）
        bool swappedSibling = false;

        float elapsed = 0f;
        while (elapsed < swapDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / swapDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            // 位置互换
            frontPanel.anchoredPosition = Vector2.Lerp(posA, posB, smooth);
            backPanel.anchoredPosition = Vector2.Lerp(posB, posA, smooth);

            // 颜色互换
            if (frontBg) frontBg.color = Color.Lerp(colorA, dimColor, smooth);
            if (backBg) backBg.color = Color.Lerp(colorB, brightColor, smooth);

            // 动画进行到一半时切换渲染层级
            if (!swappedSibling && t >= 0.5f)
            {
                backPanel.SetAsLastSibling();
                swappedSibling = true;
            }

            yield return null;
        }

        // 确保最终状态精确
        frontPanel.anchoredPosition = posB;
        backPanel.anchoredPosition = posA;
        if (frontBg) frontBg.color = dimColor;
        if (backBg) backBg.color = brightColor;
        backPanel.SetAsLastSibling();

        // 交换引用，下次切换方向正确
        (frontPanel, backPanel) = (backPanel, frontPanel);
        (frontBg, backBg) = (backBg, frontBg);

        swapCoroutine = null;
    }

    void ApplyVisual(RectTransform panel, Image bg, Color color)
    {
        if (bg) bg.color = color;
    }
}
