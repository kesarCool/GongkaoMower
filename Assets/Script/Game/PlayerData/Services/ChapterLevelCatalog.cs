using System.Collections.Generic;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>按 <c>levelId</c>（如 101）索引 <see cref="ChapterLevel"/> 配表行。</summary>
public static class ChapterLevelCatalog
{
    public const int DefaultFirstLevelId = 101;

#if USE_FB_TABLE
    private static Dictionary<int, ChapterLevel> _byLevelId;
#endif

    public static void InvalidateCache()
    {
#if USE_FB_TABLE
        _byLevelId = null;
#endif
    }

#if USE_FB_TABLE
    public static bool TryGetByLevelId(int levelId, out ChapterLevel row)
    {
        EnsureCache();
        if (_byLevelId != null && _byLevelId.TryGetValue(levelId, out row))
            return true;
        row = null;
        return false;
    }

    private static void EnsureCache()
    {
        if (_byLevelId != null) return;
        _byLevelId = new Dictionary<int, ChapterLevel>();

        if (TableManager.Instance == null) return;
        var dict = TableManager.Instance.GetTable<ChapterLevel>();
        if (dict == null) return;

        foreach (var kv in dict)
        {
            if (kv.Value is ChapterLevel cl && cl.levelId > 0)
                _byLevelId[cl.levelId] = cl;
        }
    }
#else
    public static bool TryGetByLevelId(int levelId, out object row)
    {
        row = null;
        return false;
    }
#endif
}
