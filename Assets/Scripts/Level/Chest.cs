using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 宝箱：固定位置奖励，碰到后给玩家金币 + 短暂护盾
/// Tag: "Chest"，Collider2D: Trigger
/// 自身只负责视觉和碰撞检测，通过 EventBus 广播
/// 还负责显示 +金币 的浮动 UI 反馈
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("奖励")]
    [SerializeField] private int   coinReward    = 8;
    [SerializeField] private float shieldTime    = 3f;

    [Header("视觉")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;

    [Header("金币反馈 UI")]
    [SerializeField] private GameObject feedbackRoot; // 留空即可，运行时自动查找
    [SerializeField] private string feedbackRootName = "ChestCoinFeedbackRoot"; // 场景中 UI root 的名字
    [SerializeField] private float feedbackFadeDuration = 0.6f;
    [SerializeField] private float feedbackHoldDuration = 0.4f;
    [SerializeField] private float feedbackFloatDistance = 50f;
    [SerializeField] private float feedbackFloatDuration = 1f;

    private SpriteRenderer sr;
    private Animator animator;
    private bool isOpened;
    private CanvasGroup feedbackCanvasGroup;
    private RectTransform feedbackRectTransform;
    private Vector2 feedbackStartPos;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Start()
    {
        InitFeedbackUI();
    }

    private void InitFeedbackUI()
    {
        // 预制体无法引用场景 UI，运行时通过名字查找
        if (feedbackRoot == null && !string.IsNullOrEmpty(feedbackRootName))
            feedbackRoot = GameObject.Find(feedbackRootName);

        if (feedbackRoot == null) return;

        feedbackCanvasGroup = feedbackRoot.GetComponent<CanvasGroup>();
        if (!feedbackCanvasGroup)
            feedbackCanvasGroup = feedbackRoot.AddComponent<CanvasGroup>();

        feedbackRectTransform = feedbackRoot.GetComponent<RectTransform>();
        if (feedbackRectTransform)
            feedbackStartPos = feedbackRectTransform.anchoredPosition;

        feedbackCanvasGroup.alpha = 0f;
        feedbackRoot.SetActive(false);
    }

    void OnEnable()
    {
        isOpened = false;
        
        // 如果有Animator，重置为关闭状态
        if (animator)
        {
            animator.SetBool("IsOpen", false);
        }

        if (sr)
        {
            sr.enabled = true;
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
            if (closedSprite)
                sr.sprite = closedSprite;
        }

        // 清理池复用时可能残留的雾子物体
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "Fog")
                DestroyImmediate(child.gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isOpened) return;
        if (!other.CompareTag("Player")) return;

        // 被雾覆盖时由 PlayerController 统一处理伤害，不触发宝箱逻辑
        var fog = GetComponent<FogCover>();
        if (fog != null && !fog.IsRevealed) return;

        isOpened = true;

        // 播放打开动画
        if (animator)
        {
            animator.SetBool("IsOpen", true);
        }
        else if (sr && openedSprite)
        {
            // 如果没有Animator的降级处理：直接换图
            sr.sprite = openedSprite;
        }

        // 广播宝箱事件，传递奖励数据
        var reward = new ChestReward { coins = coinReward, shieldDuration = shieldTime };
        EventBus.Publish(GameEvents.ChestOpened, reward);

        // 显示金币反馈 UI
        if (feedbackRoot != null)
        {
            StartCoroutine(ShowCoinFeedback(coinReward));
        }

        // ※ 移除了原本的摧毁逻辑，宝箱现在只会打开并留在原地 ※
    }

    private IEnumerator ShowCoinFeedback(int coins)
    {
        if (feedbackRoot == null || feedbackCanvasGroup == null || feedbackRectTransform == null)
            yield break;

        // 重置位置和透明度
        feedbackRectTransform.anchoredPosition = feedbackStartPos;
        feedbackCanvasGroup.alpha = 0f;
        feedbackRoot.SetActive(true);

        // 更新显示的金币数量
        var textComp = feedbackRoot.GetComponentInChildren<TextMeshProUGUI>();
        if (textComp != null)
            textComp.text = $"+{coins}";

        // 淡入
        float fadeInTimer = 0f;
        while (fadeInTimer < feedbackFadeDuration)
        {
            fadeInTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeInTimer / feedbackFadeDuration);
            feedbackCanvasGroup.alpha = t;
            yield return null;
        }
        feedbackCanvasGroup.alpha = 1f;

        // 保持显示
        yield return new WaitForSeconds(feedbackHoldDuration);

        // 淡出 + 浮动
        float fadeOutTimer = 0f;
        float floatTimer = 0f;

        while (fadeOutTimer < feedbackFadeDuration || floatTimer < feedbackFloatDuration)
        {
            float dt = Time.deltaTime;
            fadeOutTimer += dt;
            floatTimer += dt;

            // 淡出
            if (fadeOutTimer <= feedbackFadeDuration)
            {
                float t = Mathf.Clamp01(fadeOutTimer / feedbackFadeDuration);
                feedbackCanvasGroup.alpha = 1f - t;
            }
            else
            {
                feedbackCanvasGroup.alpha = 0f;
            }

            // 浮动
            if (floatTimer <= feedbackFloatDuration)
            {
                float t = Mathf.Clamp01(floatTimer / feedbackFloatDuration);
                float newY = feedbackStartPos.y + feedbackFloatDistance * t;
                feedbackRectTransform.anchoredPosition = new Vector2(feedbackStartPos.x, newY);
            }

            yield return null;
        }

        // 隐藏
        feedbackRoot.SetActive(false);
    }
}

/// <summary>宝箱奖励数据，通过 EventBus 传递</summary>
public class ChestReward
{
    public int   coins;
    public float shieldDuration;
}
