using System.Collections;
using UnityEngine;

/// <summary>
/// Signal Wave: forward-moving wave that clears fog in its path.
/// Expands forward instead of radially.
/// </summary>
public class SignalWave : MonoBehaviour
{
    [SerializeField] private float expandSpeed = 8f;
    [SerializeField] private float waveWidth = 1.5f;

    private float maxDistance;
    private float currentDistance;
    private SpriteRenderer sr;
    private FogManager fogManager;

    public void Init(float radius)
    {
        maxDistance = radius;
        currentDistance = 0f;
        sr = GetComponent<SpriteRenderer>();
        fogManager = FindObjectOfType<FogManager>();

        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
        }

        // 每次都强制创建并赋值白色 Sprite（避免预制体已有 Sprite 导致条件跳过）
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color[] pixels = new Color[64];
        for (int i = 0; i < 64; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 1f);

        // Green color for detection signal
        sr.color = new Color(0f, 1f, 0.3f, 0.6f);
        sr.sortingOrder = 10;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.enabled = true;

        // 重置 scale
        transform.localScale = new Vector3(0.1f, waveWidth, 1f);

        StartCoroutine(ExpandWave());
    }

    IEnumerator ExpandWave()
    {
        while (currentDistance < maxDistance)
        {
            currentDistance += expandSpeed * Time.deltaTime;
            float t = currentDistance / maxDistance;

            // Scale: wave expands forward (X) and width (Y)
            float waveScale = Mathf.Lerp(0.1f, 1f, t);
            transform.localScale = new Vector3(waveScale, waveWidth, 1f);

            // Fade out
            Color c = sr.color;
            c.a = Mathf.Lerp(0.4f, 0f, t);
            sr.color = c;

            // Pulse outward — clear fog ahead
            if (fogManager != null)
            {
                Vector3 wavePos = transform.position + Vector3.right * (currentDistance - expandSpeed * Time.deltaTime);
                fogManager.EmitPulseAtPoint(wavePos, waveWidth * 0.5f);
            }

            yield return null;
        }

        // Return to pool
        if (ObjectPool.Instance)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }
}

