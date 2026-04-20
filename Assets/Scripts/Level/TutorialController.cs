using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教学关卡控制器
/// 管理分段引导流程：E切换 → Q探测 → Q+E+Q击碎 → Space跳跃 → Book通关
/// 挂在场景中的空物体上
/// </summary>
public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }

    /// <summary>教学阶段</summary>
    public enum TutorialPhase
    {
        None,
        WalkToFirstPrompt,
        TeachFirstE,
        TeachFirstQ,
        WaitCollectFirstCrown,
        WalkToRedObstacle,
        TeachRedE,
        TeachRedQ,
        WaitDestroyRedObstacle,
        WalkToFogObstacle,
        TeachFogE,
        TeachFogQ,
        TeachFogEBack,
        TeachFogQDestroy,
        WaitDestroyFogObstacle,
        WalkToCoinLine,
        WalkToSecondCrown,
        WalkToJumpObstacle,
        TeachSpace,
        WalkToChest,
        WalkToThirdCrown,
        WalkToBook,
        Completed
    }

    public TutorialPhase CurrentPhase { get; private set; } = TutorialPhase.None;

    [Header("教学物体预制体")]
    [SerializeField] private GameObject crownPrefab;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject bookPrefab;

    [Header("关键位置 X 坐标")]
    [SerializeField] private float firstCrownX = 16f;
    [SerializeField] private float redObstacleX = 34f;
    [SerializeField] private float fogObstacleX = 50f;
    [SerializeField] private float coinStartX = 62f;
    [SerializeField] private int coinCount = 8;
    [SerializeField] private float coinSpacing = 1.6f;
    [SerializeField] private float secondCrownX = 79f;
    [SerializeField] private float jumpObstacleX = 90f;
    [SerializeField] private float chestX = 102f;
    [SerializeField] private float thirdCrownX = 112f;
    [SerializeField] private float bookX = 122f;

    [Header("触发点 X 坐标")]
    [SerializeField] private float triggerFirstTeachX = 10f;
    [SerializeField] private float triggerRedTeachX = 30f;
    [SerializeField] private float triggerFogTeachX = 46f;
    [SerializeField] private float triggerJumpTeachX = 86f;

    [Header("白雾 Sprite")]
    [SerializeField] private Sprite fogSprite;

    private LevelData levelData;
    private PlayerController player;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private bool isActive;

    // 缓存关键物体引用，用于流程判定
    private GameObject firstCrown;
    private GameObject redObstacle;
    private GameObject fogObstacle;
    private GameObject secondCrown;
    private GameObject thirdCrown;

    private int crownCollectedCount;
    private bool coinHintShown;
    private bool chestOpened;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>初始化教学关卡，生成所有障碍物和书本</summary>
    public void Init(LevelData data)
    {
        levelData = data;
        player = FindObjectOfType<PlayerController>();
        isActive = true;
        CurrentPhase = TutorialPhase.WalkToFirstPrompt;
        crownCollectedCount = 0;
        coinHintShown = false;
        chestOpened = false;

        if (player)
        {
            player.isTutorialInputRestricted = true;
            player.isTutorialPaused = false;
        }

        // 第一段要求先按 E 再按 Q(蓝波)，因此开局强制到低频，让 E 切回高频
        if (player != null && player.CurrentFrequency == SignalFrequency.High)
            player.TutorialForceSwitch();

        if (fogSprite == null)
        {
            var chunker = FindObjectOfType<TerrainChunker>();
            if (chunker != null) fogSprite = chunker.FogSprite;
        }

        SpawnTutorialObjects();

        if (TutorialGuideUI.Instance)
            TutorialGuideUI.Instance.ShowMessageTransient("Tutorial Start", 1.0f);
    }

    /// <summary>清理教学关生成的所有物体</summary>
    public void Cleanup()
    {
        isActive = false;
        CurrentPhase = TutorialPhase.None;

        foreach (var obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();
        firstCrown = null;
        redObstacle = null;
        fogObstacle = null;
        secondCrown = null;
        thirdCrown = null;

        if (player)
        {
            player.isTutorialPaused = false;
            player.isTutorialInputRestricted = false;
        }

        if (TutorialGuideUI.Instance)
            TutorialGuideUI.Instance.HideAll();
    }

    void SpawnTutorialObjects()
    {
        // 1) 泡泡皇冠（先 E 再 Q 去除泡泡后收集）
        if (crownPrefab)
        {
            firstCrown = Instantiate(crownPrefab, new Vector3(firstCrownX, -1.4f, 0f), Quaternion.identity);
            ApplyFog(firstCrown);
            spawnedObjects.Add(firstCrown);
        }

        // 2) 可摧毁障碍物（无泡泡，E+Q 红波击碎）
        if (obstaclePrefab)
        {
            redObstacle = Instantiate(obstaclePrefab, new Vector3(redObstacleX, -2f, 0f), Quaternion.identity);
            ConfigureObstacle(redObstacle, false);
            spawnedObjects.Add(redObstacle);
        }

        // 3) 泡泡里的可摧毁障碍物（先去泡泡，再切红波摧毁）
        if (obstaclePrefab)
        {
            fogObstacle = Instantiate(obstaclePrefab, new Vector3(fogObstacleX, -2f, 0f), Quaternion.identity);
            ConfigureObstacle(fogObstacle, false);
            ApplyFog(fogObstacle);
            spawnedObjects.Add(fogObstacle);
        }

        // 4) 一串金币（不暂停）
        if (coinPrefab)
        {
            for (int i = 0; i < coinCount; i++)
            {
                float x = coinStartX + i * coinSpacing;
                var coin = Instantiate(coinPrefab, new Vector3(x, -1f, 0f), Quaternion.identity);
                spawnedObjects.Add(coin);
            }
        }

        // 5) 第二个皇冠
        if (crownPrefab)
        {
            secondCrown = Instantiate(crownPrefab, new Vector3(secondCrownX, -1.4f, 0f), Quaternion.identity);
            spawnedObjects.Add(secondCrown);
        }

        // 6) 不可摧毁障碍物（跳跃教学）
        if (obstaclePrefab)
        {
            var jumpObstacle = Instantiate(obstaclePrefab, new Vector3(jumpObstacleX, -2f, 0f), Quaternion.identity);
            ConfigureObstacle(jumpObstacle, true);
            spawnedObjects.Add(jumpObstacle);
        }

        // 7) 宝箱
        if (chestPrefab)
        {
            var chest = Instantiate(chestPrefab, new Vector3(chestX, -2f, 0f), Quaternion.identity);
            spawnedObjects.Add(chest);
        }

        // 8) 第三个皇冠
        if (crownPrefab)
        {
            thirdCrown = Instantiate(crownPrefab, new Vector3(thirdCrownX, -1.4f, 0f), Quaternion.identity);
            spawnedObjects.Add(thirdCrown);
        }

        // 9) 终点书本
        if (bookPrefab)
        {
            var book = Instantiate(bookPrefab, new Vector3(bookX, -2f, 0f), Quaternion.identity);
            spawnedObjects.Add(book);
        }
    }

    private void ConfigureObstacle(GameObject obstacleObj, bool isIndestructible)
    {
        if (obstacleObj == null) return;

        var obs = obstacleObj.GetComponent<Obstacle>();
        if (obs == null) return;

        obs.SetIndestructible(isIndestructible);

        if (levelData == null || levelData.segmentA == null) return;

        if (isIndestructible)
        {
            var sprites = levelData.segmentA.indestructibleObstacleSprites;
            if (sprites != null && sprites.Length > 0)
                obs.SetVariants(new Sprite[] { sprites[0] });
        }
        else
        {
            var sprites = levelData.segmentA.destructibleObstacleSprites;
            if (sprites != null && sprites.Length > 0)
                obs.SetVariants(new Sprite[] { sprites[0] });
        }
    }

    private void ApplyFog(GameObject target)
    {
        if (target == null) return;
        var fog = target.GetComponent<FogCover>();
        if (fog == null) fog = target.AddComponent<FogCover>();
        if (fogSprite != null) fog.SetFogSprite(fogSprite);
    }

    private bool IsDestroyed(GameObject go)
    {
        return go == null || !go.activeInHierarchy;
    }

    void Update()
    {
        if (!isActive || player == null) return;

        float px = player.transform.position.x;

        switch (CurrentPhase)
        {
            // 1) 泡泡皇冠教学：先 E 再 Q
            case TutorialPhase.WalkToFirstPrompt:
                if (px >= triggerFirstTeachX)
                {
                    CurrentPhase = TutorialPhase.TeachFirstE;
                    PausePlayer();
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.E, "Press E to switch weapon");
                }
                break;

            case TutorialPhase.TeachFirstE:
                if (Input.GetKeyDown(KeyCode.E))
                {
                    player.TutorialForceSwitch();
                    CurrentPhase = TutorialPhase.TeachFirstQ;
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.Q, "Press Q to attack — blue wave reveals and removes bubbles!");
                }
                break;

            case TutorialPhase.TeachFirstQ:
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    player.TutorialForceSignal();
                    CurrentPhase = TutorialPhase.WaitCollectFirstCrown;
                    ResumePlayer();
                    if (TutorialGuideUI.Instance) TutorialGuideUI.Instance.HideAll();
                }
                break;

            case TutorialPhase.WaitCollectFirstCrown:
                // 等待 CrownCollected 事件推动
                break;

            // 2) 红波摧毁普通可摧毁障碍物
            case TutorialPhase.WalkToRedObstacle:
                if (px >= triggerRedTeachX)
                {
                    CurrentPhase = TutorialPhase.TeachRedE;
                    PausePlayer();
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.E, "Press E to switch to red wave");
                }
                break;

            case TutorialPhase.TeachRedE:
                if (Input.GetKeyDown(KeyCode.E))
                {
                    player.TutorialForceSwitch();
                    CurrentPhase = TutorialPhase.TeachRedQ;
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.Q, "Press Q — red wave destroys obstacles!");
                }
                break;

            case TutorialPhase.TeachRedQ:
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    player.TutorialForceAttack();
                    CurrentPhase = TutorialPhase.WaitDestroyRedObstacle;
                    if (TutorialGuideUI.Instance) TutorialGuideUI.Instance.HideAll();
                }
                break;

            case TutorialPhase.WaitDestroyRedObstacle:
                if (IsDestroyed(redObstacle))
                {
                    CurrentPhase = TutorialPhase.WalkToFogObstacle;
                    ResumePlayer();
                }
                break;

            // 3) 泡泡里的危险障碍物：先去泡泡，再切红波摧毁
            case TutorialPhase.WalkToFogObstacle:
                if (px >= triggerFogTeachX)
                {
                    CurrentPhase = TutorialPhase.TeachFogE;
                    PausePlayer();

                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.E, "Danger inside! Press E to switch to blue wave first");
                }
                break;

            case TutorialPhase.TeachFogE:
                if (Input.GetKeyDown(KeyCode.E))
                {
                    player.TutorialForceSwitch();
                    CurrentPhase = TutorialPhase.TeachFogQ;
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.Q, "Press Q to scan with blue wave and remove the bubble");
                }
                break;

            case TutorialPhase.TeachFogQ:
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    player.TutorialForceSignal();
                    CurrentPhase = TutorialPhase.TeachFogEBack;
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.E, "Now press E to switch back to red wave");
                }
                break;

            case TutorialPhase.TeachFogEBack:
                if (Input.GetKeyDown(KeyCode.E))
                {
                    player.TutorialForceSwitch();
                    CurrentPhase = TutorialPhase.TeachFogQDestroy;
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.Q, "Press Q to destroy the obstacle");
                }
                break;

            case TutorialPhase.TeachFogQDestroy:
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    player.TutorialForceAttack();
                    CurrentPhase = TutorialPhase.WaitDestroyFogObstacle;
                    if (TutorialGuideUI.Instance) TutorialGuideUI.Instance.HideAll();
                }
                break;

            case TutorialPhase.WaitDestroyFogObstacle:
                if (IsDestroyed(fogObstacle))
                {
                    CurrentPhase = TutorialPhase.WalkToCoinLine;
                    ResumePlayer();
                }
                break;

            // 4) 金币段（不暂停，文字提示）
            case TutorialPhase.WalkToCoinLine:
                if (px >= secondCrownX - 2f)
                    CurrentPhase = TutorialPhase.WalkToSecondCrown;
                break;

            // 5) 第二个皇冠
            case TutorialPhase.WalkToSecondCrown:
                if (crownCollectedCount >= 2)
                    CurrentPhase = TutorialPhase.WalkToJumpObstacle;
                break;

            // 6) 不可摧毁障碍 + Space 跳跃教学
            case TutorialPhase.WalkToJumpObstacle:
                if (px >= triggerJumpTeachX)
                {
                    CurrentPhase = TutorialPhase.TeachSpace;
                    PausePlayer();
                    if (TutorialGuideUI.Instance)
                        TutorialGuideUI.Instance.ShowKeyWithMessage(TutorialGuideUI.KeyType.Space, "Press SPACE to jump — hold longer to jump higher!");
                }
                break;

            case TutorialPhase.TeachSpace:
                if (Input.GetButtonDown("Jump"))
                {
                    CurrentPhase = TutorialPhase.WalkToChest;
                    ResumePlayer();
                    player.TutorialForceJump();
                    if (TutorialGuideUI.Instance) TutorialGuideUI.Instance.HideAll();
                }
                break;

            // 7) 宝箱
            case TutorialPhase.WalkToChest:
                if (chestOpened)
                    CurrentPhase = TutorialPhase.WalkToThirdCrown;
                break;

            // 8) 第三个皇冠
            case TutorialPhase.WalkToThirdCrown:
                if (crownCollectedCount >= 3)
                    CurrentPhase = TutorialPhase.WalkToBook;
                break;

            // 9) 走向终点书本
            case TutorialPhase.WalkToBook:
                break;
        }
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.CrownCollected, OnCrownCollected);
        EventBus.Subscribe(GameEvents.CoinCollected, OnCoinCollected);
        EventBus.Subscribe(GameEvents.ChestOpened, OnChestOpened);
        EventBus.Subscribe(GameEvents.LevelCompleted, OnTutorialBookTouched);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.CrownCollected, OnCrownCollected);
        EventBus.Unsubscribe(GameEvents.CoinCollected, OnCoinCollected);
        EventBus.Unsubscribe(GameEvents.ChestOpened, OnChestOpened);
        EventBus.Unsubscribe(GameEvents.LevelCompleted, OnTutorialBookTouched);
    }

    void OnCrownCollected(object _)
    {
        if (!isActive) return;

        crownCollectedCount++;

        if (TutorialGuideUI.Instance)
            TutorialGuideUI.Instance.ShowMessageTransient("Crown collected!", 1.2f);

        if (CurrentPhase == TutorialPhase.WaitCollectFirstCrown && crownCollectedCount >= 1)
        {
            CurrentPhase = TutorialPhase.WalkToRedObstacle;
        }
    }

    void OnCoinCollected(object _)
    {
        if (!isActive || coinHintShown) return;

        if (CurrentPhase == TutorialPhase.WalkToCoinLine ||
            CurrentPhase == TutorialPhase.WalkToSecondCrown ||
            CurrentPhase == TutorialPhase.WalkToJumpObstacle)
        {
            coinHintShown = true;
            if (TutorialGuideUI.Instance)
                TutorialGuideUI.Instance.ShowMessageTransient("Coin collected!", 1.0f);
        }
    }

    void OnChestOpened(object _)
    {
        if (!isActive) return;
        chestOpened = true;

        if (TutorialGuideUI.Instance)
            TutorialGuideUI.Instance.ShowMessageTransient("Lots of coins!", 1.2f);
    }

    void OnTutorialBookTouched(object _)
    {
        if (!isActive || CurrentPhase != TutorialPhase.WalkToBook) return;
        CurrentPhase = TutorialPhase.Completed;
        isActive = false;

        if (player)
        {
            player.isTutorialPaused = false;
            player.isTutorialInputRestricted = false;
        }

        if (TutorialGuideUI.Instance)
            TutorialGuideUI.Instance.HideAll();
    }

    void PausePlayer()
    {
        if (player) player.isTutorialPaused = true;
    }

    void ResumePlayer()
    {
        if (player) player.isTutorialPaused = false;
    }
}
