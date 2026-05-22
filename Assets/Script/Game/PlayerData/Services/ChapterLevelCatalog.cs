using System;
using System.Collections.Generic;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>按 <c>levelId</c>（如 101）索引 <see cref="ChapterLevel"/> 配表行。</summary>
public static class ChapterLevelCatalog
{
    public const int DefaultFirstLevelId = 101;
    public const string DefaultMapResourcesPath = "Map/TileMap101";

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

    /// <summary>
    /// 关卡表 <c>mapPath</c> → Resources 路径（不含扩展名）；空或未配表时返回默认 TileMap101。
    /// </summary>
    public static string ResolveMapResourcesPath(int levelId)
    {
#if USE_FB_TABLE
        if (TryGetByLevelId(levelId, out ChapterLevel row) &&
            row != null &&
            !string.IsNullOrWhiteSpace(row.mapPath))
        {
            return NormalizeMapResourcesPath(row.mapPath);
        }
#endif
        return DefaultMapResourcesPath;
    }

    public static string NormalizeMapResourcesPath(string mapPath)
    {
        if (string.IsNullOrWhiteSpace(mapPath))
            return DefaultMapResourcesPath;

        string path = mapPath.Trim().Replace('\\', '/');
        if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            path = path.Substring(0, path.Length - ".prefab".Length);

        const string assetsResourcesPrefix = "Assets/Resources/";
        if (path.StartsWith(assetsResourcesPrefix, StringComparison.OrdinalIgnoreCase))
            path = path.Substring(assetsResourcesPrefix.Length);

        const string resourcesPrefix = "Resources/";
        if (path.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
            path = path.Substring(resourcesPrefix.Length);

        if (path.StartsWith("/", StringComparison.Ordinal))
            path = path.TrimStart('/');

        if (!path.Contains("/"))
            path = "Map/" + path;

        return path;
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
