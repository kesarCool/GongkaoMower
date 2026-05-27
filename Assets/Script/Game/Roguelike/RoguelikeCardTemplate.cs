using UnityEngine;

/// <summary>
/// 卡牌模板：新卡 / 升级卡 / 突破卡 三种视觉。
/// 合并了原 CardViewStyle——不需要两层 SO 间接引用。
/// </summary>
[CreateAssetMenu(menuName = "Game/Roguelike/Card Template", fileName = "CardTemplate")]
public class RoguelikeCardTemplate : ScriptableObject
{
    [Tooltip("背景底图")]
    public Sprite background;

    [Tooltip("标签文字（如 新 / 升 / 破）")]
    public string labelText;

    [Tooltip("标签文字颜色")]
    public Color labelColor = Color.white;
}

public enum CardTemplateType
{
    NewSkill,
    UpgradeSkill,
    Breakthrough
}
