using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 关卡前序介绍面板
/// 流程：显示动画 → 点击拍照按钮 → 闪光灯 + 快门音效 → 切换到结果页（拍立得相纸滑出）→ 点击继续
/// </summary>
public class LevelIntroUI : MonoBehaviour
{
    public static LevelIntroUI Instance { get; private set; }

    [Header("面板")]
    [SerializeField] private GameObject introPanel;

    [Header("第一页 —— 动画预览")]
    [SerializeField] private GameObject  animPage;
    [SerializeField] private Image       animBackground;
    [SerializeField] private Image       introImage;
    [SerializeField] private Animator    introImageAnimator;
    [SerializeField] private TextMeshProUGUI introTextField;
    [SerializeField] private TextMeshProUGUI chapterLabel;
    [SerializeField] private Button      shutterButton;     // 拍照按钮

    [Header("闪光灯遮罩（全屏白色 Image，初始透明）")]
    [SerializeField] private Image flashOverlay;

    [Header("第二页 —— 拍照结果")]
    [SerializeField] private GameObject resultPage;
    [SerializeField] private Image      resultBackground;   // 背景色块
    [SerializeField] private Image      cameraImage;        // 相机图片（固定位置）
    [SerializeField] private RectTransform polaroidRect;     // 拍立得相纸 RectTransform
    [SerializeField] private Image      polaroidImage;       // 相纸 Image
    [SerializeField] private Button     nextButton;          // 继续按钮

    [Header("动画参数")]
    [SerializeField] private float flashDuration    = 0.3f;  // 闪光灯持续
    [SerializeField] private float flashHoldTime    = 0.15f; // 白屏停留
    [SerializeField] private float polaroidSlideDistance = 300f; // 相纸滑出距离（向下）
    [SerializeField] private float polaroidSlideDuration = 0.8f; // 滑出时长

    private System.Action onNextCallback;
    private LevelData currentLevelData;
    private Vector2 polaroidStartAnchor;   // 相纸隐藏位置
    private Vector2 polaroidEndAnchor;     // 相纸最终位置

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (introPanel) introPanel.SetActive(false);
        if (shutterButton) shutterButton.onClick.AddListener(OnShutterClicked);
        if (nextButton)    nextButton.onClick.AddListener(OnNextClicked);

        // 记录相纸的目标位置，起始位置在下方
        if (polaroidRect)
        {
            polaroidEndAnchor   = polaroidRect.anchoredPosition;
            polaroidStartAnchor = polaroidEndAnchor - Vector2.up * polaroidSlideDistance;
        }
    }

    /// <summary>显示指定关卡的介绍面板</summary>
    public void Show(LevelData levelData, System.Action onNext)
    {
        if (levelData == null) return;

        currentLevelData = levelData;
        onNextCallback   = onNext;

        Time.timeScale = 0f;

        // 章节编号
        if (chapterLabel)
            chapterLabel.text = $"NO.{levelData.levelIndex}";

        // 介绍文字
        if (introTextField)
            introTextField.text = levelData.introText ?? "";

        // 图片 / 动画
        if (introImage && levelData.introImage)
        {
            introImage.sprite = levelData.introImage;
            introImage.raycastTarget = false;
        }

        if (animBackground)
            animBackground.color = levelData.introAnimBgColor;

        if (introImageAnimator)
        {
            if (levelData.introAnimator != null)
            {
                introImageAnimator.runtimeAnimatorController = levelData.introAnimator;
                introImageAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                introImageAnimator.enabled = true;
                introImageAnimator.Update(0f);
            }
            else
            {
                introImageAnimator.enabled = false;
            }
        }

        // 初始化页面状态
        if (animPage)   animPage.SetActive(true);
        if (resultPage) resultPage.SetActive(false);
        if (flashOverlay)
        {
            flashOverlay.color = new Color(1, 1, 1, 0);
            flashOverlay.raycastTarget = false;
            flashOverlay.gameObject.SetActive(true);
        }

        if (introPanel) introPanel.SetActive(true);
    }

    /// <summary>隐藏面板</summary>
    public void Hide()
    {
        if (introPanel) introPanel.SetActive(false);
    }

    // ────── 拍照按钮 ──────
    void OnShutterClicked()
    {
        if (shutterButton) shutterButton.interactable = false;
        StartCoroutine(ShutterSequence());
    }

    IEnumerator ShutterSequence()
    {
        // 播放快门音效
        if (AudioManager.Instance) AudioManager.Instance.PlayCameraShutter();

        // 阶段1：闪光灯亮起（白色遮罩淡入）
        if (flashOverlay)
        {
            flashOverlay.raycastTarget = true;
            float timer = 0;
            float fadeInTime = flashDuration * 0.4f;
            while (timer < fadeInTime)
            {
                timer += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(timer / fadeInTime);
                flashOverlay.color = new Color(1, 1, 1, a);
                yield return null;
            }
            flashOverlay.color = Color.white;
        }

        // 阶段2：白屏停留
        float holdTimer = 0;
        while (holdTimer < flashHoldTime)
        {
            holdTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        // 在白屏期间切换页面
        if (animPage)   animPage.SetActive(false);
        SetupResultPage();
        if (resultPage) resultPage.SetActive(true);

        // 阶段3：闪光灯消退
        if (flashOverlay)
        {
            float timer = 0;
            float fadeOutTime = flashDuration * 0.6f;
            while (timer < fadeOutTime)
            {
                timer += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(timer / fadeOutTime);
                flashOverlay.color = new Color(1, 1, 1, a);
                yield return null;
            }
            flashOverlay.color = new Color(1, 1, 1, 0);
            flashOverlay.raycastTarget = false;
        }

        // 阶段4：拍立得相纸从相机里滑出
        yield return StartCoroutine(PolaroidSlideOut());

        // 显示继续按钮
        if (nextButton) nextButton.gameObject.SetActive(true);
    }

    void SetupResultPage()
    {
        if (currentLevelData == null) return;

        // 背景色
        if (resultBackground)
            resultBackground.color = currentLevelData.introResultBgColor;

        // 拍立得相纸
        if (polaroidImage && currentLevelData.polaroidSprite)
            polaroidImage.sprite = currentLevelData.polaroidSprite;

        // 相纸初始位置（藏在相机上方）
        if (polaroidRect)
            polaroidRect.anchoredPosition = polaroidStartAnchor;

        // 继续按钮先隐藏，等动画播完再显示
        if (nextButton) nextButton.gameObject.SetActive(false);
    }

    IEnumerator PolaroidSlideOut()
    {
        if (polaroidRect == null) yield break;

        // 播放拍立得相纸滑出音效
        if (AudioManager.Instance) AudioManager.Instance.PlayPolaroidSlide();

        float timer = 0;
        while (timer < polaroidSlideDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(timer / polaroidSlideDuration));
            polaroidRect.anchoredPosition = Vector2.Lerp(polaroidStartAnchor, polaroidEndAnchor, t);
            yield return null;
        }
        polaroidRect.anchoredPosition = polaroidEndAnchor;
    }

    // ────── 继续按钮 ──────
    void OnNextClicked()
    {
        if (shutterButton) shutterButton.interactable = true;
        Hide();
        Time.timeScale = 1f;
        onNextCallback?.Invoke();
    }
}
