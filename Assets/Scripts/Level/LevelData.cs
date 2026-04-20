using UnityEngine;


/// <summary>
/// 一个关卡内的单个场景段配置
/// </summary>
[System.Serializable]
public class SceneSegment
{
    [Tooltip("该场景段的背景 Sprite")]
    public Sprite backgroundSprite;

    [Tooltip("可摧毁障碍物的 Sprite 变体")]
    public Sprite[] destructibleObstacleSprites;

    [Tooltip("不可摧毁障碍物的 Sprite 变体")]
    public Sprite[] indestructibleObstacleSprites;

    [Tooltip("该场景段地形块循环次数（之后切换到下一段或下一关）")]
    public int cycleCount = 10;
}

/// <summary>
/// 单个关卡的完整配置（ScriptableObject）
/// 只包含 2 个场景段，无过渡画面
/// </summary>
[CreateAssetMenu(fileName = "Level_01", menuName = "Game/Level Data")]
public class LevelData : ScriptableObject
{
    [Tooltip("关卡编号（1~5）")]
    public int levelIndex;

    [Header("关卡前序介绍")]
    [Tooltip("介绍页面显示的动画控制器（用于 Image 的 Animator）")]
    public RuntimeAnimatorController introAnimator;

    [Tooltip("介绍页面显示的 Sprite（无动画时作为静态图片）")]
    public Sprite introImage;

    [Tooltip("介绍页面的文字内容")]
    [TextArea(3, 6)]
    public string introText;

    [Tooltip("拍照后结果页背景色")]
    public Color introResultBgColor = Color.white;

    [Tooltip("拍立得相纸 Sprite（拍照后从相机滑出）")]
    public Sprite polaroidSprite;

    [Header("关卡字幕")]
    [Tooltip("该关卡游戏中滚动播放的字幕台词序列")]
    public SubtitlePlayer.SubtitleLine[] subtitleLines;

    [Tooltip("场景段 A（先播放）")]
    public SceneSegment segmentA;

    [Tooltip("场景段 B（衔接后播放）")]
    public SceneSegment segmentB;

    [Tooltip("该关卡的生成序列（控制障碍物/金币/宝箱/皇冠出现顺序）")]
    public SpawnPattern spawnPattern;

    [Header("最终关卡配置")]
    [Tooltip("勾选后启用最终关特殊逻辑（手动移动/照片收集/无伤害）")]
    public bool isFinalLevel;

    [Tooltip("5张彩色照片（收集后显示）")]
    public Sprite[] photoSprites;

    [Tooltip("照片泡泡预制体")]
    public GameObject photoBubblePrefab;

    [Tooltip("小猫预制体（收集完全部照片后出现）")]
    public GameObject catPrefab;
}
