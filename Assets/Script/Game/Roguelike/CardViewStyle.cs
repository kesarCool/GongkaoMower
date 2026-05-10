using UnityEngine;

/// <summary>
/// 卡牌视觉样式（UI模板）：新卡/升级卡/突破卡 三种样式
/// </summary>
[CreateAssetMenu(menuName = "Game/Roguelike/Card View Style", fileName = "CardViewStyle")]
public class CardViewStyle : ScriptableObject
{
    [Tooltip("样式名称（如新卡/升级卡/突破卡）")]
    public string styleName;

    [Tooltip("背景底图（绿色/黄色/红色）")]
    public Sprite background;

    [Tooltip("左上角标签图标（如新字图标）")]
    public Sprite labelIcon;

    [Tooltip("标签文字颜色")]
    public Color labelColor = Color.white;

    [Tooltip("边框光效颜色（稀有度光效）")]
    public Color borderGlowColor = Color.white;

    [Tooltip("稀有度星星数量（UI显示用）")]
    public int rarityStars = 1;
}
