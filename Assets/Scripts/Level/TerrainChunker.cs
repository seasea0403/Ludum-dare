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

    [Header("白雾 Sprite")]
    [SerializeField] private Sprite fogSprite;

    private int totalCrownsSpawned;
    private const int MAX_CROWNS = 3;
    private SpawnPattern currentPattern;
    private int spawnCursor;

    private Camera cam;

    // 每个 chunk 下动态生成的物体，回收时销毁
    private Dictionary<Transform, List<GameObject>> spawnedObjects = new Dictionary<Transform, List<GameObject>>();

    void Start()
    {
        cam = Camera.main;
        foreach (var c in chunks)
            spawnedObjects[c] = new List<GameObject>();
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
    void RespawnContent(Transform chunk, float chunkStartX)
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

        // 切换关卡/Pattern 时重置游标
        if (pattern != currentPattern)
        {
            currentPattern = pattern;
            spawnCursor = 0;
        }

        float margin = 3f;
        float cursor = chunkStartX + margin;
        float chunkEnd = chunkStartX + chunkWidth - margin;

        while (cursor < chunkEnd && HasMoreEntries(pattern))
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
                float clusterWidth = SpawnCoinCluster(chunk, cursor, entry, pattern.coinSpacing);
                cursor += clusterWidth + 2f;
            }
            else
            {
                SpawnSingleObject(chunk, cursor, entry);
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

    void SpawnSingleObject(Transform chunk, float x, SpawnEntry entry)
    {
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
                LevelManager.Instance.GetObstacleConfig(out Sprite sp, out bool indestructible);
                if (sp != null)
                    obs.SetVariants(new Sprite[] { sp });
                obs.SetIndestructible(indestructible);
            }
        }

        if (entry.type == SpawnType.Crown)
            totalCrownsSpawned++;

        TryAddFog(obj, entry.hasFog);
        spawnedObjects[chunk].Add(obj);
    }

    GameObject GetPrefab(SpawnType type)
    {
        switch (type)
        {
            case SpawnType.Obstacle: return obstaclePrefab;
            case SpawnType.Chest:    return chestPrefab;
            case SpawnType.Crown:    return crownPrefab;
            default:                 return null;
        }
    }

    /// <summary>生成一簇弧形排列的金币，返回簇的总宽度</summary>
    float SpawnCoinCluster(Transform chunk, float startX, SpawnEntry entry, float spacing)
    {
        int count = Mathf.Max(1, entry.coinCount);
        float arcH = entry.arcHeight;

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
        return (count - 1) * spacing;
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
