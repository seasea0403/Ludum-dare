using UnityEngine;

public enum SpawnType { Obstacle, CoinCluster, Chest, Crown, Book, Boss }

[System.Serializable]
public class SpawnEntry
{
    public SpawnType type;

    [Tooltip("是否有雾遮罩（true=有雾，false=无雾）")]
    public bool hasFog;

    [Tooltip("是否为不可摧毁障碍物")]
    public bool isIndestructible;

    public int   coinCount  = 5;
    public float arcHeight  = 2f;

    [Tooltip("是否在物件周围生成金币")]
    public bool hasCoinAround;

    [Tooltip("物件前后各生成的金币数量")]
    public int coinAroundCount = 3;

    [Tooltip("围绕金币的弧度高度（0 = 无弧度贯穿，>0 = 弧形跳跃引导）")]
    public float coinAroundArcHeight = 0f;

    [Tooltip("围绕金币的间距")]
    public float coinAroundSpacing = 1.5f;
}

/// <summary>
/// 生成序列配置 —— 定义关卡中物体出现的顺序与概率
/// 在 Inspector 中逐条设计出现顺序，TerrainChunker 按序读取
/// </summary>
[CreateAssetMenu(fileName = "SpawnPattern", menuName = "Game/Spawn Pattern")]
public class SpawnPattern : ScriptableObject
{
    [Tooltip("到末尾后是否从头循环")]
    public bool loop = true;

    [Tooltip("非金币物体之间的最小间距")]
    public float minSpacing = 11f;

    [Tooltip("非金币物体之间的最大间距")]
    public float maxSpacing = 20f;

    [Tooltip("金币簇内金币间距")]
    public float coinSpacing = 1.5f;

    [Tooltip("金币簇之间的最小距离（防重叠）")]
    public float minCoinClusterDistance = 3f;

    [Tooltip("生成序列（按顺序出现）")]
    public SpawnEntry[] entries;
}
