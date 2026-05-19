/// <summary>
/// 关卡解锁：<c>unlockChapterId</c> 为 0 表示默认解锁；否则须已通关该前置 <c>levelId</c>（如通关 101 解锁 102）。
/// </summary>
public static class ChapterLevelUnlockEvaluator
{
    /// <param name="prerequisiteLevelId">配表 <c>unlockChapterId</c>，语义为前置关卡 <c>levelId</c>。</param>
    public static bool IsLevelUnlocked(int levelId, System.Func<int, bool> hasClearedLevel)
    {
#if !USE_FB_TABLE
        return true;
#else
        if (levelId <= 0) return false;
        if (!ChapterLevelCatalog.TryGetByLevelId(levelId, out var row) || row == null)
            return levelId == ChapterLevelCatalog.DefaultFirstLevelId;

        int prerequisiteLevelId = row.unlockChapterId;
        if (prerequisiteLevelId <= 0)
            return true;

        return hasClearedLevel != null && hasClearedLevel(prerequisiteLevelId);
#endif
    }
}
