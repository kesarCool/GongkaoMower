/// <summary>
/// 选关纵向列表的一行：章节大标题 或 具体关卡（由 <see cref="ChapterLevelListBuilder"/> 展开为扁平序列）。
/// </summary>
public enum LevelSelectRowKind
{
    ChapterHeader,
    Level,
}

public struct LevelSelectFlatRow
{
    public LevelSelectRowKind Kind;
    /// <summary>章节 ID（两种行均有效，关卡行表示所属章）。</summary>
    public int chapterId;
    /// <summary>关卡 ID；仅 <see cref="LevelSelectRowKind.Level"/> 有意义。</summary>
    public int levelId;
    /// <summary>配表 <c>ChapterLevel.mapName</c>；章节行可作副标题，关卡行作主显示。</summary>
    public string mapName;
    /// <summary>配表行主键，便于调试或跳转。</summary>
    public int tableRowId;
    /// <summary>全列表中关卡行的序号（从 1 起）；仅 <see cref="LevelSelectRowKind.Level"/> 有效，章节头为 0。</summary>
    public int levelOrdinalInList;
}
