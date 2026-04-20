using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 游戏内开始/选关面板
/// 不依赖场景切换；通过 LevelManager.LoadLevel() 在当前场景内切换SO关卡
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("面板根节点（点击开始后隐藏）")]
    [SerializeField] private GameObject menuPanel;

    [Header("关卡按钮")]
    [SerializeField] private Button startButton;
    [SerializeField] private Animator startButtonAnim;      // Start 按钮旁边的动画 Image
    [SerializeField] private Button[] levelButtons;   // 5 个关卡按钮

    [Header("教程/引导页面（点击开始后先展示教学）")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private Button guideCloseButton;

    [Header("设置")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Animator settingsButtonAnim;   // Settings 按钮旁边的动画 Image

    void Start()
    {
        // 进入游戏时暂停，等待玩家从菜单选择
        if (menuPanel) menuPanel.SetActive(true);
        if (guidePanel) guidePanel.SetActive(false);
        Time.timeScale = 0f;

        // 为 Start 和关卡按钮统一添加动画交互
        SetupAnimatedButton(startButton, startButtonAnim, () => StartGame(0));
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int idx = i;
            SetupAnimatedButton(levelButtons[i], null, () => StartGame(idx));
        }

        if (settingsButton)
            SetupAnimatedButton(settingsButton, settingsButtonAnim, () =>
            {
                if (SettingsUI.Instance) SettingsUI.Instance.Show(false);
            });
    }

    /// <summary>为按钮添加 hover/click 动画行为</summary>
    void SetupAnimatedButton(Button btn, Animator externalAnim, System.Action callback)
    {
        if (btn == null) return;

        // 优先用传入的外部 Animator（旁边的 Image），否则 fallback 到按钮自身
        var animator = externalAnim != null ? externalAnim : btn.GetComponent<Animator>();

        // 保留 Button 本身的颜色过渡（ColorTint），实现按钮本身的深浅变化
        // 如果 Animator 是挂在外部图片上，那么这两者就不会冲突
        if (externalAnim != null)
        {
            btn.transition = Selectable.Transition.ColorTint;
        }
        else
        {
            btn.transition = Selectable.Transition.None;
        }
        
        btn.onClick.RemoveAllListeners();

        var handler = btn.gameObject.GetComponent<AnimatedButtonHandler>();
        if (handler == null)
            handler = btn.gameObject.AddComponent<AnimatedButtonHandler>();

        handler.Init(animator, callback);
    }

    void StartGame(int levelIndex)
    {
        if (menuPanel) menuPanel.SetActive(false);

        // 选择第一关（或直接点 Start）时，如果有教学面板，则先引导玩家
        if (levelIndex == 0 && guidePanel != null)
        {
            guidePanel.SetActive(true);
            if (guideCloseButton)
            {
                guideCloseButton.onClick.RemoveAllListeners();
                guideCloseButton.onClick.AddListener(() => 
                {
                    guidePanel.SetActive(false);
                    // 关闭引导面板后进入教学关卡
                    Time.timeScale = 1f;
                    if (LevelManager.Instance) LevelManager.Instance.LoadTutorial();
                });
            }
        }
        else
        {
            // 无教学面板，或跳关直接开始该关卡
            Time.timeScale = 1f;
            if (LevelManager.Instance) LevelManager.Instance.LoadLevel(levelIndex);
        }
    }
}
