using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地形块循环复用器
/// 当最左块完全离开屏幕后，自动移到最右块的右边
/// 并随机刷新障碍物和金币
/// </summary>
public class TerrainChunker : MonoBehaviour
{
    [Header("地形块列表（2~3 个 Grid GameObject）")]
    [SerializeField] private List<Transform> chunks;

    [Header("单块宽度（单位：Unity 米）")]
    [SerializeField] private float chunkWidth = 32f;

    [Header("提前回收的缓冲距离")]
    [SerializeField] private float screenBuffer = 2f;

    [Header("障碍物/金币预制体")]
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject crownPrefab;
    [SerializeField] private GameObject bookPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("白雾 Sprite")]
    [SerializeField] private Sprite fogSprite;
    public Sprite FogSprite => fogSprite;

    private int totalCrownsSpawned;
    private const int MAX_CROWNS = 3;
    private SpawnPattern currentPattern;
    private int spawnCursor;
    private float lastCoinEndX = float.NegativeInfinity;
    private bool spawnPaused;
    private bool bossPending; // Boss entry 已解析，等待下一个 chunk 生成

    /// <summary>暂停/恢复物体生成（Boss 用）</summary>
    public void SetSpawnPaused(bool paused) => spawnPaused = paused;

    private Camera cam;
    private List<Vector3> initialChunkPositions = new List<Vector3>();

    // 每个 chunk 下动态生成的物体，回收时销毁
    private Dictionary<Transform, List<GameObject>> spawnedObjects = new Dictionary<Transform, List<GameObject>>();

    void Start()
    {
        cam = Camera.main;
        foreach (var c in chunks)
        {
            spawnedObjects[c] = new List<GameObject>();
            initialChunkPositions.Add(c.position); // 记录初始位置
        }
    }

    /// <summary>重置所有 chunk 到初始位置并清空生成物</summary>
    public void ResetChunks()
    {
        // 回收所有已生成物体
        foreach (var c in chunks)
        {
            if (spawnedObjects.ContainsKey(c))
            {
                foreach (var obj in spawnedObjects[c])
                {
                    if (obj != null)
                    {
                        var fogs = obj.GetComponents<FogCover>();
                        foreach (var fog in fogs)
                            DestroyImmediate(fog);
                        if (ObjectPool.Instance)
                            ObjectPool.Instance.Return(obj);
                        else
                            Destroy(obj);
                    }
                }
                spawnedObjects[c].Clear();
            }
        }
        // 恢复 chunk 初始位置
        for (int i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] != null && i < initialChunkPositions.Count)
                chunks[i].position = initialChunkPositions[i];
        }
        // 重置生成状态
        spawnCursor = 0;
        currentPattern = null;
        totalCrownsSpawned = 0;
        lastCoinEndX = float.NegativeInfinity;
        spawnPaused = false;
        bossPending = false;
        Physics2D.SyncTransforms();
        // 初始内容由 LevelManager.BeginLevel 在 CurrentSegment 设好后再调用 SpawnInitialContent()
    }

    /// <summary>在 CurrentSegment 设置后调用，生成初始地形内容</summary>
    public void SpawnInitialContent()
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] != null)
                RespawnContent(chunks[i], chunks[i].position.x, 15f);
        }
    }

    void LateUpdate()
    {
        if (chunks == null || chunks.Count < 2) return;

        float camLeft = cam.transform.position.x
                        - cam.orthographicSize * cam.aspect
                        - screenBuffer;

        Transform leftmost  = null;
        Transform rightmost = null;
        float minX =  float.MaxValue;
        float maxX = -float.MaxValue;

        foreach (var c in chunks)
        {
            if (c == null) continue;
            float x = c.position.x;
            if (x < minX) { minX = x; leftmost  = c; }
            if (x > maxX) { maxX = x; rightmost = c; }
        }

        if (leftmost == null || rightmost == null) return;

        if (leftmost.position.x + chunkWidth < camLeft)
        {
            float newX = rightmost.position.x + chunkWidth;
            leftmost.position = new Vector3(newX, leftmost.position.y, leftmost.position.z);
            Physics2D.SyncTransforms();   // 强制同步碰撞体位置
            RespawnContent(leftmost, newX);

            // 通知 LevelManager 完成一次循环
            if (LevelManager.Instance)
                LevelManager.Instance.OnChunkRecycled();
        }
    }

    /// <summary>清除旧物体，按 SpawnPattern 生成新内容</summary>
    void RespawnContent(Transform chunk, float chunkStartX, float safeStartX = float.NegativeInfinity)
    {
        // 回收上一轮物体到对象池
        if (spawnedObjects.ContainsKey(chunk))
        {
            foreach (var obj in spawnedObjects[chunk])
            {
                if (obj != null)
                {
                    var fogs = obj.GetComponents<FogCover>();
                    foreach (var fog in fogs)
                        DestroyImmediate(fog);
                    if (ObjectPool.Instance)
                        ObjectPool.Instance.Return(obj);
                    else
                        Destroy(obj);
                }
            }
            spawnedObjects[chunk].Clear();
        }

        // 获取当前关卡的 SpawnPattern
        SpawnPattern pattern = null;
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            pattern = LevelManager.Instance.CurrentLevel.spawnPattern;

        if (pattern == null || pattern.entries == null || pattern.entries.Length == 0)
            return;

        // Boss 存活期间暂停生成新物体
        if (spawnPaused) return;

        // 上一个 chunk 解析到了 Boss entry，现在新 chunk 开头生成 Boss
        if (bossPending)
        {
            bossPending = false;
            SpawnBoss(chunk, chunkStartX);
            return; // Boss chunk 不生成其他物体
        }

        // 切换关卡/Pattern 时重置游标
        if (pattern != currentPattern)
        {
            currentPattern = pattern;
            spawnCursor = 0;
        }

        float margin = 3f;
        float cursor = chunkStartX + margin;
        
        // 应用安全距离（例如刚开局的 15 米内不刷怪，防初见杀）
        if (cursor < safeStartX)
            cursor = safeStartX;

        float chunkEnd = chunkStartX + chunkWidth - margin;

        while (cursor < chunkEnd && HasMoreEntries(pattern) && !spawnPaused)
        {
            SpawnEntry entry = pattern.entries[spawnCursor];

            // 皇冠已达上限时跳过
            if (entry.type == SpawnType.Crown && totalCrownsSpawned >= MAX_CROWNS)
            {
                AdvanceCursor(pattern);
                continue;
            }

            if (entry.type == SpawnType.CoinCluster)
            {
                float clusterWidth = SpawnCoinCluster(chunk, cursor, entry, pattern.coinSpacing, pattern.minCoinClusterDistance);
                cursor += clusterWidth + 2f;
            }
            else
            {
                SpawnSingleObject(chunk, cursor, entry, pattern.minCoinClusterDistance);
                cursor += Random.Range(pattern.minSpacing, pattern.maxSpacing);
            }

            AdvanceCursor(pattern);
        }
    }

    bool HasMoreEntries(SpawnPattern pattern)
    {
        if (pattern.loop) return true;
        return spawnCursor < pattern.entries.Length;
    }

    void AdvanceCursor(SpawnPattern pattern)
    {
        spawnCursor++;
        if (pattern.loop && spawnCursor >= pattern.entries.Length)
            spawnCursor = 0;
    }

    void SpawnSingleObject(Transform chunk, float x, SpawnEntry entry, float minCoinDist)
    {
        // Boss 特殊处理：不在当前 chunk 生成，标记 pending，等下一个 chunk 时生成
        // 这样前序物体会先滚出屏幕，不会重叠
        if (entry.type == SpawnType.Boss)
        {
            bossPending = true;
            return;
        }

        GameObject prefab = GetPrefab(entry.type);
        if (prefab == null) return;

        Vector3 pos = new Vector3(x, -2f, 0f);
        var obj = ObjectPool.Instance
            ? ObjectPool.Instance.Get(prefab, pos, Quaternion.identity, chunk)
            : Instantiate(prefab, pos, Quaternion.identity, chunk);

        if (entry.type == SpawnType.Obstacle)
        {
            var obs = obj.GetComponent<Obstacle>();
            if (obs != null && LevelManager.Instance)
            {
                LevelManager.Instance.GetObstacleConfig(entry.isIndestructible, out Sprite sp);
                if (sp != null)
                    obs.SetVariants(new Sprite[] { sp });
                obs.SetIndestructible(entry.isIndestructible);
            }
        }

        if (entry.type == SpawnType.Crown)
            totalCrownsSpawned++;

        TryAddFog(obj, entry.hasFog);
        spawnedObjects[chunk].Add(obj);

        // 围绕物件生成金币
        if (entry.hasCoinAround)
            SpawnCoinAround(chunk, x, entry, minCoinDist);
    }

    GameObject GetPrefab(SpawnType type)
    {
        switch (type)
        {
            case SpawnType.Obstacle: return obstaclePrefab;
            case SpawnType.Chest:    return chestPrefab;
            case SpawnType.Crown:    return crownPrefab;
            case SpawnType.Book:     return bookPrefab;
            default:                 return null;
        }
    }

    /// <summary>生成 Boss（不走对象池，挂在场景根而非地形块下）</summary>
    private void SpawnBoss(Transform chunk, float x)
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("TerrainChunker: bossPrefab 未设置！Boss 不会生成。");
            return;
        }

        // Boss 生成在屏幕右侧外一点，并以玩家速度向右移动，看起来固定在画面上
        float spawnX = x; // 使用 pattern 给出的世界坐标
        if (cam != null)
        {
            float rightEdge = cam.transform.position.x + cam.orthographicSize * cam.aspect;
            spawnX = rightEdge + 1f; // 从屏幕右侧外一点滑入
        }
        Vector3 pos = new Vector3(spawnX, -1f, 0f);
        var obj = Instantiate(bossPrefab, pos, Quaternion.identity);

        // 把 fogSprite 传给 Boss，让子弹的 FogCover 能正确显示
        var boss = obj.GetComponent<Boss>();
        if (boss != null && fogSprite != null)
            boss.SetFogSprite(fogSprite);

        Debug.Log($"SpawnBoss: 已生成 Boss 在 ({pos.x:F1}, {pos.y:F1})");

        // Boss 存活期间暂停物体生成
        spawnPaused = true;
    }

    /// <summary>生成一簇弧形排列的金币，返回簇的总宽度</summary>
    float SpawnCoinCluster(Transform chunk, float startX, SpawnEntry entry, float spacing, float minCoinDist)
    {
        int count = Mathf.Max(1, entry.coinCount);
        float arcH = entry.arcHeight;
        float clusterEnd = startX + (count - 1) * spacing;

        // 与上一个金币簇重叠检测
        if (startX - lastCoinEndX < minCoinDist)
            return 0f;

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;
            float y = -2f + arcH * Mathf.Sin(t * Mathf.PI);
            float x = startX + i * spacing;

            if (coinPrefab)
            {
                Vector3 pos = new Vector3(x, y, 0f);
                var obj = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(coinPrefab, pos, Quaternion.identity, chunk)
                    : Instantiate(coinPrefab, pos, Quaternion.identity, chunk);
                TryAddFog(obj, entry.hasFog);
                spawnedObjects[chunk].Add(obj);
            }
        }

        lastCoinEndX = clusterEnd;
        return (count - 1) * spacing;
    }

    /// <summary>围绕物件生成金币（以物件为中心，前后各 coinAroundCount 枚）</summary>
    void SpawnCoinAround(Transform chunk, float centerX, SpawnEntry entry, float minCoinDist)
    {
        if (!coinPrefab) return;

        int perSide   = Mathf.Max(1, entry.coinAroundCount);
        float spacing = entry.coinAroundSpacing;
        float arcH    = entry.coinAroundArcHeight;
        int totalCoins = perSide * 2;

        float halfSpan = perSide * spacing;
        float coinStartX = centerX - halfSpan;
        float coinEndX   = centerX + halfSpan;

        // 与上一个金币簇重叠检测
        if (coinStartX - lastCoinEndX < minCoinDist)
            return;

        for (int i = 0; i < totalCoins; i++)
        {
            // 均匀分布，跳过正中心（障碍物所在处）
            float x;
            if (i < perSide)
                x = coinStartX + i * spacing;
            else
                x = centerX + (i - perSide + 1) * spacing;

            // t ∈ [0,1]，用于计算弧度
            float t = (float)i / (totalCoins - 1);
            float y = -2f + arcH * Mathf.Sin(t * Mathf.PI);

            Vector3 pos = new Vector3(x, y, 0f);
            var obj = ObjectPool.Instance
                ? ObjectPool.Instance.Get(coinPrefab, pos, Quaternion.identity, chunk)
                : Instantiate(coinPrefab, pos, Quaternion.identity, chunk);
            
            // 独立的金币簇有雾根据配置决定，但作为引导围绕着障碍物生成的辅助金币默认不加雾
            TryAddFog(obj, false);
            
            spawnedObjects[chunk].Add(obj);
        }

        lastCoinEndX = coinEndX;
    }

    /// <summary>根据 hasFog 给物体加上白雾</summary>
    void TryAddFog(GameObject obj, bool hasFog)
    {
        var existing = obj.GetComponent<FogCover>();

        if (!hasFog)
        {
            if (existing != null) Destroy(existing);
            return;
        }

        var fog = existing != null ? existing : obj.AddComponent<FogCover>();
        if (fogSprite) fog.SetFogSprite(fogSprite);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (chunks == null) return;
        Gizmos.color = Color.cyan;
        foreach (var c in chunks)
        {
            if (c == null) continue;
            Vector3 center = c.position + new Vector3(chunkWidth * 0.5f, 2f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(chunkWidth, 4f, 0f));
        }
    }
#endif
}
