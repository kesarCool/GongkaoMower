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

        var list = new List<ChapterLevel>(dict.Count);
        foreach (var kv in dict)
        {
            if (kv.Value is ChapterLevel cl)
                list.Add(cl);
        }

        if (list.Count == 0) return false;

        list.Sort((a, b) =>
        {
            int c = a.chapterId.CompareTo(b.chapterId);
            return c != 0 ? c : a.levelId.CompareTo(b.levelId);
        });

        int idx = list.FindIndex(x => x.chapterId == chapterId && x.levelId == levelId);
        if (idx < 0 || idx >= list.Count - 1) return false;

        ChapterLevel next = list[idx + 1];
        nextChapterId = next.chapterId;
        nextLevelId = next.levelId;
        return true;
#else
        return false;
#endif
    }
}
