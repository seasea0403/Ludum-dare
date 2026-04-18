using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏结束面板（死亡/通关）
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject      panel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button          retryButton;
    [SerializeField] private Button          nextLevelButton;

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
        panel.SetActive(false);
        retryButton.onClick.AddListener(OnRetry);
        nextLevelButton.onClick.AddListener(OnNextLevel);
    }

    void OnPlayerDied(object data)
    {
        Show("游戏结束", false);
    }

    void OnLevelCompleted(object data)
    {
        Show("关卡完成!", true);
    }

    void Show(string title, bool showNext)
    {
        Time.timeScale = 0f;
        panel.SetActive(true);
        titleText.text = title;

        var player = FindObjectOfType<PlayerController>();
        if (player && statsText)
            statsText.text = $"里程: {Mathf.FloorToInt(player.Distance)} m\n金币: {player.CoinCount}";

        nextLevelButton.gameObject.SetActive(showNext && LevelManager.Instance != null && LevelManager.Instance.HasNextLevel);
    }

    void OnRetry()
    {
        Time.timeScale = 1f;
        LevelManager.Instance.ReloadLevel();
    }

    void OnNextLevel()
    {
        Time.timeScale = 1f;
        LevelManager.Instance.LoadNextLevel();
    }
}
