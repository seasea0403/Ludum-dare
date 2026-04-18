using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD：血量、金币数、里程数、当前频段
/// 挂在 Canvas 下的 HUD 面板上
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("血量")]
    [SerializeField] private Image[] heartIcons;           // 3 个心形图标
    [SerializeField] private Sprite  heartFull;
    [SerializeField] private Sprite  heartEmpty;

    [Header("数据显示")]
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI frequencyText;

    [Header("皇冠")]
    [SerializeField] private Image[] crownIcons;           // 3 个皇冠图标
    [SerializeField] private Sprite  crownCollected;
    [SerializeField] private Sprite  crownEmpty;

    [Header("护盾")]
    [SerializeField] private GameObject shieldIndicator;    // 护盾激活时显示的 UI 元素

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.PlayerHit,          OnPlayerHit);
        EventBus.Subscribe(GameEvents.CoinCollected,      OnCoinCollected);
        EventBus.Subscribe(GameEvents.DistanceUpdated,    OnDistanceUpdated);
        EventBus.Subscribe(GameEvents.FrequencyChanged,   OnFrequencyChanged);
        EventBus.Subscribe(GameEvents.CrownCountChanged,  OnCrownCountChanged);
        EventBus.Subscribe(GameEvents.ShieldActivated,    OnShieldActivated);
        EventBus.Subscribe(GameEvents.ShieldBroken,       OnShieldBroken);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.PlayerHit,          OnPlayerHit);
        EventBus.Unsubscribe(GameEvents.CoinCollected,      OnCoinCollected);
        EventBus.Unsubscribe(GameEvents.DistanceUpdated,    OnDistanceUpdated);
        EventBus.Unsubscribe(GameEvents.FrequencyChanged,   OnFrequencyChanged);
        EventBus.Unsubscribe(GameEvents.CrownCountChanged,  OnCrownCountChanged);
        EventBus.Unsubscribe(GameEvents.ShieldActivated,    OnShieldActivated);
        EventBus.Unsubscribe(GameEvents.ShieldBroken,       OnShieldBroken);
    }

    void Start()
    {
        if (coinText)     coinText.text     = "0";
        if (distanceText) distanceText.text = "0 m";
        if (frequencyText) frequencyText.text = "高频 - 信号模式";
        UpdateHearts(3);
        UpdateCrowns(0);
        if (shieldIndicator) shieldIndicator.SetActive(false);
    }

    void OnPlayerHit(object data)
    {
        int hp = (int)data;
        UpdateHearts(hp);
    }

    void OnCoinCollected(object data)
    {
        int count = (int)data;
        if (coinText) coinText.text = count.ToString();
    }

    void OnDistanceUpdated(object data)
    {
        float dist = (float)data;
        if (distanceText) distanceText.text = Mathf.FloorToInt(dist) + " m";
    }

    void OnFrequencyChanged(object data)
    {
        var freq = (SignalFrequency)data;
        if (frequencyText)
            frequencyText.text = freq == SignalFrequency.High
                ? "高频 - 信号模式"
                : "低频 - 攻击模式";
    }

    void UpdateHearts(int currentHP)
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i])
                heartIcons[i].sprite = i < currentHP ? heartFull : heartEmpty;
        }
    }

    void OnCrownCountChanged(object data)
    {
        int count = (int)data;
        UpdateCrowns(count);
    }

    void UpdateCrowns(int collected)
    {
        for (int i = 0; i < crownIcons.Length; i++)
        {
            if (crownIcons[i])
                crownIcons[i].sprite = i < collected ? crownCollected : crownEmpty;
        }
    }

    void OnShieldActivated(object data)
    {
        if (shieldIndicator) shieldIndicator.SetActive(true);
    }

    void OnShieldBroken(object data)
    {
        if (shieldIndicator) shieldIndicator.SetActive(false);
    }
}
