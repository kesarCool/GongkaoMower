#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 关卡展示文案（选关 / 大厅 / 结算共用）。
/// </summary>
public static class ChapterLevelDisplay
{
    public static string FormatLevelName(int levelId, string mapName = null)
    {
        if (!string.IsNullOrWhiteSpace(mapName))
            return mapName.Trim();

        int chapter = levelId / 100;
        int stage = levelId % 100;
        if (chapter > 0 && stage > 0)
            return $"关卡{chapter}-{stage}";

        return $"关卡 {levelId}";
    }

    public static string FormatLevelLabel(int levelId, string mapName = null)
    {
        return $"关卡：{FormatLevelName(levelId, mapName)}";
    }

    public static string ResolveMapName(int levelId)
    {
#if USE_FB_TABLE
        if (ChapterLevelCatalog.TryGetByLevelId(levelId, out ChapterLevel row) && row != null)
            return row.mapName;
#endif
        return null;
    }
}
