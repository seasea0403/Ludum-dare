using System;
using System.Collections.Generic;

public static class EventBus
{
    private static readonly Dictionary<string, Action<object>> events = new();

    public static void Subscribe(string eventName, Action<object> callback)
    {
        if (!events.ContainsKey(eventName))
            events[eventName] = null;
        events[eventName] += callback;
    }

    public static void Unsubscribe(string eventName, Action<object> callback)
    {
        if (events.ContainsKey(eventName))
            events[eventName] -= callback;
    }

    public static void Publish(string eventName, object data = null)
    {
        if (events.ContainsKey(eventName))
            events[eventName]?.Invoke(data);
    }
}

public static class GameEvents
{
    public const string PlayerHit         = "PlayerHit";
    public const string PlayerDied        = "PlayerDied";
    public const string CoinCollected     = "CoinCollected";
    public const string FrequencyChanged  = "FrequencyChanged";
    public const string LevelCompleted    = "LevelCompleted";
    public const string DistanceUpdated   = "DistanceUpdated";
    public const string CrownCollected    = "CrownCollected";
    public const string CrownCountChanged = "CrownCountChanged";
    public const string ChestOpened       = "ChestOpened";
    public const string ShieldActivated   = "ShieldActivated";
    public const string ShieldBroken      = "ShieldBroken";

    // 关卡系统
    public const string SceneSegmentChanged = "SceneSegmentChanged";
    public const string TransitionStart     = "TransitionStart";
    public const string GameCompleted       = "GameCompleted";

    // 最终关照片收集
    public const string PhotoCollected      = "PhotoCollected";
    public const string AllPhotosCollected  = "AllPhotosCollected";
}