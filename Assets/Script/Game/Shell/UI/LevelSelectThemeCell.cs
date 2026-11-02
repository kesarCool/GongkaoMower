using UnityEngine;
using UnityEngine.UI;

/// <summary>One recyclable cell type for the loop library's internal temporary pool.</summary>
public sealed class LevelSelectThemeCell : MonoBehaviour
{
    public LevelSelectChapterHeaderCell chapter;
    public LevelSelectLevelRowCell level;
    public void Bind(in LevelSelectFlatRow row)
    {
        bool isChapter = row.Kind == LevelSelectRowKind.ChapterHeader;
        chapter.gameObject.SetActive(isChapter); level.gameObject.SetActive(!isChapter);
        GetComponent<LayoutElement>().preferredHeight = isChapter ? 90 : 122;
        if (isChapter) chapter.Bind(row); else level.Bind(row);
    }
}
