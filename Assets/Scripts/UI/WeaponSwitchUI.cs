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

    void Awake()
    {
        originalFrontPanel = frontPanel;
        originalBackPanel  = backPanel;
        originalFrontBg    = frontBg;
        originalBackBg     = backBg;
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
        // 记录初始锚点位置（必须在 Start 中，RectTransform 已完成布局后再读）
        if (frontPanel) originalFrontPos = frontPanel.anchoredPosition;
        if (backPanel)  originalBackPos  = backPanel.anchoredPosition;

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

        // 恢复位置（仅当位置已记录时才恢复）
        if (frontPanel && originalFrontPos != Vector2.zero)
            frontPanel.anchoredPosition = originalFrontPos;
        if (backPanel && originalBackPos != Vector2.zero)
            backPanel.anchoredPosition  = originalBackPos;

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
