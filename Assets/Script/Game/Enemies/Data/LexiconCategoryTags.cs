/// <summary>
/// 公考主题词库（ThemePackId=1）下，文字怪 <see cref="ProtoTable.Monster.CategoryTag"/> /
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

    /// <summary>主题包：公考梗词</summary>
    public const int ThemePackGongkao = 1;

    /// <summary>言语理解</summary>
    public const int YanYuLiJie = 1;
    /// <summary>判断推理</summary>
    public const int PanDuanTuiLi = 2;
    /// <summary>资料分析</summary>
    public const int ZiLiaoFenXi = 3;
    /// <summary>数量关系</summary>
    public const int ShuLiangGuanXi = 4;
    /// <summary>时政常识</summary>
    public const int ShiZhengChangShi = 5;
    /// <summary>申论</summary>
    public const int ShenLun = 6;
}
