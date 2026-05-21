using System.Collections.Generic;
using UnityEngine;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 章节关卡顺序导航（依赖配表；无表或未命中时 <see cref="TryGetNext"/> 返回 false）。
/// </summary>
public static class ChapterLevelNavigation
{
    public static bool TryGetNext(int chapterId, int levelId, out int nextChapterId, out int nextLevelId)
    {
        nextChapterId = 0;
        nextLevelId = 0;

#if USE_FB_TABLE
        if (TableManager.Instance == null) return false;

        var dict = TableManager.Instance.GetTable<ChapterLevel>();
        if (dict == null || dict.Count == 0) return false;

        var list = BuildSortedLevelRows(dict);
        if (list.Count == 0) return false;

        int idx = list.FindIndex(x => x.chapterId == chapterId && x.levelId == levelId);
        if (idx < 0 || idx >= list.Count - 1) return false;

        var next = list[idx + 1];
        nextChapterId = next.chapterId;
        nextLevelId = next.levelId;
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 按列表顺序取「当前进度」：最后一个已解锁的关卡（默认开始游戏用）。
    /// </summary>
    public static bool TryGetMaxUnlockedLevel(out int chapterId, out int levelId)
    {
        chapterId = 0;
        levelId = 0;

#if USE_FB_TABLE
        if (TableManager.Instance == null) return false;

        TableManager.Instance.Init();
        var dict = TableManager.Instance.GetTable<ChapterLevel>();
        if (dict == null || dict.Count == 0) return false;

        var list = BuildSortedLevelRows(dict);
        if (list.Count == 0) return false;

        PlayerProfileService.Instance.LoadOrCreate();

        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            var row = list[i];
            if (!PlayerProfileService.Instance.IsLevelUnlocked(row.levelId))
                break;

            chapterId = row.chapterId;
            levelId = row.levelId;
            any = true;
        }

        if (any) return true;

        chapterId = list[0].chapterId;
        levelId = list[0].levelId;
        return true;
#else
        chapterId = 1;
        levelId = ChapterLevelCatalog.DefaultFirstLevelId;
        return true;
#endif
    }

#if USE_FB_TABLE
    private static List<ChapterLevel> BuildSortedLevelRows(Dictionary<int, object> dict)
    {
        var list = new List<ChapterLevel>(dict.Count);
        foreach (var kv in dict)
        {
            if (kv.Value is ChapterLevel cl)
                list.Add(cl);
        }

        list.Sort((a, b) =>
        {
            int c = a.chapterId.CompareTo(b.chapterId);
            return c != 0 ? c : a.levelId.CompareTo(b.levelId);
        });

        return list;
    }
#endif
}
