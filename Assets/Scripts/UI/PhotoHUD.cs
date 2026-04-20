using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最终关的照片收集 HUD
/// 5 个槽位显示灰色/彩色照片，点击已收集照片可查看大图
/// 全部收集后广播 AllPhotosCollected
/// </summary>
public class PhotoHUD : MonoBehaviour
{
    public static PhotoHUD Instance { get; private set; }

    [Header("照片槽位（5 个 Image，按顺序）")]
    [SerializeField] private Image[] photoSlots;

    [Header("大图预览面板")]
    [SerializeField] private GameObject previewPanel;
    [SerializeField] private Image      previewImage;
    [SerializeField] private Button     previewCloseButton;

    private Sprite[] colorSprites;
    private Sprite[] graySprites;   // 运行时从彩色图生成
    private bool[] collected;
    private int collectedCount;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        EventBus.Subscribe(GameEvents.PhotoCollected, OnPhotoCollected);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEvents.PhotoCollected, OnPhotoCollected);
    }

    /// <summary>由 LevelManager 在最终关开始时调用，初始化槽位</summary>
    public void Init(Sprite[] photos)
    {
        colorSprites = photos;
        collectedCount = 0;

        int count = Mathf.Min(photoSlots.Length, photos != null ? photos.Length : 0);
        collected = new bool[count];

        // 从彩色图生成灰度 Sprite
        graySprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            if (photos[i] != null)
                graySprites[i] = CreateGrayscaleSprite(photos[i]);
        }

        for (int i = 0; i < photoSlots.Length; i++)
        {
            if (photoSlots[i] == null) continue;

            if (i < count && graySprites[i] != null)
                photoSlots[i].sprite = graySprites[i];

            // 绑定点击事件
            int idx = i; // 闭包捕获
            var btn = photoSlots[i].GetComponent<Button>();
            if (btn == null) btn = photoSlots[i].gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSlotClicked(idx));
        }

        if (previewPanel) previewPanel.SetActive(false);
        if (previewCloseButton)
            previewCloseButton.onClick.AddListener(() => previewPanel.SetActive(false));
    }

    /// <summary>将一张彩色 Sprite 转为灰度 Sprite（CPU 像素操作）</summary>
    static Sprite CreateGrayscaleSprite(Sprite src)
    {
        var srcTex = src.texture;

        // 如果纹理不可读，创建一份可读副本
        Texture2D readable;
        if (!srcTex.isReadable)
        {
            RenderTexture rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0);
            Graphics.Blit(srcTex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readable = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, srcTex.width, srcTex.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        else
        {
            readable = srcTex;
        }

        var pixels = readable.GetPixels();
        for (int j = 0; j < pixels.Length; j++)
        {
            float gray = pixels[j].r * 0.299f + pixels[j].g * 0.587f + pixels[j].b * 0.114f;
            pixels[j] = new Color(gray, gray, gray, pixels[j].a);
        }

        var grayTex = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, false);
        grayTex.SetPixels(pixels);
        grayTex.Apply();

        // 用原 Sprite 的 rect 和 pivot 创建新 Sprite
        Rect rect = src.rect;
        Vector2 pivot = new Vector2(
            src.pivot.x / src.rect.width,
            src.pivot.y / src.rect.height);

        return Sprite.Create(grayTex, new Rect(0, 0, grayTex.width, grayTex.height),
            pivot, src.pixelsPerUnit);
    }

    void OnPhotoCollected(object data)
    {
        int index = (int)data;
        if (collected == null || index < 0 || index >= collected.Length) return;
        if (collected[index]) return;

        collected[index] = true;
        collectedCount++;

        // 槽位从灰色变彩色
        if (index < photoSlots.Length && photoSlots[index] != null
            && colorSprites != null && index < colorSprites.Length)
        {
            photoSlots[index].sprite = colorSprites[index];
        }

        // 全部收集完
        if (collectedCount >= collected.Length)
        {
            EventBus.Publish(GameEvents.AllPhotosCollected, null);
        }
    }

    void OnSlotClicked(int index)
    {
        if (collected == null || index < 0 || index >= collected.Length) return;
        if (!collected[index]) return; // 未收集的不响应

        if (previewPanel && previewImage && colorSprites != null && index < colorSprites.Length)
        {
            previewImage.sprite = colorSprites[index];
            previewPanel.SetActive(true);
        }
    }
}
