using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算面板单条：技能图标 / 名称 / 局内累计伤害。
/// </summary>
[DisallowMultipleComponent]
public class GameResultSkillDamageCell : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image damageShare;
    [SerializeField] private GameObject breakthroughBadge;

    private void Awake()
    {
        if (icon == null) icon = transform.Find("Image")?.GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("TextName")?.GetComponent<TextMeshProUGUI>();
        if (damageText == null) damageText = transform.Find("TextDamage")?.GetComponent<TextMeshProUGUI>();
    }

    public void Bind(Sprite sprite, string displayName, float damage)
    {
        if (icon != null)
        {
            icon.color = Color.white;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        string name = displayName ?? string.Empty;
        if (levelText != null)
        {
            int split = name.IndexOf(' '); bool hasLevel = name.StartsWith("Lv.") && split > 0;
            levelText.text = hasLevel ? name.Substring(0, split) : "";
            if (hasLevel) name = name.Substring(split + 1);
        }
        if (nameText != null) nameText.text = name;
        if (damageText != null) damageText.text = Mathf.RoundToInt(damage).ToString(levelText != null ? "N0" : "0");
    }

    /// <summary>设置突破满级态：icon 变红。</summary>
    public void SetBreakthrough(bool on)
    {
        if (breakthroughBadge != null) { breakthroughBadge.SetActive(on); if (icon != null) icon.color = Color.white; return; }
        if (icon != null)
        {
            icon.color = on ? new Color(1f, 0f, 0.478f, 1f) : Color.white; // #FF007A
        }
    }

    public void SetDamageShare(float share, int index)
    {
        if (damageShare == null) return;
        Color32[] colors = { new Color32(210,161,74,255), new Color32(143,171,115,255), new Color32(114,162,167,255), new Color32(161,139,177,255), new Color32(197,132,112,255) };
        damageShare.color = colors[Mathf.Abs(index) % colors.Length]; damageShare.fillAmount = Mathf.Clamp01(share);
    }
}
