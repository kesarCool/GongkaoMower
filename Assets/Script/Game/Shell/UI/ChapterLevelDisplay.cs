#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 关卡展示文案（选关 / 大厅 / 结算共用）。
/// </summary>
public static class ChapterLevelDisplay
{
    public static string FormatLevelName(int levelId, string stageName = null)
    {
        if (stageName == null)
            stageName = ResolveStageName(levelId);
        if (!string.IsNullOrWhiteSpace(stageName))
            return stageName.Trim();

        int chapter = levelId / 100;
        int stage = levelId % 100;
        if (chapter > 0 && stage > 0)
            return $"关卡{chapter}-{stage}";

        return $"关卡 {levelId}";
    }

    public static string FormatLevelLabel(int levelId, string stageName = null)
    {
        return $"关卡：{FormatLevelName(levelId, stageName)}";
    }

    public static string ResolveStageName(int levelId)
    {
#if USE_FB_TABLE
        TableManager.Instance.EnsureLoaded();
        if (ChapterLevelCatalog.TryGetByLevelId(levelId, out ChapterLevel row) && row != null)
            return row.stageName;
#endif
        return null;
    }

    public static string ResolveChapterName(int chapterId)
    {
#if USE_FB_TABLE
        TableManager.Instance.EnsureLoaded();
        var table = TableManager.Instance.GetTable<ChapterLevel>();
        ChapterLevel first = null;
        if (table != null)
            foreach (var entry in table)
                if (entry.Value is ChapterLevel row && row.chapterId == chapterId &&
                    !string.IsNullOrWhiteSpace(row.mapName) && (first == null || row.levelId < first.levelId))
                    first = row;
        if (first != null)
            return first.mapName.Trim();
#endif
        return null;
    }

    public static string FormatChapterTitle(int chapterId, string mapName = null)
    {
        string name = mapName ?? ResolveChapterName(chapterId);
        return string.IsNullOrWhiteSpace(name) ? $"第{chapterId}章" : $"第{chapterId}章 · {name.Trim()}";
    }
}
