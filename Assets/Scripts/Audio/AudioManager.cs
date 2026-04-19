using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局音频管理器（单例）
/// 负责 BGM 播放/切换 + SFX 播放（对象池复用 AudioSource）
/// 挂在场景中一个空 GameObject 上，标记 DontDestroyOnLoad
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ───── BGM ─────
    [Header("BGM")]
    [SerializeField] private AudioClip[] levelBGMs;         // 按关卡索引 0~4
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.4f;
    [SerializeField] private float bgmFadeDuration = 1f;

    // ───── SFX ─────
    [Header("SFX — 玩家")]
    [SerializeField] private AudioClip sfxBirth;
    [SerializeField] private AudioClip sfxDeath;
    [SerializeField] private AudioClip sfxHit;              // 扣除爱心
    [SerializeField] private AudioClip sfxJump;

    [Header("SFX — 波")]
    [SerializeField] private AudioClip sfxScanWave;         // 高频脉冲（清雾）
    [SerializeField] private AudioClip sfxClearWave;        // 低频攻击波
    [SerializeField] private AudioClip sfxFrequencyShift;   // E 切换频段

    [Header("SFX — 物件")]
    [SerializeField] private AudioClip sfxBubblePop;        // 泡泡破裂（雾消散）
    [SerializeField] private AudioClip sfxObstacleShatter;  // 击碎障碍物
    [SerializeField] private AudioClip sfxCoinCollect;      // 金币
    [SerializeField] private AudioClip sfxCrownCollect;     // 皇冠（收集奖励）
    [SerializeField] private AudioClip sfxChestOpen;        // 宝箱
    [SerializeField] private AudioClip sfxShieldActivate;   // 获得护盾

    [Header("SFX — 关卡")]
    [SerializeField] private AudioClip sfxLevelComplete;    // 关卡通关
    [SerializeField] private AudioClip sfxLevelIntroText;   // 关卡开头文字
    [SerializeField] private AudioClip sfxTyping;           // 打字机逐字音效
    [SerializeField] private AudioClip sfxBookFlip;         // 翻书音效

    [Header("SFX 设置")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;
    [SerializeField] private int sfxPoolSize = 8;

    // ───── 内部 ─────
    private AudioSource bgmSourceA;
    private AudioSource bgmSourceB;
    private AudioSource activeBgmSource;
    private AudioSource typingAudioSource;  // 打字音效专用 source
    private List<AudioSource> sfxPool;

    // ══════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建两个 BGM Source 用于淡入淡出切换
        bgmSourceA = CreateAudioSource("BGM_A", true);
        bgmSourceB = CreateAudioSource("BGM_B", true);
        activeBgmSource = bgmSourceA;

        // 创建打字音效专用 source（不循环）
        typingAudioSource = CreateAudioSource("Typing", false);

        // 创建 SFX 对象池
        sfxPool = new List<AudioSource>(sfxPoolSize);
        for (int i = 0; i < sfxPoolSize; i++)
            sfxPool.Add(CreateAudioSource($"SFX_{i}", false));
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.PlayerHit,         OnPlayerHit);
        EventBus.Subscribe(GameEvents.PlayerDied,        OnPlayerDied);
        EventBus.Subscribe(GameEvents.CoinCollected,     OnCoinCollected);
        EventBus.Subscribe(GameEvents.CrownCollected,    OnCrownCollected);
        EventBus.Subscribe(GameEvents.ChestOpened,       OnChestOpened);
        EventBus.Subscribe(GameEvents.FrequencyChanged,  OnFrequencyChanged);
        EventBus.Subscribe(GameEvents.ShieldActivated,   OnShieldActivated);
        EventBus.Subscribe(GameEvents.LevelCompleted,    OnLevelCompleted);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.PlayerHit,         OnPlayerHit);
        EventBus.Unsubscribe(GameEvents.PlayerDied,        OnPlayerDied);
        EventBus.Unsubscribe(GameEvents.CoinCollected,     OnCoinCollected);
        EventBus.Unsubscribe(GameEvents.CrownCollected,    OnCrownCollected);
        EventBus.Unsubscribe(GameEvents.ChestOpened,       OnChestOpened);
        EventBus.Unsubscribe(GameEvents.FrequencyChanged,  OnFrequencyChanged);
        EventBus.Unsubscribe(GameEvents.ShieldActivated,   OnShieldActivated);
        EventBus.Unsubscribe(GameEvents.LevelCompleted,    OnLevelCompleted);
    }

    // ══════════════════════════════════════════
    //  公开接口 —— 供不走 EventBus 的脚本直接调用
    // ══════════════════════════════════════════

    /// <summary>播放出生音效</summary>
    public void PlayBirth()            => PlaySFX(sfxBirth);

    /// <summary>播放跳跃音效</summary>
    public void PlayJump()             => PlaySFX(sfxJump);

    /// <summary>播放高频脉冲（Scan Wave）音效</summary>
    public void PlayScanWave()         => PlaySFX(sfxScanWave);

    /// <summary>播放低频攻击波（Clear Wave）音效</summary>
    public void PlayClearWave()        => PlaySFX(sfxClearWave);

    /// <summary>播放泡泡破裂（雾消散）音效</summary>
    public void PlayBubblePop()        => PlaySFX(sfxBubblePop);

    /// <summary>播放障碍物击碎音效</summary>
    public void PlayObstacleShatter()  => PlaySFX(sfxObstacleShatter);

    /// <summary>播放关卡开头文字音效</summary>
    public void PlayLevelIntroText()   => PlaySFX(sfxLevelIntroText);
    
    /// <summary>播放皇冠收集音效（用于UI结算动画）</summary>
    public void PlayCrownCollect()     => PlaySFX(sfxCrownCollect);
    
    /// <summary>播放玩家受击/扣血音效（用于UI结算动画）</summary>
    public void PlayPlayerHit()        => PlaySFX(sfxHit);

    /// <summary>开始播放打字音效（长音效，持续播放）</summary>
    public void PlayTypingStart()
    {
        if (sfxTyping == null || typingAudioSource == null) return;
        if (typingAudioSource.isPlaying)
            typingAudioSource.Stop();
        typingAudioSource.clip = sfxTyping;
        typingAudioSource.volume = sfxVolume;
        typingAudioSource.pitch = 1f;
        typingAudioSource.Play();
    }
    
    /// <summary>停止打字音效</summary>
    public void StopTyping()
    {
        if (typingAudioSource != null && typingAudioSource.isPlaying)
            typingAudioSource.Stop();
    }
    
    public void PlayBookFlip()          => PlaySFX(sfxBookFlip);

    /// <summary>按关卡索引播放 BGM（带淡入淡出）</summary>
    public void PlayBGM(int levelIndex)
    {
        if (levelBGMs == null || levelIndex < 0 || levelIndex >= levelBGMs.Length) return;
        var clip = levelBGMs[levelIndex];
        if (clip == null) return;

        // 如果正在播放同一首，跳过
        if (activeBgmSource.clip == clip && activeBgmSource.isPlaying) return;

        StartCoroutine(CrossfadeBGM(clip));
    }

    /// <summary>停止 BGM（淡出）</summary>
    public void StopBGM()
    {
        StartCoroutine(FadeOut(activeBgmSource, bgmFadeDuration));
    }

    // ══════════════════════════════════════════
    //  EventBus 回调
    // ══════════════════════════════════════════

    private void OnPlayerHit(object _)          => PlaySFX(sfxHit);
    private void OnPlayerDied(object _)         => PlaySFX(sfxDeath);
    private void OnCoinCollected(object _)      => PlaySFX(sfxCoinCollect);
    private void OnCrownCollected(object _)     => PlaySFX(sfxCrownCollect);
    private void OnChestOpened(object _)        => PlaySFX(sfxChestOpen);
    private void OnFrequencyChanged(object _)   => PlaySFX(sfxFrequencyShift);
    private void OnShieldActivated(object _)    => PlaySFX(sfxShieldActivate);

    private void OnLevelCompleted(object _)
    {
        PlaySFX(sfxLevelComplete);
        // 切换到下一关的 BGM
        if (LevelManager.Instance != null)
            PlayBGM(LevelManager.Instance.CurrentLevelIndex);
    }

    // ══════════════════════════════════════════
    //  内部工具
    // ══════════════════════════════════════════

    /// <summary>从对象池取一个空闲 AudioSource 播放一次性音效</summary>
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        var source = GetFreeSFXSource();
        source.clip   = clip;
        source.volume = sfxVolume;
        source.pitch  = 1f;
        source.Play();
    }

    /// <summary>从对象池取一个空闲 AudioSource 播放一次性音效（可自定义音量和音高）</summary>
    private void PlaySFX(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;

        var source = GetFreeSFXSource();
        source.clip   = clip;
        source.volume = volume;
        source.pitch  = pitch;
        source.Play();
    }

    private AudioSource GetFreeSFXSource()
    {
        // 优先找空闲的
        foreach (var s in sfxPool)
            if (!s.isPlaying) return s;

        // 全忙 → 扩容一个
        var newSource = CreateAudioSource($"SFX_{sfxPool.Count}", false);
        sfxPool.Add(newSource);
        return newSource;
    }

    private AudioSource CreateAudioSource(string childName, bool loop)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        return src;
    }

    /// <summary>交叉淡入淡出切换 BGM</summary>
    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        var oldSource = activeBgmSource;
        var newSource = (activeBgmSource == bgmSourceA) ? bgmSourceB : bgmSourceA;
        activeBgmSource = newSource;

        newSource.clip   = newClip;
        newSource.volume = 0f;
        newSource.Play();

        float timer = 0f;
        while (timer < bgmFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / bgmFadeDuration;
            oldSource.volume = Mathf.Lerp(bgmVolume, 0f, t);
            newSource.volume = Mathf.Lerp(0f, bgmVolume, t);
            yield return null;
        }

        oldSource.Stop();
        oldSource.volume = 0f;
        newSource.volume = bgmVolume;
    }

    private IEnumerator FadeOut(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, timer / duration);
            yield return null;
        }
        source.Stop();
        source.volume = 0f;
    }
}
