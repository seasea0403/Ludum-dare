using UnityEngine;

/// <summary>
/// 关卡流程控制器
/// 管理 5 个关卡的推进，驱动 TerrainChunker 和 ParallaxBackground 切换场景段
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("关卡配置（按顺序放入 5 个 LevelData SO）")]
    [SerializeField] private LevelData[] levels;

    /// <summary>当前关卡索引 (0~4)</summary>
    public int CurrentLevelIndex { get; private set; }

    /// <summary>当前场景段 (A=0, B=1)</summary>
    public int CurrentSegmentPhase { get; private set; }

    /// <summary>当前场景段已循环的次数</summary>
    public int CurrentCycleCount { get; private set; }

    /// <summary>当前生效的场景段数据</summary>
    public SceneSegment CurrentSegment { get; private set; }

    /// <summary>当前关卡数据</summary>
    public LevelData CurrentLevel => (levels != null && CurrentLevelIndex < levels.Length)
        ? levels[CurrentLevelIndex] : null;

    private int targetCycleCount;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 不自动启动，由 MainMenuUI 调用 LoadLevel 触发
    }

    /// <summary>显示关卡介绍面板，点击 Next 后真正开始关卡</summary>
    private void ShowIntroThenStart()
    {
        if (CurrentLevel == null) return;

        if (AudioManager.Instance) AudioManager.Instance.SetGameplaySfxBlocked(true);

        if (LevelIntroUI.Instance)
        {
            LevelIntroUI.Instance.Show(CurrentLevel, BeginLevel);
        }
        else
        {
            // 没有介绍面板时直接开始
            BeginLevel();
        }
    }

    /// <summary>真正启动关卡（介绍面板关闭后调用）</summary>
    private void BeginLevel()
    {
        if (CurrentLevel == null) return;

        if (AudioManager.Instance) AudioManager.Instance.SetGameplaySfxBlocked(false);

        CurrentSegmentPhase = 0;
        CurrentCycleCount = 0;
        CurrentSegment = CurrentLevel.segmentA;
        targetCycleCount = CurrentSegment.cycleCount;
        lastDestructibleIndex   = -1;
        lastIndestructibleIndex = -1;

        // 确保血量 HUD 与实际数值同步（防止过关/重来后 UI 未刷新）
        var player = FindObjectOfType<PlayerController>();
        if (player)
        {
            EventBus.Publish(GameEvents.PlayerHit,          player.CurrentHealth);
            EventBus.Publish(GameEvents.CoinCollected,      player.CoinCount);
            EventBus.Publish(GameEvents.CrownCountChanged,  player.CrownCount);
        }

        // 播放当前关卡字幕
        var subtitle = FindObjectOfType<SubtitlePlayer>(true);
        if (subtitle && CurrentLevel.subtitleLines != null && CurrentLevel.subtitleLines.Length > 0)
        {
            subtitle.Play(CurrentLevel.subtitleLines);
        }

        // 播放当前关卡 BGM
        if (AudioManager.Instance) AudioManager.Instance.PlayBGM(CurrentLevelIndex);

        // 设置玩家的最终关模式
        if (player) player.isFinalLevel = CurrentLevel.isFinalLevel;

        if (CurrentLevel.isFinalLevel)
        {
            // ——— 最终关特殊流程 ———
            // 不生成地形障碍物
            var finalCtrl = FindObjectOfType<FinalLevelController>(true);
            if (finalCtrl) finalCtrl.Init(CurrentLevel);

            // UI 切换：隐藏 NormalHUD，显示 PhotoHUD
            var normalHUD = GameObject.Find("NormalHUD");
            if (normalHUD) normalHUD.SetActive(false);

            var photoHUD = FindObjectOfType<PhotoHUD>(true);
            if (photoHUD)
            {
                photoHUD.gameObject.SetActive(true);
                photoHUD.Init(CurrentLevel.photoSprites);
            }

            EventBus.Publish(GameEvents.SceneSegmentChanged, CurrentSegment);
        }
        else
        {
            // ——— 普通关卡流程 ———
            // 确保 NormalHUD 显示，PhotoHUD 隐藏
            var normalHUD = GameObject.Find("NormalHUD");
            if (normalHUD) normalHUD.SetActive(true);

            var photoHUD = FindObjectOfType<PhotoHUD>(true);
            if (photoHUD) photoHUD.gameObject.SetActive(false);

            // CurrentSegment 已设好，现在再生成初始地形内容，确保 obstacle 有正确的 sprite
            var chunker = FindObjectOfType<TerrainChunker>();
            if (chunker) chunker.SpawnInitialContent();

            EventBus.Publish(GameEvents.SceneSegmentChanged, CurrentSegment);
        }
    }

    /// <summary>
    /// 由 TerrainChunker 每次回收地形块时调用，推进循环计数
    /// </summary>
    public void OnChunkRecycled()
    {
        if (CurrentLevel == null) return;

        CurrentCycleCount++;

        if (CurrentCycleCount >= targetCycleCount)
        {
            AdvancePhase();
        }
    }

    void AdvancePhase()
    {
        if (CurrentSegmentPhase == 0)
        {
            // A 段结束 → 进入 B 段
            CurrentSegmentPhase = 1;
            CurrentCycleCount = 0;
            CurrentSegment = CurrentLevel.segmentB;
            targetCycleCount = CurrentSegment.cycleCount;
            lastDestructibleIndex   = -1;
            lastIndestructibleIndex = -1;

            EventBus.Publish(GameEvents.SceneSegmentChanged, CurrentSegment);
        }
        else if (CurrentSegmentPhase == 1)
        {
            // B 段结束 → 关卡完成
            if (CurrentLevelIndex < levels.Length - 1)
            {
                // 发布通关事件，不立即启动下一关
                // GameOverUI 会弹出结算面板，玩家点击"下一关"后触发 LoadNextLevel
                EventBus.Publish(GameEvents.LevelCompleted, CurrentLevelIndex);
            }
            else
            {
                // 全部通关
                EventBus.Publish(GameEvents.GameCompleted, null);
            }
        }
    }

    private int lastDestructibleIndex   = -1;
    private int lastIndestructibleIndex = -1;

    /// <summary>根据指定的可摧毁性，从当前场景段获取对应 Sprite（相邻不重复）</summary>
    public void GetObstacleConfig(bool isIndestructible, out Sprite sprite)
    {
        sprite = null;
        if (CurrentSegment == null) return;

        if (isIndestructible)
        {
            var arr = CurrentSegment.indestructibleObstacleSprites;
            if (arr == null || arr.Length == 0) return;
            int idx = PickNonRepeat(arr.Length, ref lastIndestructibleIndex);
            sprite = arr[idx];
        }
        else
        {
            var arr = CurrentSegment.destructibleObstacleSprites;
            if (arr == null || arr.Length == 0) return;
            int idx = PickNonRepeat(arr.Length, ref lastDestructibleIndex);
            sprite = arr[idx];
        }
    }

    /// <summary>从 count 个元素中随机选一个与 lastIndex 不同的索引</summary>
    private int PickNonRepeat(int count, ref int lastIndex)
    {
        if (count == 1) { lastIndex = 0; return 0; }
        int idx;
        do { idx = Random.Range(0, count); } while (idx == lastIndex);
        lastIndex = idx;
        return idx;
    }

    /// <summary>获取当前背景 Sprite</summary>
    public Sprite GetCurrentBackgroundSprite()
    {
        return CurrentSegment?.backgroundSprite;
    }

    /// <summary>是否还有下一关</summary>
    public bool HasNextLevel => levels != null && CurrentLevelIndex < levels.Length - 1;

    /// <summary>重试当前关卡（无介绍面板）</summary>
    public void ReloadLevel()
    {
        if (AudioManager.Instance) AudioManager.Instance.SetGameplaySfxBlocked(false);
        ResetGameState();
        BeginLevel();
    }

    /// <summary>加载下一关（先显示介绍面板）</summary>
    public void LoadNextLevel()
    {
        if (!HasNextLevel) return;
        CurrentLevelIndex++;
        if (AudioManager.Instance) AudioManager.Instance.SetGameplaySfxBlocked(true);
        ResetGameState();
        ShowIntroThenStart();
    }

    /// <summary>加载指定关卡（先显示介绍面板）</summary>
    public void LoadLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length) return;
        CurrentLevelIndex = index;
        if (AudioManager.Instance) AudioManager.Instance.SetGameplaySfxBlocked(true);
        ResetGameState();
        ShowIntroThenStart();
    }

    /// <summary>原地重置玩家和地形，不需要切换场景</summary>
    private void ResetGameState()
    {
        Time.timeScale = 1f;

        // 停止正在播放的字幕
        var subtitle = FindObjectOfType<SubtitlePlayer>(true);
        if (subtitle) subtitle.Stop();

        // 清理残留的 Boss 和 Boss 子弹
        foreach (var boss in FindObjectsOfType<Boss>()) Destroy(boss.gameObject);
        foreach (var bullet in FindObjectsOfType<BossBullet>()) Destroy(bullet.gameObject);

        // 清理最终关的泡泡和猫
        var finalCtrl = FindObjectOfType<FinalLevelController>(true);
        if (finalCtrl) finalCtrl.Cleanup();

        var chunker = FindObjectOfType<TerrainChunker>();
        if (chunker) chunker.ResetChunks();

        var player = FindObjectOfType<PlayerController>();
        if (player) player.ResetState();

        var playerAnim = FindObjectOfType<PlayerAnimator>();
        if (playerAnim) playerAnim.ResetState();

        var parallax = FindObjectOfType<ParallaxBackground>();
        if (parallax) parallax.ResetBackground();
    }
}
