using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 从 <see cref="TableManager"/> 的 <c>LexiconTable</c> 聚合主题包、分类与 <c>DisplayText</c> 列表（供词汇预览弹窗使用）。
/// </summary>
public static class LexiconPreviewCatalog
{
    public static List<int> CollectThemePackIds()
    {
        var list = new List<int>();
#if USE_FB_TABLE
        var dict = TableManager.Instance != null ? TableManager.Instance.GetTable<LexiconTable>() : null;
        if (dict == null || dict.Count == 0)
            return list;

        var set = new HashSet<int>();
        foreach (var kv in dict)
        {
            if (kv.Value is LexiconTable row && row.ThemePackId > 0)
                set.Add(row.ThemePackId);
        }

        list.AddRange(set);
        list.Sort();
#endif
        return list;
    }

    public static List<int> CollectCategoryTagsForTheme(int themePackId)
    {
        var list = new List<int>();
#if USE_FB_TABLE
        var dict = TableManager.Instance != null ? TableManager.Instance.GetTable<LexiconTable>() : null;
        if (dict == null || dict.Count == 0 || themePackId <= 0)
            return list;

        var set = new HashSet<int>();
        foreach (var kv in dict)
        {
            if (kv.Value is LexiconTable row && row.ThemePackId == themePackId && row.CategoryTag > 0)
                set.Add(row.CategoryTag);
        }

        list.AddRange(set);
        list.Sort();
#endif
        return list;
    }

    public static List<string> CollectDisplayTexts(int themePackId, int categoryTag)
    {
        var list = new List<string>();
#if USE_FB_TABLE
        var dict = TableManager.Instance != null ? TableManager.Instance.GetTable<LexiconTable>() : null;
        if (dict == null || dict.Count == 0 || themePackId <= 0 || categoryTag <= 0)
            return list;

        var rows = new List<(int lexiconId, string text)>();
        foreach (var kv in dict)
        {
            if (kv.Value is not LexiconTable row)
                continue;
            if (row.ThemePackId != themePackId || row.CategoryTag != categoryTag)
                continue;

            if (!SensitiveWordFilter.Instance.IsLexiconAllowed(row.ID, row.DisplayText))
                continue;

            var t = row.DisplayText ?? string.Empty;
            rows.Add((row.lexiconId, t));
        }

        foreach (var x in rows.OrderBy(r => r.lexiconId))
            list.Add(x.text);
#endif
        return list;
    }

    /// <summary>
    /// 多选主题 + 多选分类：返回所有「主题 ∈ <paramref name="themePackIds"/> 且 分类 ∈ <paramref name="categoryTags"/>」的词条文本，
    /// 按主题、分类、<c>lexiconId</c> 排序（词汇量大时用虚拟列表展示）。
    /// </summary>
    public static List<string> CollectDisplayTextsFiltered(IReadOnlyCollection<int> themePackIds, IReadOnlyCollection<int> categoryTags)
    {
        var list = new List<string>();
#if USE_FB_TABLE
        if (themePackIds == null || categoryTags == null || themePackIds.Count == 0 || categoryTags.Count == 0)
            return list;

        var themeSet = new HashSet<int>(themePackIds);
        var catSet = new HashSet<int>(categoryTags);

        var dict = TableManager.Instance != null ? TableManager.Instance.GetTable<LexiconTable>() : null;
        if (dict == null || dict.Count == 0)
            return list;

        var rows = new List<(int theme, int cat, int lexiconId, string text)>();
        foreach (var kv in dict)
        {
            if (kv.Value is not LexiconTable row)
                continue;
            if (!themeSet.Contains(row.ThemePackId) || !catSet.Contains(row.CategoryTag))
                continue;

            if (!SensitiveWordFilter.Instance.IsLexiconAllowed(row.ID, row.DisplayText))
                continue;

            var t = row.DisplayText ?? string.Empty;
            rows.Add((row.ThemePackId, row.CategoryTag, row.lexiconId, t));
        }

        foreach (var x in rows.OrderBy(r => r.theme).ThenBy(r => r.cat).ThenBy(r => r.lexiconId))
            list.Add(x.text);
#endif
        return list;
    }

    /// <summary>在多个已选主题下，合并出现的分类 <c>CategoryTag</c>（去重、升序）。</summary>
    public static List<int> CollectCategoryTagsForThemes(IReadOnlyCollection<int> themePackIds)
    {
        var list = new List<int>();
        if (themePackIds == null || themePackIds.Count == 0)
            return list;

        var set = new HashSet<int>();
        foreach (var tid in themePackIds)
        {
            foreach (var cid in CollectCategoryTagsForTheme(tid))
                set.Add(cid);
        }

        list.AddRange(set);
        list.Sort();
        return list;
    }

