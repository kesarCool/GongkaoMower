using TMPro;
using UnityEngine;

/// <summary>
/// 词汇预览虚拟列表单行：展示「序号 + DisplayText」，由 <see cref="LexiconPreviewLoopScrollDriver"/> 池化并 <see cref="Bind"/>。
/// </summary>
[DisallowMultipleComponent]
public class LexiconPreviewEntryRowCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI rightText,leftIndex,rightIndex;
    [SerializeField] private GameObject rightItem;
    public void BindPair(int ordinal,string left,string right)
    {
        bodyText.text=left??"";leftIndex.text=ordinal.ToString("00");
        rightItem.SetActive(right!=null);
        rightText.text=right??"";rightIndex.text=(ordinal+1).ToString("00");
        var layout=GetComponent<UnityEngine.UI.LayoutElement>();
        if(layout!=null)layout.preferredHeight=Mathf.Max(111,bodyText.preferredHeight+30,right!=null?rightText.preferredHeight+30:0);
    }

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

        // 虚拟列表 Cell 被池回收/重用，Start() 时机不可靠；Bind 时确保中文字体已应用
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToTMP(bodyText);

        var t = displayText ?? string.Empty;
        if (displayOrdinal > 0)
            bodyText.text = $"{displayOrdinal}. {t}";
        else
            bodyText.text = t;
    }
}
