using System.Collections;
using UnityEngine;

public class RunnerCamera : MonoBehaviour
{
    public static RunnerCamera Instance { get; private set; }

    [Header("速度")]
    [SerializeField] private float scrollSpeed = 4f;

    [Header("Follow")]
    [SerializeField] private Transform player;
    [SerializeField] private float fixedY = 0f;  // 固定Y坐标

    public float CurrentSpeed => scrollSpeed;

    private Camera cam;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (player == null) return;
        // 水平：跟随玩家X，让玩家始终在屏幕左侧偏移 2 单位处
        float halfWidth = cam.orthographicSize * cam.aspect;
        float offset = halfWidth - 3f; // 玩家距屏幕左边缘 3 单位
        float newX = player.position.x + offset;
        // 垂直：固定不变
        transform.position = new Vector3(newX, fixedY, transform.position.z);
    }

    public float RightEdgeX => transform.position.x + cam.orthographicSize * cam.aspect;
    public float LeftEdgeX  => transform.position.x - cam.orthographicSize * cam.aspect;

    // 屏幕震动
    public void Shake(float strength = 0.2f, float duration = 0.15f)
    {
        StartCoroutine(ShakeRoutine(strength, duration));
    }

    IEnumerator ShakeRoutine(float strength, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float decay = 1f - t;
            Vector3 offset = (Vector3)Random.insideUnitCircle * strength * decay;
            transform.position += offset;
            yield return null;
        }
    }
}