using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 被动技能列表 Cell：展示 icon + 名字 + 等级。
/// </summary>
public class GamePassiveSkillCell : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;

    public void Bind(Sprite icon, string skillName, int level)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
            nameText.text = skillName ?? "";

        if (levelText != null)
            levelText.text = level > 0 ? $"Lv.{level}" : "";
    }
}
