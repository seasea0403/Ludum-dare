using System.Collections;
using UnityEngine;

/// <summary>
/// Laser beam: starts slowly, then accelerates and concentrates on target.
/// Shows line renderer trace as it flies.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    [Header("Laser Flight")]
    [SerializeField] private float startSpeed     = 3f;
    [SerializeField] private float maxSpeed       = 20f;
    [SerializeField] private float accelerationDuration = 0.3f;
    [SerializeField] private float maxTravelDistance = 20f;

    [Header("Line Renderer")]
    [SerializeField] private float beamWidth     = 0.08f;
    [SerializeField] private Color beamColor     = new Color(1f, 0.3f, 0.1f, 0.9f);
    [SerializeField] private LayerMask hitLayers;

    private LineRenderer lr;
    private bool isFlying;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null)
            lr = gameObject.AddComponent<LineRenderer>();

        lr.positionCount   = 2;
        lr.startWidth      = beamWidth;
        lr.endWidth        = beamWidth * 0.5f;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.startColor      = beamColor;
        lr.endColor        = new Color(beamColor.r, beamColor.g, beamColor.b, 0.2f);
        lr.sortingOrder    = 100;
        lr.enabled         = false;
    }

    /// <summary>Fire laser from origin towards right</summary>
    public bool Fire(Vector3 origin)
    {
        // 强制停止上一次射击的残留协程，重置状态
        StopAllCoroutines();
        isFlying = false;
        lr.enabled = false;

        // 重置颜色（FadeLaser 可能改过）
        lr.startColor = beamColor;
        lr.endColor   = new Color(beamColor.r, beamColor.g, beamColor.b, 0.2f);

        isFlying = true;
        StartCoroutine(FlyAndHit(origin));
        return true;
    }

    IEnumerator FlyAndHit(Vector3 origin)
    {
        lr.enabled = true;

        // 瞬间 Raycast，无飞行延迟
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right, maxTravelDistance, hitLayers);

        Vector3 endPos;
        if (hit.collider != null)
        {
            endPos = (Vector3)hit.point;

            if (hit.collider.CompareTag("Obstacle"))
            {
                var fog = hit.collider.GetComponent<FogCover>();
                if (fog == null || fog.IsRevealed)
                {
                    var obs = hit.collider.GetComponent<Obstacle>();
                    if (obs != null)
                        obs.Shatter();
                }
            }
        }
        else
        {
            endPos = origin + Vector3.right * maxTravelDistance;
        }

        // 画出射线
        lr.SetPosition(0, origin);
        lr.SetPosition(1, endPos);

        // 等一帧让射线可见，然后淡出
        yield return null;

        StartCoroutine(FadeLaser());
        isFlying = false;
    }

    IEnumerator FadeLaser()
    {
        float duration = 0.2f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            Color c = beamColor;
            c.a = Mathf.Lerp(beamColor.a, 0f, t);
            lr.startColor = c;
            lr.endColor   = new Color(c.r, c.g, c.b, c.a * 0.3f);

            yield return null;
        }

        lr.enabled = false;
    }
}