    /// <summary>
    /// 当前已选多个主题时：若仅一个主题则用 <see cref="GetCategoryTabLabel"/>；若含成语包则优先用成语包命名；否则用 <see cref="GetCategoryTabLabelBestEffort"/>。
    /// </summary>
    public static string GetCategoryTabLabelForSelection(IReadOnlyCollection<int> themePackIds, int categoryTag)
    {
        if (themePackIds == null || themePackIds.Count == 0)
            return GetCategoryTabLabelBestEffort(categoryTag);

        if (themePackIds.Count == 1)
        {
            foreach (var t in themePackIds)
                return GetCategoryTabLabel(t, categoryTag);
        }

        foreach (var t in themePackIds)
        {
            if (t == LexiconCategoryTags.ThemePackChengyu)
                return GetCategoryTabLabel(t, categoryTag);
        }

        return GetCategoryTabLabelBestEffort(categoryTag);
    }

    /// <summary>分类显示名：不依赖具体主题包时，用成语常用标签名兜底。</summary>
    public static string GetCategoryTabLabelBestEffort(int categoryTag)
    {
        if (categoryTag == LexiconCategoryTags.ChengyuShenHua) return "神话传说";
        if (categoryTag == LexiconCategoryTags.ChengyuYuYan) return "寓言故事";
        if (categoryTag == LexiconCategoryTags.ChengyuLiShi) return "历史事件";
        if (categoryTag == LexiconCategoryTags.ChengyuWenXue) return "文学创作";
        if (categoryTag == LexiconCategoryTags.ChengyuFoJing) return "佛经禅语";
        if (categoryTag == LexiconCategoryTags.ChengyuMinSu) return "民俗谚语";
        if (categoryTag == LexiconCategoryTags.ChengyuZhuZi) return "诸子格言";
        return "分类 " + categoryTag;
    }

    /// <summary>主题包在页签上的显示名（可随产品扩展）。</summary>
    public static string GetThemeTabLabel(int themePackId)
    {
        if (themePackId == LexiconCategoryTags.ThemePackChengyu)
            return "成语篇";
        if (themePackId == LexiconCategoryTags.ThemePackFood)
            return "美食篇";
        return "主题 " + themePackId;
    }

    /// <summary>分类在子页签上的显示名。</summary>
    public static string GetCategoryTabLabel(int themePackId, int categoryTag)
    {
        if (themePackId == LexiconCategoryTags.ThemePackChengyu)
        {
            if (categoryTag == LexiconCategoryTags.ChengyuShenHua) return "神话传说";
            if (categoryTag == LexiconCategoryTags.ChengyuYuYan) return "寓言故事";
            if (categoryTag == LexiconCategoryTags.ChengyuLiShi) return "历史事件";
            if (categoryTag == LexiconCategoryTags.ChengyuWenXue) return "文学创作";
            if (categoryTag == LexiconCategoryTags.ChengyuFoJing) return "佛经禅语";
            if (categoryTag == LexiconCategoryTags.ChengyuMinSu) return "民俗谚语";
            if (categoryTag == LexiconCategoryTags.ChengyuZhuZi) return "诸子格言";
        }

        if (themePackId == LexiconCategoryTags.ThemePackFood)
        {
            if (categoryTag == LexiconCategoryTags.FoodJingJinJi) return "京津冀";
            if (categoryTag == LexiconCategoryTags.FoodJinLuYu) return "晋鲁豫";
            if (categoryTag == LexiconCategoryTags.FoodDongBei) return "黑吉辽蒙";
            if (categoryTag == LexiconCategoryTags.FoodJiangZheHuWan) return "江浙沪皖";
            if (categoryTag == LexiconCategoryTags.FoodYunGuiChuanYu) return "云贵川渝";
            if (categoryTag == LexiconCategoryTags.FoodXiangGanE) return "湘赣鄂";
            if (categoryTag == LexiconCategoryTags.FoodGangAoTai) return "港澳台";
            if (categoryTag == LexiconCategoryTags.FoodYueGuiQiongMin) return "粤桂琼闽";
            if (categoryTag == LexiconCategoryTags.FoodShaanGanNing) return "陕甘宁";
            if (categoryTag == LexiconCategoryTags.FoodXinQingZang) return "新青藏";
        }

        return "分类 " + categoryTag;
    }
}
