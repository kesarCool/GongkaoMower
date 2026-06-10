using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一物品展示 Cell：图标 + 名称 + 数量 + 品级边框 + 点击。
/// 使用 SetClickCallback 挂自定义回调；不挂则点击无响应。
/// </summary>
public class ItemCell : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image gradeBorder;
    [SerializeField] private Button clickButton;

    public static readonly Color[] GradeColors =
    {
        new Color(0.75f, 0.75f, 0.75f),
        new Color(0.35f, 0.85f, 0.35f),
        new Color(0.3f,  0.6f,  1f),
        new Color(0.75f, 0.35f, 0.95f),
        new Color(1f,    0.6f,  0.15f),
        new Color(1f,    0.15f, 0.15f),
    };

    private Action _onClick;
    private string _itemName, _description;

    public void Bind(Sprite icon, string itemName, int count, int grade, string description = "")
    {
        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
        if (nameText != null) nameText.text = itemName ?? "";
        if (countText != null) { countText.text = count > 1 ? count.ToString() : ""; countText.gameObject.SetActive(count > 1); }

        int g = Mathf.Clamp(grade, 0, GradeColors.Length - 1);
        if (gradeBorder != null) gradeBorder.color = GradeColors[g];

        _itemName = itemName;
        _description = description;

        if (clickButton != null)
        {
            clickButton.interactable = true;
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(() =>
            {
                _onClick?.Invoke();
                if (!string.IsNullOrEmpty(_itemName)) ItemTooltip.Show(_itemName, _description, (RectTransform)transform);
            });
        }
    }

    public void SetClickCallback(Action callback) { _onClick = callback; }

    /// <summary>绑定空占位格子：无内容、灰底、不可点击。</summary>
    public void BindEmpty()
    {
        if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        if (nameText != null) nameText.text = "";
        if (countText != null) { countText.text = ""; countText.gameObject.SetActive(false); }
        if (gradeBorder != null) gradeBorder.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        _itemName = null;
        _description = null;

        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.interactable = false;
        }
    }

    public static Color GradeColor(int grade)
    {
        int g = Mathf.Clamp(grade, 0, GradeColors.Length - 1);
        return GradeColors[g];
    }
}
