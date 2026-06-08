using TMPro;
using UnityEngine;

/// <summary>
/// 升级属性行：名称 | 当前值 → 下级值（满级时隐藏箭头和下级）。
/// 放在 Prefab 上，由 CharacterSelectionPanel 动态实例化 8 个。
/// </summary>
public class UpgradeAttrRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI curText;
    [SerializeField] private TextMeshProUGUI nextText;
    [SerializeField] private GameObject arrow;

    /// <summary>
    /// 绑定一行属性。
    /// </summary>
    /// <param name="attrName">属性名（"攻击"/"血量"/…）</param>
    /// <param name="curValue">当前值字符串（如 "15"）</param>
    /// <param name="nextValue">下级值字符串（满级时传空）</param>
    /// <param name="isMax">是否满级</param>
    /// <param name="nextColor">下级值颜色（默认绿色表示成长）</param>
    public void Bind(string attrName, string curValue, string nextValue, bool isMax, string nextColor = "#88FF88")
    {
        if (nameText != null) nameText.text = attrName;

        if (curText != null) curText.text = curValue;

        bool hasNext = !isMax && !string.IsNullOrEmpty(nextValue);
        if (nextText != null)
        {
            nextText.text = hasNext ? $"<color={nextColor}>{nextValue}</color>" : "";
            nextText.gameObject.SetActive(hasNext);
        }
        if (arrow != null) arrow.SetActive(hasNext);
    }
}
