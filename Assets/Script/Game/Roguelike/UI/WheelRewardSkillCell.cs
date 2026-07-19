using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 转盘结算奖励 Cell — 展示中奖卡片的 icon、名称、等级。
/// 挂载在结算弹窗内的每个奖励槽位上。
/// </summary>
public class WheelRewardSkillCell : MonoBehaviour
{
    [Tooltip("技能图标")]
    public Image icon;
    [Tooltip("技能名称")]
    public TextMeshProUGUI nameText;
    [Tooltip("等级文字（如 Lv.2 → Lv.3）")]
    public TextMeshProUGUI levelText;

    public void Bind(WheelSlotData data)
    {
        if (data == null) return;

        if (icon != null && data.def != null)
            icon.sprite = data.def.icon;

        if (nameText != null && data.def != null)
            nameText.text = data.def.displayName;

        if (levelText != null)
            levelText.text = $"Lv.{data.currentLevel} → Lv.{data.targetLevel}";

        gameObject.SetActive(true);
    }
}
