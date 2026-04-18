using System.Collections;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [Header("Random Appearance")]
    [SerializeField] private Sprite[] variants;

    [Header("Shatter Settings")]
    [SerializeField] private int   fragmentsPerAxis = 3;   // 3x3 = 9 fragments
    [SerializeField] private float shatterForce     = 5f;
    [SerializeField] private float fragmentLifeTime = 0.8f;
    [SerializeField] private Color fragmentTint     = new Color(1f, 0.6f, 0.2f, 1f);

    private SpriteRenderer sr;
    private Collider2D col;
    private bool isShattered;

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        isShattered = false;
        if (sr) sr.enabled = true;
        if (col) col.enabled = true;

        // Random sprite each time
        if (sr != null && variants != null && variants.Length > 0)
            sr.sprite = variants[Random.Range(0, variants.Length)];
    }

    /// <summary>Called by laser — break into fragments then return to pool</summary>
    public void Shatter()
    {
        if (isShattered) return;
        isShattered = true;

        // Hide the original
        if (sr) sr.enabled = false;
        if (col) col.enabled = false;

        // Spawn fragment sprites
        SpawnFragments();

        // Return to pool after fragments are done
        StartCoroutine(ReturnAfterDelay(fragmentLifeTime + 0.1f));
    }

    /// <summary>Called by bullet (legacy) — just return to pool</summary>
    public void onDestr()
    {
        if (ObjectPool.Instance)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    void SpawnFragments()
    {
        if (sr == null || sr.sprite == null) { onDestr(); return; }

        Sprite sprite  = sr.sprite;
        Rect texRect   = sprite.textureRect;
        Texture2D tex  = sprite.texture;
        float ppu      = sprite.pixelsPerUnit;

        float fragW = texRect.width  / fragmentsPerAxis;
        float fragH = texRect.height / fragmentsPerAxis;
        float worldW = fragW / ppu;
        float worldH = fragH / ppu;

        Vector3 origin = transform.position;
        // Offset so fragments appear centered on original
        Vector3 startOffset = new Vector3(
            -(texRect.width / ppu) * 0.5f + worldW * 0.5f,
            -(texRect.height / ppu) * 0.5f + worldH * 0.5f,
            0f
        );

        for (int y = 0; y < fragmentsPerAxis; y++)
        {
            for (int x = 0; x < fragmentsPerAxis; x++)
            {
                // Create fragment sprite from sub-region
                Rect subRect = new Rect(
                    texRect.x + x * fragW,
                    texRect.y + y * fragH,
                    fragW, fragH
                );
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                Sprite fragSprite = Sprite.Create(tex, subRect, pivot, ppu);

                Vector3 pos = origin + startOffset + new Vector3(x * worldW, y * worldH, 0f);

                var fragObj = new GameObject("Frag");
                fragObj.transform.position = pos;

                var fragSR = fragObj.AddComponent<SpriteRenderer>();
                fragSR.sprite       = fragSprite;
                fragSR.color        = fragmentTint;
                fragSR.sortingOrder = sr.sortingOrder + 1;

                var fragRB = fragObj.AddComponent<Rigidbody2D>();
                fragRB.gravityScale = 2f;
                // Scatter outward from center
                Vector2 dir = ((Vector2)(pos - origin)).normalized + Random.insideUnitCircle * 0.5f;
                fragRB.velocity = dir * shatterForce;
                fragRB.angularVelocity = Random.Range(-360f, 360f);

                // Fade & destroy
                StartCoroutine(FadeFragment(fragObj, fragSR, fragmentLifeTime));
            }
        }
    }

    IEnumerator FadeFragment(GameObject obj, SpriteRenderer fragSR, float duration)
    {
        float timer = 0f;
        Color startColor = fragSR.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            // Shrink and fade
            if (obj == null) yield break;
            obj.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t);
            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            fragSR.color = c;

            yield return null;
        }

        if (obj != null) Destroy(obj);
    }

    IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ObjectPool.Instance)
            ObjectPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerController player = collision.collider.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
            player.TakeDamage();
    }
}
