using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最终关结局 UI：画面渐白 → 显示结局面板
/// 挂在 Canvas 下，包含一个全屏白色 Image（初始透明）和一个结局面板
/// </summary>
public class EndingUI : MonoBehaviour
{
    public static EndingUI Instance { get; private set; }

    [Header("白色遮罩（全屏 Image，初始透明）")]
    [SerializeField] private Image whiteOverlay;

    [Header("结局面板（渐白完成后显示）")]
    [SerializeField] private GameObject endingPanel;

    [Header("渐变设置")]
    [SerializeField] private float fadeToWhiteDuration = 2f;
    [SerializeField] private float holdWhiteDuration   = 1f;
    [SerializeField] private float panelFadeInDuration = 1f;

    private CanvasGroup endingPanelCanvasGroup;

    void Awake()
    {
        Instance = this;

        if (whiteOverlay)
        {
            whiteOverlay.color = new Color(1, 1, 1, 0);
            whiteOverlay.raycastTarget = false;
            whiteOverlay.gameObject.SetActive(true);
        }

        if (endingPanel)
        {
            endingPanelCanvasGroup = endingPanel.GetComponent<CanvasGroup>();
            if (!endingPanelCanvasGroup)
                endingPanelCanvasGroup = endingPanel.AddComponent<CanvasGroup>();
            endingPanelCanvasGroup.alpha = 0;
            endingPanel.SetActive(false);
        }
    }

    public void StartEnding()
    {
        StartCoroutine(EndingSequence());
    }

    IEnumerator EndingSequence()
    {
        // 停止打字机音效
        if (AudioManager.Instance) AudioManager.Instance.StopTyping();
        var typingEffect = FindObjectOfType<TypingEffect>(true);
        if (typingEffect) typingEffect.Clear();

        // 阶段1：画面渐渐变白
        if (whiteOverlay)
        {
            whiteOverlay.raycastTarget = true; // 阻挡点击
            float timer = 0;
            while (timer < fadeToWhiteDuration)
            {
                timer += Time.deltaTime;
                float a = Mathf.Clamp01(timer / fadeToWhiteDuration);
                whiteOverlay.color = new Color(1, 1, 1, a);
                yield return null;
            }
            whiteOverlay.color = Color.white;
        }

        // 阶段2：白屏停留
        yield return new WaitForSeconds(holdWhiteDuration);

        // 隐藏 PhotoHUD
        var photoHUD = FindObjectOfType<PhotoHUD>(true);
        if (photoHUD) photoHUD.gameObject.SetActive(false);

        // 阶段3：显示结局面板，淡入
        if (endingPanel && endingPanelCanvasGroup)
        {
            endingPanel.SetActive(true);
            float timer = 0;
            while (timer < panelFadeInDuration)
            {
                timer += Time.deltaTime;
                endingPanelCanvasGroup.alpha = Mathf.Clamp01(timer / panelFadeInDuration);
                yield return null;
            }
            endingPanelCanvasGroup.alpha = 1;
        }

        // 广播游戏完成事件
        EventBus.Publish(GameEvents.GameCompleted, null);
    }
}
