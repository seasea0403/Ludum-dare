using UnityEngine;

/// <summary>
/// 最终关控制器
/// 在固定场景中生成 5 个照片泡泡，全部收集后生成小猫
/// 挂在场景中的空物体上
/// </summary>
public class FinalLevelController : MonoBehaviour
{
    public static FinalLevelController Instance { get; private set; }

    private LevelData levelData;
    private GameObject catInstance;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.AllPhotosCollected, OnAllPhotosCollected);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.AllPhotosCollected, OnAllPhotosCollected);
    }

    /// <summary>由 LevelManager.BeginLevel 调用，生成泡泡</summary>
    public void Init(LevelData data)
    {
        levelData = data;
        SpawnBubbles();
    }

    /// <summary>清理最终关生成的物体（重试时调用）</summary>
    public void Cleanup()
    {
        // 清理泡泡
        foreach (var bubble in FindObjectsOfType<PhotoBubble>())
            Destroy(bubble.gameObject);

        // 清理猫
        if (catInstance != null)
        {
            Destroy(catInstance);
            catInstance = null;
        }
    }

    void SpawnBubbles()
    {
        if (levelData == null || levelData.photoBubblePrefab == null) return;

        int count = 5;
        // 活动范围 10~70，均匀分布
        float startX = 10f;
        float endX   = 70f;
        float spacing = (endX - startX) / (count - 1);

        Sprite[] photos = levelData.photoSprites;

        for (int i = 0; i < count; i++)
        {
            float x = startX + spacing * i;
            Vector3 pos = new Vector3(x, -1f, 0f);
            var obj = Instantiate(levelData.photoBubblePrefab, pos, Quaternion.identity);
            obj.SetActive(true);

            var bubble = obj.GetComponent<PhotoBubble>();
            if (bubble != null)
            {
                Sprite sp = (photos != null && i < photos.Length) ? photos[i] : null;
                bubble.Init(i, sp);
            }

            // 给泡泡加 FogCover
            var fog = obj.GetComponent<FogCover>();
            if (fog == null) fog = obj.AddComponent<FogCover>();

            // 设置 fogSprite
            var chunker = FindObjectOfType<TerrainChunker>();
            if (chunker != null && chunker.FogSprite != null)
                fog.SetFogSprite(chunker.FogSprite);
        }
    }

    void OnAllPhotosCollected(object _)
    {
        SpawnCat();
    }

    void SpawnCat()
    {
        if (levelData == null || levelData.catPrefab == null) return;

        // 猫生成在场景末端
        Vector3 pos = new Vector3(75f, -2f, 0f);
        catInstance = Instantiate(levelData.catPrefab, pos, Quaternion.identity);
        catInstance.SetActive(true);

        // 确保猫有 Trigger Collider
        var col = catInstance.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // 猫碰到玩家 → 游戏通关
        var trigger = catInstance.GetComponent<CatTrigger>();
        if (trigger == null) trigger = catInstance.AddComponent<CatTrigger>();
    }
}
