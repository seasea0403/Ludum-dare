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

    [Header("生成参数")]
    [SerializeField] private int   minObstacles  = 2;
    [SerializeField] private int   maxObstacles  = 5;
    [SerializeField] private int   minCoins      = 3;
    [SerializeField] private int   maxCoins      = 8;
    [SerializeField] private float groundY       = 0.5f;
    [SerializeField] private float coinHeightMin = 1f;
    [SerializeField] private float coinHeightMax = 3f;
    [SerializeField, Range(0f, 1f)] private float chestChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float crownChance = 0.15f;  // 15% per chunk
    [SerializeField] private float crownHeight = 3.5f;

    private int totalCrownsSpawned = 0;
    private const int MAX_CROWNS = 3;

    [Header("白雾参数")]
    [SerializeField] private Sprite fogSprite;             // 白色圆形 Sprite
    [SerializeField, Range(0f, 1f)] private float fogChance = 0.4f;  // 40% 的物体被雾覆盖

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
        }
    }

    /// <summary>清除旧物体，随机生成新障碍物和金币</summary>
    void RespawnContent(Transform chunk, float chunkStartX)
    {
        // 回收上一轮物体到对象池
        if (spawnedObjects.ContainsKey(chunk))
        {
            foreach (var obj in spawnedObjects[chunk])
            {
                if (obj != null)
                {
                    // DestroyImmediate 确保 FogCover 立刻移除，避免池复用时残留
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

        // 记录已生成的"非金币"物体 X 坐标，用于距离检查
        List<float> occupiedX = new List<float>();
        float minDistance = 10f;  // X 间隔最小 10m

        // 在 chunk 范围内随机放障碍物
        int obstacleCount = Random.Range(minObstacles, maxObstacles + 1);
        float margin = 3f;
        for (int i = 0; i < obstacleCount; i++)
        {
            float x;
            int attempts = 0;
            do
            {
                x = Random.Range(chunkStartX + margin, chunkStartX + chunkWidth - margin);
                attempts++;
            } while (IsTooCloseToOthers(x, occupiedX, minDistance) && attempts < 10);

            if (attempts < 10 && obstaclePrefab)
            {
                var obj = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(obstaclePrefab, Vector3.zero, Quaternion.identity, chunk)
                    : Instantiate(obstaclePrefab, Vector3.zero, Quaternion.identity, chunk);
                obj.transform.position = new Vector3(x, -2f, 0f);
                TryAddFog(obj);
                spawnedObjects[chunk].Add(obj);
                occupiedX.Add(x);
            }
        }

        // 在 chunk 范围内随机放金币（金币无距离限制）
        int coinCount = Random.Range(minCoins, maxCoins + 1);
        for (int i = 0; i < coinCount; i++)
        {
            float x = Random.Range(chunkStartX + margin, chunkStartX + chunkWidth - margin);
            if (coinPrefab)
            {
                var obj = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(coinPrefab, Vector3.zero, Quaternion.identity, chunk)
                    : Instantiate(coinPrefab, Vector3.zero, Quaternion.identity, chunk);
                obj.transform.position = new Vector3(x, -2f, 0f);
                TryAddFog(obj);
                spawnedObjects[chunk].Add(obj);
            }
        }

        // 随机放宝箱（有距离限制）
        if (chestPrefab && Random.value <= chestChance)
        {
            float x;
            int attempts = 0;
            do
            {
                x = Random.Range(chunkStartX + margin, chunkStartX + chunkWidth - margin);
                attempts++;
            } while (IsTooCloseToOthers(x, occupiedX, minDistance) && attempts < 10);

            if (attempts < 10)
            {
                var obj = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(chestPrefab, Vector3.zero, Quaternion.identity, chunk)
                    : Instantiate(chestPrefab, Vector3.zero, Quaternion.identity, chunk);
                obj.transform.position = new Vector3(x, -2f, 0f);
                TryAddFog(obj);
                spawnedObjects[chunk].Add(obj);
                occupiedX.Add(x);
            }
        }

        // 随机放皇冠（上限3个，有距离限制）
        if (crownPrefab && totalCrownsSpawned < MAX_CROWNS && Random.value <= crownChance)
        {
            float x;
            int attempts = 0;
            do
            {
                x = Random.Range(chunkStartX + margin, chunkStartX + chunkWidth - margin);
                attempts++;
            } while (IsTooCloseToOthers(x, occupiedX, minDistance) && attempts < 10);

            if (attempts < 10)
            {
                var obj = ObjectPool.Instance
                    ? ObjectPool.Instance.Get(crownPrefab, Vector3.zero, Quaternion.identity, chunk)
                    : Instantiate(crownPrefab, Vector3.zero, Quaternion.identity, chunk);
                obj.transform.position = new Vector3(x, -2f, 0f);
                spawnedObjects[chunk].Add(obj);
                occupiedX.Add(x);
                totalCrownsSpawned++;
            }
        }
    }

    /// <summary>检查 X 坐标是否过于接近其他物体</summary>
    bool IsTooCloseToOthers(float x, List<float> occupiedXList, float minDistance)
    {
        foreach (float ox in occupiedXList)
        {
            if (Mathf.Abs(x - ox) < minDistance)
                return true;
        }
        return false;
    }

    /// <summary>随机给物体加上白雾</summary>
    void TryAddFog(GameObject obj)
    {
        if (Random.value > fogChance) return;

        // 确保没有残留的 FogCover（对象池复用时可能存在）
        var existing = obj.GetComponent<FogCover>();
        if (existing != null) return;  // 已有则跳过，避免重复

        var fog = obj.AddComponent<FogCover>();
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
