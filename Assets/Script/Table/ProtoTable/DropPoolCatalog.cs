using System.Collections.Generic;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>按 PoolId → DropPool 行（一池一行，物品用竖线分隔）。</summary>
public static class DropPoolCatalog
{
#if USE_FB_TABLE
    private static Dictionary<int, DropPool> _byPoolId;
#endif

    public static void InvalidateCache()
    {
#if USE_FB_TABLE
        _byPoolId = null;
#endif
    }

#if USE_FB_TABLE
    public static DropPool Get(int poolId)
    {
        EnsureCache();
        _byPoolId.TryGetValue(poolId, out var row);
        return row;
    }

    private static void EnsureCache()
    {
        if (_byPoolId != null) return;
        _byPoolId = new Dictionary<int, DropPool>();

        if (TableManager.Instance == null) return;
        var dict = TableManager.Instance.GetTable<DropPool>();
        if (dict == null) return;

        foreach (var kv in dict)
        {
            if (kv.Value is DropPool dp && dp.PoolId > 0)
                _byPoolId[dp.PoolId] = dp;
        }
    }
#endif
}
