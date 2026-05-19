using UnityEngine;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 局内关卡唯一数据源：读 <see cref="SelectedLevelContext"/>（选关 / 结算下一关写入），
/// 不再由 SpawnerWaves、RoguelikeCardManager 等各自维护 levelId。
/// </summary>
public static class BattleLevelContext
{
    public static int LevelId
    {
        get
        {
            if (SelectedLevelContext.HasSelection)
                return SelectedLevelContext.LevelId;
            return ChapterLevelCatalog.DefaultFirstLevelId;
        }
    }

    public static bool HasSelection => SelectedLevelContext.HasSelection;

    /// <summary>如 101 → 「关卡1-1」；有配表 mapName 时追加名称。</summary>
    public static string GetDisplayText()
    {
        int id = LevelId;
        int chapter = id / 100;
        int stage = id % 100;
        if (chapter <= 0 || stage <= 0)
            return $"关卡 {id}";

        string text = $"关卡{chapter}-{stage}";

/*#if USE_FB_TABLE
        if (ChapterLevelCatalog.TryGetByLevelId(id, out ChapterLevel row) &&
            row != null &&
            !string.IsNullOrEmpty(row.mapName))
        {
            text = $"{text} {row.mapName}";
        }
#endif
*/

        return text;
    }

    public static void LogMissingSelectionOnce(string caller)
    {
        if (HasSelection) return;
        Debug.LogWarning($"[{caller}] 未选关，局内将使用默认关卡 {ChapterLevelCatalog.DefaultFirstLevelId}。");
    }
}
