/// <summary>
/// 选关结果：由关卡 Cell 写入，BattleLoading / Game 入口读取。
/// </summary>
public static class SelectedLevelContext
{
    public static int ChapterId { get; private set; }
    public static int LevelId { get; private set; }
    public static bool HasSelection { get; private set; }

    public static void Set(int chapterId, int levelId)
    {
        ChapterId = chapterId;
        LevelId = levelId;
        HasSelection = true;
    }

    public static void Clear()
    {
        HasSelection = false;
        ChapterId = 0;
        LevelId = 0;
    }
}
