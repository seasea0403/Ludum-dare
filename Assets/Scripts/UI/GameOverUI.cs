using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏结束面板（死亡/通关）
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("胜利面板")]
    [SerializeField] private GameObject      victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryStatsText;
    [SerializeField] private Button          victoryNextButton;
    [SerializeField] private Button          victoryRetryButton;

    [Header("胜利面板 - 皇冠动画")]
    [SerializeField] private Image[]         victoryCrowns;
    [SerializeField] private Sprite          crownActiveSprite;
    [SerializeField] private Sprite          crownInactiveSprite;

    [Header("失败面板")]
    [SerializeField] private GameObject      defeatPanel;
    [SerializeField] private TextMeshProUGUI defeatStatsText;
    [SerializeField] private Button          defeatRetryButton;
    
    [Header("失败面板 - 生命动画")]
    [SerializeField] private Image[]         defeatLives;
    [SerializeField] private Sprite          lifeActiveSprite;
    [SerializeField] private Sprite          lifeInactiveSprite;

    [Header("延迟与动画设置")]
    [SerializeField] private float delayBeforeShow = 1f;
    [SerializeField] private float uiAnimationInterval = 0.3f;

    [Header("返回主菜单按钮")]
    [SerializeField] private Button          victoryMenuButton;
    [SerializeField] private Button          defeatMenuButton;

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.PlayerDied,     OnPlayerDied);
        EventBus.Subscribe(GameEvents.LevelCompleted, OnLevelCompleted);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.PlayerDied,     OnPlayerDied);
        EventBus.Unsubscribe(GameEvents.LevelCompleted, OnLevelCompleted);
    }

    void Start()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);

        if (victoryNextButton) victoryNextButton.onClick.AddListener(OnNextLevel);
        if (victoryRetryButton) victoryRetryButton.onClick.AddListener(OnRetry);
        if (defeatRetryButton) defeatRetryButton.onClick.AddListener(OnRetry);
        if (victoryMenuButton) victoryMenuButton.onClick.AddListener(OnReturnToMenu);
        if (defeatMenuButton) defeatMenuButton.onClick.AddListener(OnReturnToMenu);
    }

    void OnPlayerDied(object data)
    {
        StartCoroutine(ShowPanelDelayed(false));
    }

    void OnLevelCompleted(object data)
    {
        StartCoroutine(ShowPanelDelayed(true));
    }

    System.Collections.IEnumerator ShowPanelDelayed(bool isVictory)
    {
        // 挂起一部分玩家控制，这里等待死亡或开书动画播完
        var player = FindObjectOfType<PlayerController>();
        if (player)
        {
            player.enabled = false; // 禁用玩家位移和输入
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb) rb.velocity = new Vector2(0, rb.velocity.y); // 停下水平移动，允许落地
        }
        
        yield return new WaitForSeconds(delayBeforeShow);

        // 停止打字机（防止打字音效与结算音效混杂）
        if (AudioManager.Instance) AudioManager.Instance.StopTyping();
        var typingEffect = FindObjectOfType<TypingEffect>(true);
        if (typingEffect) typingEffect.Clear();

        // 动画播完后暂停游戏并弹窗
        Time.timeScale = 0f;
        
        if (isVictory)
        {
            if (victoryPanel) victoryPanel.SetActive(true);
            if (player && victoryStatsText)
                victoryStatsText.text = $"{player.CoinCount}";
            if (victoryNextButton)
                victoryNextButton.gameObject.SetActive(LevelManager.Instance != null && LevelManager.Instance.HasNextLevel);
            
            // 播放皇冠结算动画
            StartCoroutine(AnimateVictoryCrowns(player ? player.CrownCount : 0));
        }
        else
        {
            if (defeatPanel) defeatPanel.SetActive(true);
            if (player && defeatStatsText)
                defeatStatsText.text = $"{player.CoinCount}";
            
            // 播放失败扣血动画
            StartCoroutine(AnimateDefeatLives());
        }
    }

    private System.Collections.IEnumerator AnimateVictoryCrowns(int crownCount)
    {
        // 1. 初始化所有皇冠为灰色（未收集）
        if (victoryCrowns != null)
        {
            for (int i = 0; i < victoryCrowns.Length; i++)
            {
                if (victoryCrowns[i] != null) 
                    victoryCrowns[i].sprite = crownInactiveSprite;
            }
        }

        // 2. 等待面板显示一小会儿再开始动画
        yield return new WaitForSecondsRealtime(uiAnimationInterval);

        // 3. 根据收集数量依次点亮
        if (victoryCrowns != null)
        {
            for (int i = 0; i < crownCount && i < victoryCrowns.Length; i++)
            {
                if (victoryCrowns[i] != null && crownActiveSprite != null)
                {
                    victoryCrowns[i].sprite = crownActiveSprite;
                    if (AudioManager.Instance) AudioManager.Instance.PlayCrownCollect();
                }
                yield return new WaitForSecondsRealtime(uiAnimationInterval);
            }
        }
    }

    private System.Collections.IEnumerator AnimateDefeatLives()
    {
        // 1. 初始化所有生命为红色（满血）
        if (defeatLives != null)
        {
            for (int i = 0; i < defeatLives.Length; i++)
            {
                if (defeatLives[i] != null) 
                    defeatLives[i].sprite = lifeActiveSprite;
            }
        }

        // 2. 等待面板显示一小会儿再开始动画
        yield return new WaitForSecondsRealtime(uiAnimationInterval);

        // 3. 依次变灰（扣血）
        if (defeatLives != null)
        {
            for (int i = 0; i < defeatLives.Length; i++)
            {
                if (defeatLives[i] != null && lifeInactiveSprite != null)
                {
                    defeatLives[i].sprite = lifeInactiveSprite;
                    if (AudioManager.Instance) AudioManager.Instance.PlayPlayerHit();
                }
                yield return new WaitForSecondsRealtime(uiAnimationInterval);
            }
        }
    }

    void OnRetry()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        LevelManager.Instance.ReloadLevel();
    }

    void OnNextLevel()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        LevelManager.Instance.LoadNextLevel();
    }

    void OnReturnToMenu()
    {
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        Time.timeScale = 0f;
        var mainMenuUI = FindObjectOfType<MainMenuUI>(true);
        if (mainMenuUI) mainMenuUI.Show();
    }
}
