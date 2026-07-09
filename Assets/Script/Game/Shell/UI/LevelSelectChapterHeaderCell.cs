using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 章节大标签 Cell：<see cref="LevelSelectSimpleScrollList"/> / <see cref="LevelSelectLoopScrollDriver"/> 的章节 Prefab 配套。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectChapterHeaderCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("背景")]
    [SerializeField] private Image background;
    [Tooltip("章节已解锁时背景颜色。")]
    [SerializeField] private Color unlockedColor = Color.white;
    [Tooltip("章节未解锁时背景颜色。")]
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private void Reset()
    {
        if (titleText == null)
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void Bind(in LevelSelectFlatRow row)
    {
        if (row.Kind != LevelSelectRowKind.ChapterHeader)
            return;

        if (titleText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(titleText);
            titleText.text = FormatChapterTitle(row.chapterId);
        }

        if (background != null)
            background.color = IsChapterUnlocked(row.chapterId) ? unlockedColor : lockedColor;
    }

    private static string FormatChapterTitle(int chapterId)
    {
        string theme = LexiconPreviewCatalog.GetThemeTabLabel(chapterId);
        if (string.IsNullOrEmpty(theme) || theme.StartsWith("主题 "))
            return $"第 {chapterId} 章";
        return $"第 {chapterId} 章（{theme}）";
    }

    /// <summary>该章下至少有一关已解锁即为已解锁。</summary>
    private static bool IsChapterUnlocked(int chapterId)
    {
#if USE_FB_TABLE
        if (TableManager.Instance == null) return false;
        TableManager.Instance.Init();
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        if (dict == null) return false;

        PlayerProfileService.Instance.LoadOrCreate();
        foreach (var kv in dict)
        {
            if (kv.Value is ProtoTable.ChapterLevel cl && cl.chapterId == chapterId)
            {
                if (PlayerProfileService.Instance.IsLevelUnlocked(cl.levelId))
                    return true;
            }
        }
        return false;
#else
        return true;
#endif
    }
}
