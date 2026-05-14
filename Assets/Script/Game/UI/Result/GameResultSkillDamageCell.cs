using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算面板单条：技能图标 / 名称 / 局内累计伤害。
/// </summary>
[DisallowMultipleComponent]
public class GameResultSkillDamageCell : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text nameText;
    [SerializeField] private Text damageText;

    private void Awake()
    {
        if (icon == null) icon = transform.Find("Image")?.GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("TextName")?.GetComponent<Text>();
        if (damageText == null) damageText = transform.Find("TextDamage")?.GetComponent<Text>();
    }

    public void Bind(Sprite sprite, string displayName, float damage)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (nameText != null) nameText.text = displayName ?? string.Empty;
        if (damageText != null) damageText.text = Mathf.RoundToInt(damage).ToString();
    }
}
