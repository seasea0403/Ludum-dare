using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 迷雾管理器：场景默认全亮，部分物体被白雾遮住
/// 高频脉冲到达范围内的 FogCover 会被清除
/// 挂在任意空 GameObject 上即可
/// </summary>
public class FogManager : MonoBehaviour
{
    public static FogManager Instance { get; private set; }

    [Header("脉冲参数")]
    [SerializeField] private float pulseRadius = 8f;

    // 所有活跃的雾注册到这里
    private readonly List<FogCover> activeFogs = new List<FogCover>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(FogCover fog)   => activeFogs.Add(fog);
    public void Unregister(FogCover fog) => activeFogs.Remove(fog);

    /// <summary>以 center 为圆心，清除半径内所有白雾</summary>
    public void EmitPulse(Vector3 center, float radius)
    {
        float r2 = radius * radius;
        for (int i = activeFogs.Count - 1; i >= 0; i--)
        {
            if (i >= activeFogs.Count) continue;
            var fog = activeFogs[i];
            if (fog == null) { activeFogs.RemoveAt(i); continue; }

            float dist2 = (fog.transform.position - center).sqrMagnitude;
            if (dist2 <= r2)
            {
                fog.Reveal();   // 播放消散动画并移除
            }
        }
    }

    /// <summary>供 PlayerController 调用（兼容旧接口名）</summary>
    public void EmitPulse(Vector3 center)
    {
        EmitPulse(center, pulseRadius);
    }
}
