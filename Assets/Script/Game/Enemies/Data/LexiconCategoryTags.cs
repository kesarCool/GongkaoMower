/// <summary>
/// 成语主题词库（ThemePackId=1）下，文字怪 <see cref="ProtoTable.Monster.CategoryTag"/> /
/// <see cref="ProtoTable.LexiconTable.CategoryTag"/> 的整型约定。需与词表、怪物表一致。
/// </summary>
public static class LexiconCategoryTags
{
    /// <summary>
    /// 与词表一致：<c>lexiconId = ThemePackId * 100000 + CategoryTag * 1000 + 序号</c>；
    /// 序号为同一主题、同一类型下的从 1 递增（与行主键 <c>ID</c> 无关）。
    /// </summary>
    public static int MakeLexiconId(int themePackId, int categoryTag, int indexInCategory) =>
        themePackId * 100000 + categoryTag * 1000 + indexInCategory;

    /// <summary>主题包：成语词库</summary>
    public const int ThemePackChengyu = 1;

    /// <summary>神话传说</summary>
    public const int ChengyuShenHua = 1;
    /// <summary>寓言故事</summary>
    public const int ChengyuYuYan = 2;
    /// <summary>历史事件</summary>
    public const int ChengyuLiShi = 3;
    /// <summary>文学创作</summary>
    public const int ChengyuWenXue = 4;
}
