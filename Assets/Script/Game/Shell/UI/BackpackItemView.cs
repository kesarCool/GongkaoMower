using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Backpack-specific card. The shared reward/loot ItemCell prefab stays unchanged.</summary>
public sealed class BackpackItemView : MonoBehaviour
{
    public Image face, iconSurface, icon, fragmentMark, gradeDot;
    public TMP_Text nameLabel, countLabel;
    public Button button;
    public Sprite normalSprite, selectedSprite;
    public BackPackItemInfo Item { get; private set; }
    private Action<BackpackItemView> clicked;
    public static string DisplayName(string name) => (name ?? "").Replace("(碎片)", "碎片").Replace("（碎片）", "碎片");
    private void Awake() { button.onClick.AddListener(() => clicked?.Invoke(this)); }
    public void Bind(BackPackItemInfo item, Sprite sprite, bool portrait, bool selected, Action<BackpackItemView> onClick)
    {
        Item = item; clicked = onClick; nameLabel.text = DisplayName(item.itemName); countLabel.text = "× " + item.count;
        icon.sprite = sprite; icon.enabled = sprite != null;
        icon.rectTransform.sizeDelta = portrait ? new Vector2(104, 125) : new Vector2(90, 90);
        fragmentMark.gameObject.SetActive(portrait);
        iconSurface.color = item.type == 2 ? new Color32(235, 228, 242, 255) : item.itemId == 1 ? new Color32(248, 233, 190, 255) : item.itemId == 2 ? new Color32(219, 238, 240, 255) : new Color32(232, 235, 216, 255);
        gradeDot.color = ItemCell.GradeColor(item.grade);
        button.interactable = item.itemId > 0 && item.count > 0;
        SetSelected(selected);
    }
    public void SetSelected(bool selected) { face.sprite = selected ? selectedSprite : normalSprite; }
}
