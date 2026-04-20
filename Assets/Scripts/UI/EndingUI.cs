using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    [Header("结局内容图层")]
    [SerializeField] private Image endingImageA;
    [SerializeField] private Image endingImageB;
    [SerializeField] private Image blackOverlay;

    [Header("结局文字（按顺序显示）")]
    [SerializeField] private GameObject[] textRoots;

    [Header("重玩按钮")]
    [SerializeField] private Button replayButton;

    [Header("渐变设置")]
    [SerializeField] private float fadeToWhiteDuration = 2f;
    [SerializeField] private float holdWhiteDuration   = 1f;
    [SerializeField] private float panelFadeInDuration = 1f;

    [Header("结局节奏")]
    [SerializeField] private float imageAFadeInDuration = 1.0f;
    [SerializeField] private float imageAHoldDuration = 1.5f;
    [SerializeField] private float imageCrossFadeDuration = 1.2f;
    [SerializeField] private float imageBHoldDuration = 1.2f;
    [SerializeField] private float toBlackDuration = 3f;
    [SerializeField] private float textStartDelay = 0.4f;
    [SerializeField] private float textShowDuration = 1.6f;
    [SerializeField] private float textInterval = 1.6f;

    private CanvasGroup endingPanelCanvasGroup;
    private bool hasStarted;

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

        if (endingImageA) SetImageAlpha(endingImageA, 0f);
        if (endingImageB) SetImageAlpha(endingImageB, 0f);
        if (blackOverlay)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
            blackOverlay.raycastTarget = false;
        }

        ArrangeEndingLayerOrder();

        if (textRoots != null)
        {
            foreach (var root in textRoots)
            {
                if (root) root.SetActive(false);
            }
        }

        if (replayButton)
        {
            replayButton.gameObject.SetActive(false);
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(OnReplayClicked);
        }
    }

    public void StartEnding()
    {
        if (hasStarted) return;
        hasStarted = true;
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

        // 过渡结束后释放白遮罩，避免拦截结局按钮点击
        if (whiteOverlay)
        {
            whiteOverlay.raycastTarget = false;
            whiteOverlay.color = new Color(1f, 1f, 1f, 0f);
        }

        // 重置结局元素初始可见性
        if (endingImageA) SetImageAlpha(endingImageA, 0f);
        if (endingImageB) SetImageAlpha(endingImageB, 0f);
        if (blackOverlay)
        {
            blackOverlay.gameObject.SetActive(true);
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
        }

        ArrangeEndingLayerOrder();

        if (textRoots != null)
        {
            foreach (var root in textRoots)
            {
                if (root) root.SetActive(false);
            }
        }

        if (replayButton) replayButton.gameObject.SetActive(false);

        // 阶段4：图1渐显
        if (endingImageA)
        {
            float timer = 0f;
            while (timer < imageAFadeInDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, imageAFadeInDuration));
                SetImageAlpha(endingImageA, t);
                yield return null;
            }
            SetImageAlpha(endingImageA, 1f);
        }

        // 阶段5：图1停留
        yield return new WaitForSeconds(imageAHoldDuration);

        // 阶段6：图1渐隐，图2渐显
        {
            float timer = 0f;
            while (timer < imageCrossFadeDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / imageCrossFadeDuration);
                if (endingImageA) SetImageAlpha(endingImageA, 1f - t);
                if (endingImageB) SetImageAlpha(endingImageB, t);
                yield return null;
            }
            if (endingImageA) SetImageAlpha(endingImageA, 0f);
            if (endingImageB) SetImageAlpha(endingImageB, 1f);
        }

        // 阶段7：图2停留
        yield return new WaitForSeconds(imageBHoldDuration);

        // 阶段8：图2慢慢变黑，同时依次显示文字
        yield return StartCoroutine(PlayBlackAndTexts());

        // 阶段9：最后显示重玩按钮
        if (replayButton)
        {
            replayButton.gameObject.SetActive(true);
            replayButton.transform.SetAsLastSibling();
        }

        // 广播游戏完成事件
        EventBus.Publish(GameEvents.GameCompleted, null);
    }

    private IEnumerator PlayBlackAndTexts()
    {
        int textCount = textRoots != null ? textRoots.Length : 0;
        float blackElapsed = 0f;

        if (textRoots != null)
        {
            for (int i = 0; i < textRoots.Length; i++)
            {
                if (textRoots[i]) textRoots[i].SetActive(false);
            }
        }

        // 起始延迟
        float startTimer = 0f;
        while (startTimer < textStartDelay)
        {
            float dt = Time.deltaTime;
            startTimer += dt;
            blackElapsed += dt;
            UpdateBlackOverlay(blackElapsed);
            yield return null;
        }

        // 文字依次显示后隐藏
        for (int i = 0; i < textCount; i++)
        {
            if (textRoots[i])
            {
                textRoots[i].SetActive(true);
                textRoots[i].transform.SetAsLastSibling();
            }

            float showTimer = 0f;
            while (showTimer < textShowDuration)
            {
                float dt = Time.deltaTime;
                showTimer += dt;
                blackElapsed += dt;
                UpdateBlackOverlay(blackElapsed);
                yield return null;
            }

            if (textRoots[i]) textRoots[i].SetActive(false);

            if (i < textCount - 1)
            {
                float gapTimer = 0f;
                while (gapTimer < textInterval)
                {
                    float dt = Time.deltaTime;
                    gapTimer += dt;
                    blackElapsed += dt;
                    UpdateBlackOverlay(blackElapsed);
                    yield return null;
                }
            }
        }

        // 如果黑场还没完成，继续补到全黑
        while (blackElapsed < toBlackDuration)
        {
            float dt = Time.deltaTime;
            blackElapsed += dt;
            UpdateBlackOverlay(blackElapsed);
            yield return null;
        }

        UpdateBlackOverlay(toBlackDuration);
    }

    private void UpdateBlackOverlay(float elapsed)
    {
        if (!blackOverlay) return;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, toBlackDuration));
        blackOverlay.color = new Color(0f, 0f, 0f, t);
    }

    private void OnReplayClicked()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void SetImageAlpha(Image target, float alpha)
    {
        if (!target) return;
        var c = target.color;
        c.a = Mathf.Clamp01(alpha);
        target.color = c;
    }

    private void ArrangeEndingLayerOrder()
    {
        // 期望层级：ImageA -> ImageB -> BlackOverlay -> TextRoots -> ReplayButton
        if (endingImageA && endingImageB && endingImageA.transform.parent == endingImageB.transform.parent)
        {
            int aIndex = endingImageA.transform.GetSiblingIndex();
            int bIndex = endingImageB.transform.GetSiblingIndex();
            if (aIndex > bIndex)
                endingImageA.transform.SetSiblingIndex(bIndex);

            endingImageB.transform.SetAsLastSibling();
        }

        if (blackOverlay && endingImageB && blackOverlay.transform.parent == endingImageB.transform.parent)
        {
            int bIndex = endingImageB.transform.GetSiblingIndex();
            blackOverlay.transform.SetSiblingIndex(bIndex + 1);
        }

        if (textRoots != null)
        {
            foreach (var root in textRoots)
            {
                if (root) root.transform.SetAsLastSibling();
            }
        }

        if (replayButton)
            replayButton.transform.SetAsLastSibling();
    }
}
