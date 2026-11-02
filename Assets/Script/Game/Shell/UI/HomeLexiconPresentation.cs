using TMPro;
using UnityEngine;

public sealed class HomeLexiconPresentation : MonoBehaviour
{
    public TMP_Text categoryLabel,countLabel;
    public GameObject emptyHint;
    public void Refresh(int theme,int category,int count)
    {
        categoryLabel.text=LexiconPreviewCatalog.GetCategoryTabLabel(theme,category);
        countLabel.text="共 "+count+" 个词条";
        emptyHint.SetActive(count==0);
    }
}
