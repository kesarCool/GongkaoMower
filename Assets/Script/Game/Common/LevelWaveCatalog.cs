#if USE_FB_TABLE
using ProtoTable;
#endif
using UnityEngine;

/// <summary><c>LevelWave</c> 配表查询（波次数量、本关是否有配置）。</summary>
public static class LevelWaveCatalog
{
#if USE_FB_TABLE
    /// <summary>LevelWave 表是否已加载且非空。</summary>
    public static bool IsTableLoaded()
    {
        if (TableManager.Instance == null)
            return false;
        var dict = TableManager.Instance.GetTable<LevelWave>();
        return dict != null && dict.Count > 0;
    }

    /// <summary>指定 <paramref name="levelId"/> 在表中的波次行数。</summary>
    public static int CountWavesForLevel(int levelId)
    {
        if (levelId <= 0 || !IsTableLoaded())
            return 0;

        int count = 0;
        var dict = TableManager.Instance.GetTable<LevelWave>();
        foreach (var kv in dict)
        {
            if (kv.Value is LevelWave lw && lw.levelId == levelId)
                count++;
        }

        return count;
    }
#else
    public static bool IsTableLoaded() => false;

    public static int CountWavesForLevel(int levelId) => 0;
#endif

    public static bool HasWavesForLevel(int levelId) => CountWavesForLevel(levelId) > 0;
}
