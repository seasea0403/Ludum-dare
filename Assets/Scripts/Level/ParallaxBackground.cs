using UnityEngine;

/// <summary>
/// 背景无限滚动：多张背景图拼接循环
/// 挂在一个空父物体上，子物体放 2~3 张相同宽度的背景 SpriteRenderer
/// </summary>
[DefaultExecutionOrder(100)] // 确保背景在 RunnerCamera 更新位置之后再执行视差计算，根除高移速引发的抖动
public class ParallaxBackground : MonoBehaviour
{
    [Header("背景块（按从左到右顺序放入）")]
    [SerializeField] private Transform[] bgPanels;

    [Header("单张背景宽度（和 Sprite 实际世界宽度一致）")]
    [SerializeField] private float panelWidth = 20f;

    [Header("视差系数（0=不动，1=和摄像机同速）")]
    [SerializeField] private float parallaxFactor = 0.5f;

    private SpriteRenderer[] panelRenderers;
    private float currentPanelWidth;       // 当前生效的背景宽度
    private Camera cam;
    private float lastCamX;
    private Vector3[] initialPanelPositions;

    void Awake()
    {
        panelRenderers = new SpriteRenderer[bgPanels.Length];
        initialPanelPositions = new Vector3[bgPanels.Length];

        for (int i = 0; i < bgPanels.Length; i++)
        {
            panelRenderers[i] = bgPanels[i].GetComponent<SpriteRenderer>();
            initialPanelPositions[i] = bgPanels[i].position;
        }
    }

    /// <summary>重置背景板到初始位置（跟随 LevelManager 一起调用）</summary>
    public void ResetBackground()
    {
        if (cam != null)
            lastCamX = cam.transform.position.x;
            
        for (int i = 0; i < bgPanels.Length; i++)
        {
            if (bgPanels[i] != null)
                bgPanels[i].position = initialPanelPositions[i];
        }
    }

    void Start()
    {
        cam = Camera.main;
        lastCamX = cam.transform.position.x;
        currentPanelWidth = panelWidth;

        // 主动从 LevelManager 获取初始背景，防止错过事件
        if (LevelManager.Instance != null)
        {
            Sprite bg = LevelManager.Instance.GetCurrentBackgroundSprite();
            if (bg != null) SetAllPanelSprites(bg);
        }
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.SceneSegmentChanged, OnSegmentChanged);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.SceneSegmentChanged, OnSegmentChanged);
    }

    void OnSegmentChanged(object data)
    {
        if (data is SceneSegment segment && segment.backgroundSprite != null)
        {
            SetAllPanelSprites(segment.backgroundSprite);
            currentPanelWidth = panelWidth;
        }
    }

    void SetAllPanelSprites(Sprite sprite)
    {
        if (panelRenderers == null) return;
        foreach (var sr in panelRenderers)
            if (sr) sr.sprite = sprite;
    }

    void LateUpdate()
    {
        float camX = cam.transform.position.x;
        float deltaX = camX - lastCamX;
        lastCamX = camX;

        // 按视差系数移动背景
        for (int i = 0; i < bgPanels.Length; i++)
        {
            bgPanels[i].position += Vector3.right * deltaX * parallaxFactor;
        }

        // 找到最左和最右的面板
        float camLeft = camX - cam.orthographicSize * cam.aspect - panelWidth;

        Transform leftmost = null;
        Transform rightmost = null;
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = 0; i < bgPanels.Length; i++)
        {
            float x = bgPanels[i].position.x;
            if (x < minX) { minX = x; leftmost = bgPanels[i]; }
            if (x > maxX) { maxX = x; rightmost = bgPanels[i]; }
        }

        // 最左面板超出屏幕左边时，移到最右面板右边
        if (leftmost != null && rightmost != null)
        {
            if (leftmost.position.x + currentPanelWidth * 0.5f < camLeft)
            {
                float rightEdge = rightmost.position.x + currentPanelWidth;
                leftmost.position = new Vector3(rightEdge, leftmost.position.y, leftmost.position.z);
            }
        }
    }
}
