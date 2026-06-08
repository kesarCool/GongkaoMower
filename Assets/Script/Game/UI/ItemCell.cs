using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 统一物品展示 Cell：图标 + 名称 + 数量 + 品级边框。
/// </summary>
public class ItemCell : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image gradeBorder;

    private static readonly Color[] GradeColors =
    {
        new Color(0.75f, 0.75f, 0.75f), // 0=白
        new Color(0.35f, 0.85f, 0.35f), // 1=绿
        new Color(0.3f,  0.6f,  1f),    // 2=蓝
        new Color(0.75f, 0.35f, 0.95f), // 3=紫
        new Color(1f,    0.6f,  0.15f), // 4=橙
        new Color(1f,    0.15f, 0.15f), // 5=红
    };

    public void Bind(Sprite icon, string itemName, int count, int grade)
    {
        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
        if (nameText != null) nameText.text = itemName ?? "";
        if (countText != null) { countText.text = count > 1 ? count.ToString() : ""; countText.gameObject.SetActive(count > 1); }

        int g = Mathf.Clamp(grade, 0, GradeColors.Length - 1);
        if (gradeBorder != null) gradeBorder.color = GradeColors[g];
    }
}
