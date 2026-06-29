/// <summary>
/// 文字怪 <see cref="ProtoTable.Monster.CategoryTag"/> /
/// <see cref="ProtoTable.LexiconTable.CategoryTag"/> 的整型约定。需与词表、怪物表一致。
/// <para>ThemePackId=1：中华美食（10 类）</para>
/// <para>ThemePackId=2：成语词库（7 类）</para>
/// </summary>
public static class LexiconCategoryTags
{
    /// <summary>
    /// 与词表一致：<c>lexiconId = ThemePackId * 100000 + CategoryTag * 1000 + 序号</c>；
    /// 序号为同一主题、同一类型下的从 1 递增（与行主键 <c>ID</c> 无关）。
    /// </summary>
    public static int MakeLexiconId(int themePackId, int categoryTag, int indexInCategory) =>
        themePackId * 100000 + categoryTag * 1000 + indexInCategory;

    // ===========================
    // 主题包
    // ===========================

    /// <summary>主题包：中华美食</summary>
    public const int ThemePackFood = 1;
    /// <summary>主题包：成语词库</summary>
    public const int ThemePackChengyu = 2;

    // ===========================
    // 美食分类（ThemePackId=1）
    // ===========================

    /// <summary>京津冀（京·津·冀）</summary>
    public const int FoodJingJinJi = 1;
    /// <summary>晋鲁豫（晋·鲁·豫）</summary>
    public const int FoodJinLuYu = 2;
    /// <summary>东北F4（黑·吉·辽·蒙）</summary>
    public const int FoodDongBei = 3;
    /// <summary>江浙沪皖（苏·浙·沪·皖）</summary>
    public const int FoodJiangZheHuWan = 4;
    /// <summary>云贵川渝（云·贵·川·渝）</summary>
    public const int FoodYunGuiChuanYu = 5;
    /// <summary>湘赣鄂（湘·赣·鄂）</summary>
    public const int FoodXiangGanE = 6;
    /// <summary>港澳台（港·澳·台）</summary>
    public const int FoodGangAoTai = 7;
    /// <summary>粤桂琼闽（粤·桂·琼·闽）</summary>
    public const int FoodYueGuiQiongMin = 8;
    /// <summary>陕甘宁（陕·甘·宁）</summary>
    public const int FoodShaanGanNing = 9;
    /// <summary>新青藏（新·青·藏）</summary>
    public const int FoodXinQingZang = 10;

    // ===========================
    // 成语分类（ThemePackId=2）
    // ===========================

    /// <summary>神话传说</summary>
    public const int ChengyuShenHua = 1;
    /// <summary>寓言故事</summary>
    public const int ChengyuYuYan = 2;
    /// <summary>历史事件</summary>
    public const int ChengyuLiShi = 3;
    /// <summary>文学创作</summary>
    public const int ChengyuWenXue = 4;
    /// <summary>佛经禅语</summary>
    public const int ChengyuFoJing = 5;
    /// <summary>民俗谚语</summary>
    public const int ChengyuMinSu = 6;
    /// <summary>诸子格言</summary>
    public const int ChengyuZhuZi = 7;
}
