using System.Collections.Generic;
using UnityEngine;

/// <summary>单次掉落结果。</summary>
public struct DropResult
{
    public int itemId;
    public int count;

    public DropResult(int itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }
}

/// <summary>
/// 掉落池管理器。DropPool 表每条记录一个池，ItemIds/MinCounts/MaxCounts/Weights 均为 FlatBuffer [string] 数组。
/// 数组元素一一对应：ItemIds[0] 搭配 MinCounts[0]/MaxCounts[0]/Weights[0]。
/// </summary>
public static class DropManager
{
    /// <summary>Roll 一个掉落池，返回所有命中物品（独立判定）。</summary>
    public static List<DropResult> Roll(int poolId)
    {
        var results = new List<DropResult>();
        if (poolId <= 0) return results;

#if USE_FB_TABLE
        var row = DropPoolCatalog.Get(poolId);
        if (row == null) return results;

        var ids = row.ItemIds;
        var mins = row.MinCounts;
        var maxs = row.MaxCounts;
        var weights = row.Weights;

        int count = Mathf.Min(ids.Count, mins.Count, maxs.Count, weights.Count);
        for (int i = 0; i < count; i++)
        {
            if (!int.TryParse(ids[i], out int itemId) || itemId <= 0) continue;
            int.TryParse(mins[i], out int min);
            int.TryParse(maxs[i], out int max);
            int.TryParse(weights[i], out int weight);

            int qty = RollEntry(min, max, weight, out bool hit);
            if (hit && qty > 0)
                results.Add(new DropResult(itemId, qty));
        }
#endif

        return results;
    }

    /// <summary>Roll 多个池，结果合并去重（同 itemId 累加数量）。</summary>
    public static List<DropResult> RollMultiple(params int[] poolIds)
    {
        var merged = new Dictionary<int, int>();
        foreach (int pid in poolIds)
        {
            if (pid <= 0) continue;
            foreach (var r in Roll(pid))
            {
                if (merged.ContainsKey(r.itemId))
                    merged[r.itemId] += r.count;
                else
                    merged[r.itemId] = r.count;
            }
        }

        var list = new List<DropResult>();
        foreach (var kv in merged)
            list.Add(new DropResult(kv.Key, kv.Value));
        return list;
    }

    /// <summary>收集 poolIds 中所有碎片类物品的 ID（ItemTable.Type==2）。给保底用。</summary>
    public static List<int> CollectFragmentIds(params int[] poolIds)
    {
        var ids = new List<int>();
#if USE_FB_TABLE
        var itemTable = TableManager.Instance?.GetTable<ProtoTable.ItemTable>();
        foreach (int pid in poolIds)
        {
            if (pid <= 0) continue;
            var row = DropPoolCatalog.Get(pid);
            if (row == null) continue;
            foreach (var s in row.ItemIds)
            {
                if (!int.TryParse(s, out int id) || id <= 0) continue;
                if (ids.Contains(id)) continue;
                if (itemTable != null && itemTable.TryGetValue(id, out var obj) && obj is ProtoTable.ItemTable it && it.Type == 2)
                    ids.Add(id);
            }
        }
#endif
        return ids;
    }

    private static int RollEntry(int min, int max, int weight, out bool hit)
    {
        hit = false;
        if (max <= 0) return 0;

        if (weight <= 0)
        {
            hit = true;
            return RandomCount(min, max);
        }

        if (Random.Range(1, 10001) <= weight)
        {
            hit = true;
            return RandomCount(min, max);
        }

        return 0;
    }

    private static int RandomCount(int min, int max)
    {
        if (max <= min) return Mathf.Max(1, min);
        return Random.Range(min, max + 1);
    }
}
