#if USE_FB_TABLE
using ProtoTable;
#endif
using UnityEngine;

/// <summary>
/// 由 <c>LevelWave</c> 表汇总本关计划刷怪数量（用于 HUD 目标击杀等）。
/// </summary>
public static class LevelWaveKillQuota
{
    /// <summary>表不可用或无数据时返回 0。</summary>
    public static int SumTotalMonstersForLevel(int levelId)
    {
        if (levelId <= 0) return 0;

#if USE_FB_TABLE
        if (TableManager.Instance == null) return 0;

        var dict = TableManager.Instance.GetTable<LevelWave>();
        if (dict == null || dict.Count == 0) return 0;

        int sum = 0;
        foreach (var kv in dict)
        {
            if (kv.Value is LevelWave lw && lw.levelId == levelId)
                sum += Mathf.Max(0, lw.totalMonster);
        }

        return sum;
#else
        return 0;
#endif
    }
}
