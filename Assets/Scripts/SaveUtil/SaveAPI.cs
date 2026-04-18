using System;
using System.IO;
using UnityEngine;

public static class SaveAPI
{
    [Serializable]
    private class SaveData
    {
        public int highestReached = 0;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "levels_save.json");
    private const string PlayerPrefsKey = "levels_save_json";

    // 按平台保存（WebGL 使用 PlayerPrefs）
    private static void SaveDataByPlatform(SaveData data)
    {
        if (data == null) return;
        string json = JsonUtility.ToJson(data, true);

#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
#else
        try
        {
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveAPI: 保存失败：{ex.Message}");
        }
#endif
    }

    // 按平台加载
    private static SaveData LoadDataInternal()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            return new SaveData();
        string json = PlayerPrefs.GetString(PlayerPrefsKey);
#else
        if (!File.Exists(FilePath))
            return new SaveData();
        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveAPI: 读取失败：{ex.Message}");
            return new SaveData();
        }
#endif
        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            return data ?? new SaveData();
        }
        catch
        {
            return new SaveData();
        }
    }

    // 设置并保存玩家到达的最高关卡索引（0-5），仅在 index 大于已有值时更新
    public static void SetReachedLevel(int index)
    {
        index = Mathf.Clamp(index, 0, 5);
        var data = LoadDataInternal();
        if (index > data.highestReached)
        {
            data.highestReached = index;
            SaveDataByPlatform(data);
        }
    }

    // 读取保存的最高关卡索引（-1 表示未到达任何关卡）
    public static int GetReachedLevel()
    {
        var data = LoadDataInternal();
        return data.highestReached;
    }

    // 清除存档（调试用）
    public static void ClearAll()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
#else
        if (File.Exists(FilePath)) File.Delete(FilePath);
#endif
        Debug.Log("SaveAPI: cleared");
    }
}