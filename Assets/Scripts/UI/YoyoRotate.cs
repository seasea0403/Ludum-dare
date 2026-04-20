using UnityEngine;

/// <summary>
/// 让物体在Y轴上来回慢速旋转（摆动）
/// </summary>
public class YoyoRotate : MonoBehaviour
{
    public float angle = 20f; // 最大旋转角度
    public float speed = 0.2f; // 摆动速度（周期越大越慢）
    private float t;

    void Update()
    {
        t += Time.deltaTime * speed;
        float y = Mathf.Sin(t) * angle;
        transform.localRotation = Quaternion.Euler(0, y, 0);
    }
}
