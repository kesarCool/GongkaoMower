using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 转盘单个槽位 Cell — 挂载在每个槽位根节点上。
/// 需在 Unity 编辑器中绑定 icon、levelText、highlightMark 三个引用。
/// </summary>
public class WheelSlotCell : MonoBehaviour
{
    [Tooltip("技能图标")]
    public Image icon;
    [Tooltip("等级文字")]
    public TextMeshProUGUI levelText;
    [Tooltip("高亮选中标记（初始隐藏）")]
    public GameObject highlightMark;
    public Image face;
    public Sprite activeFace, passiveFace, winnerFace;
    public GameObject winnerMark;
    public bool IsWinner { get; private set; }
    private bool _passive;

    public void Bind(WheelSlotData data)
    {
        IsWinner = false;
        _passive = data != null && !data.isActive;
        if (face != null) face.sprite = _passive ? passiveFace : activeFace;
        if (winnerMark != null) winnerMark.SetActive(false);
        if (icon != null && data.def != null)
            icon.sprite = data.def.icon;

        if (levelText != null)
            levelText.text = $"Lv.{data.currentLevel}";

        if (highlightMark != null)
            highlightMark.SetActive(false);

        gameObject.SetActive(true);
    }

    public void SetHighlight(bool on)
    {
        if (highlightMark != null)
            highlightMark.SetActive(on);
    }

    public void SetWinner(bool on)
    {
        IsWinner = on;
        if (face != null) face.sprite = on ? winnerFace : _passive ? passiveFace : activeFace;
        if (winnerMark != null) winnerMark.SetActive(on);
        SetHighlight(false);
    }
}
