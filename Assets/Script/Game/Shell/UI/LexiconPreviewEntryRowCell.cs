using TMPro;
using UnityEngine;

/// <summary>
/// 词汇预览虚拟列表单行：展示「序号 + DisplayText」，由 <see cref="LexiconPreviewLoopScrollDriver"/> 池化并 <see cref="Bind"/>。
/// </summary>
[DisallowMultipleComponent]
public class LexiconPreviewEntryRowCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI bodyText;

    private void Reset()
    {
        if (bodyText == null)
            bodyText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>运行时由 Driver 在 Instantiate 模板之后调用。</summary>
    public void AssignBodyText(TextMeshProUGUI text)
    {
        bodyText = text;
    }

    /// <param name="displayOrdinal">从 1 起的词条序号（与表内当前筛选下的顺序一致）。</param>
    public void Bind(int displayOrdinal, string displayText)
    {
        if (bodyText == null)
            return;

        var t = displayText ?? string.Empty;
        if (displayOrdinal > 0)
            bodyText.text = $"{displayOrdinal}. {t}";
        else
            bodyText.text = t;
    }
}
