using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 章节大标签 Cell：<see cref="LevelSelectSimpleScrollList"/> / <see cref="LevelSelectLoopScrollDriver"/> 的章节 Prefab 配套。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectChapterHeaderCell : MonoBehaviour
{
    [SerializeField] private Text titleText;

    private void Reset()
    {
        if (titleText == null)
            titleText = GetComponentInChildren<Text>(true);
    }

    public void Bind(in LevelSelectFlatRow row)
    {
        if (row.Kind != LevelSelectRowKind.ChapterHeader)
            return;

        if (titleText != null)
            titleText.text = $"第 {row.chapterId} 章";
    }
}
