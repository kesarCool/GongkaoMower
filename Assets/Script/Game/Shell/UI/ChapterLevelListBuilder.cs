using System.Collections.Generic;
using System.Linq;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 将 <c>ChapterLevel</c> 配表展开为纵向列表：每章先一行「章节大标签」，再按 <c>levelId</c> 升序输出该章下各关。
/// </summary>
public static class ChapterLevelListBuilder
{
    /// <param name="chapterLevelTable"><see cref="TableManager.GetTable{T}"/> 返回的字典。</param>
    public static List<LevelSelectFlatRow> Build(Dictionary<int, object> chapterLevelTable)
    {
        var rows = new List<LevelSelectFlatRow>();
        if (chapterLevelTable == null || chapterLevelTable.Count == 0)
            return rows;

#if !USE_FB_TABLE
        return rows;
#else
        var entries = new List<ChapterLevel>(chapterLevelTable.Count);
        foreach (var kv in chapterLevelTable)
        {
            if (kv.Value is ChapterLevel cl)
                entries.Add(cl);
        }

        if (entries.Count == 0)
            return rows;

        var byChapter = entries
            .GroupBy(c => c.chapterId)
            .OrderBy(g => g.Key);

        var levelOrdinal = 0;
        foreach (var group in byChapter)
        {
            int ch = group.Key;
            rows.Add(new LevelSelectFlatRow
            {
                Kind = LevelSelectRowKind.ChapterHeader,
                chapterId = ch,
                levelId = 0,
                mapName = string.Empty,
                tableRowId = 0,
                levelOrdinalInList = 0,
            });

            foreach (var cl in group.OrderBy(x => x.levelId))
            {
                levelOrdinal++;
                rows.Add(new LevelSelectFlatRow
                {
                    Kind = LevelSelectRowKind.Level,
                    chapterId = cl.chapterId,
                    levelId = cl.levelId,
                    mapName = cl.mapName ?? string.Empty,
                    tableRowId = cl.ID,
                    levelOrdinalInList = levelOrdinal,
                });
            }
        }

        return rows;
#endif
    }
}
