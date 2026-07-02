using TMPro;
using UnityEngine;

/// <summary>
/// 章节大标签 Cell：<see cref="LevelSelectSimpleScrollList"/> / <see cref="LevelSelectLoopScrollDriver"/> 的章节 Prefab 配套。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectChapterHeaderCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;

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
    }

    private static string FormatChapterTitle(int chapterId)
    {
        string theme = LexiconPreviewCatalog.GetThemeTabLabel(chapterId);
        if (string.IsNullOrEmpty(theme) || theme.StartsWith("主题 "))
            return $"第 {chapterId} 章";
        return $"第 {chapterId} 章（{theme}）";
    }
}
