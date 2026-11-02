using UnityEngine;

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

    /// <summary>使用配表 stageName；未配置时显示关卡编号。</summary>
    public static string GetDisplayText()
    {
        return ChapterLevelDisplay.FormatLevelName(LevelId);
    }

    public static void LogMissingSelectionOnce(string caller)
    {
        if (HasSelection) return;
        Debug.LogWarning($"[{caller}] 未选关，局内将使用默认关卡 {ChapterLevelCatalog.DefaultFirstLevelId}。");
    }
}
