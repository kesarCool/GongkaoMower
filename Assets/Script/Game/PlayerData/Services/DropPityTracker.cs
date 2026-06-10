using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 掉落保底系统：记录每个关卡连续未出碎片的次数，达到阈值强制掉落。
/// 存储键：pity_L{levelId}；计数含"本局是否已掉碎片"的信息。
/// </summary>
public static class DropPityTracker
{
    private const string KEY_PREFIX = "pity_L";
    private const int PITY_THRESHOLD = 5; // 连续 5 次不掉触发保底

    private static Dictionary<int, int> _cache;

    /// <summary>获知本局掉落结果后上报：告知该关卡掉了哪些物品。</summary>
    /// <returns>保底额外追加的物品ID（0 表示无追加）</returns>
    public static int ReportAndCheck(int levelId, List<DropResult> drops, List<int> fragmentItemIds)
    {
        LoadIfNeeded();

        bool droppedFragment = false;
        if (drops != null)
        {
            foreach (var d in drops)
            {
                if (fragmentItemIds.Contains(d.itemId))
                {
                    droppedFragment = true;
                    break;
                }
            }
        }

        int counter = _cache.TryGetValue(levelId, out int v) ? v : 0;

        if (droppedFragment)
        {
            // 掉过 → 重置
            _cache[levelId] = 0;
            Save();
            return 0;
        }

        // 没掉 → 累加
        counter++;
        _cache[levelId] = counter;

        if (counter >= PITY_THRESHOLD)
        {
            // 触发保底：选池子里第一个碎片
            _cache[levelId] = 0;
            Save();
            return fragmentItemIds.Count > 0 ? fragmentItemIds[0] : 0;
        }

        Save();
        return 0;
    }

    /// <summary>查询当前保底计数（调试用）。</summary>
    public static int GetCounter(int levelId)
    {
        LoadIfNeeded();
        return _cache.TryGetValue(levelId, out int v) ? v : 0;
    }

    /// <summary>手动重置某关卡保底（GM 命令等）。</summary>
    public static void Reset(int levelId)
    {
        LoadIfNeeded();
        _cache[levelId] = 0;
        Save();
    }

    private static void LoadIfNeeded()
    {
        if (_cache != null) return;
        _cache = new Dictionary<int, int>();

        // 遍历所有 PlayerPrefs 键，还原缓存
        // PlayerPrefs 没有枚举所有键的能力，改用按需加载
    }

    private static int Load(int levelId)
    {
        if (_cache != null && _cache.TryGetValue(levelId, out int v))
            return v;
        int val = PlayerPrefs.GetInt(KeyFor(levelId), 0);
        if (_cache != null) _cache[levelId] = val;
        return val;
    }

    private static void Save()
    {
        if (_cache == null) return;
        foreach (var kv in _cache)
        {
            if (kv.Value > 0)
                PlayerPrefs.SetInt(KeyFor(kv.Key), kv.Value);
            else
                PlayerPrefs.DeleteKey(KeyFor(kv.Key));
        }
        PlayerPrefs.Save();
    }

    private static string KeyFor(int levelId) => $"{KEY_PREFIX}{levelId}";
}
