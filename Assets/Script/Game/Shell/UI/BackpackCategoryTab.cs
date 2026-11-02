using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BackpackCategoryTab : MonoBehaviour
{
    public Button button;
    public Image face, icon;
    public TMP_Text label;
    private Action clicked;
    private void Awake() { button.onClick.AddListener(() => clicked?.Invoke()); }
    public void Bind(BackpackCategoryCatalog.Category category, Action onClick)
    {
        label.text = category.label; icon.sprite = category.icon; icon.enabled = category.icon != null; clicked = onClick;
        GetComponent<LayoutElement>().preferredWidth = Mathf.Max(230, label.GetPreferredValues(category.label).x + 142);
    }
    public void Select(bool selected)
    {
        face.enabled = selected;
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        label.color = selected ? new Color32(73, 54, 39, 255) : new Color32(118, 99, 76, 255);
    }
}
