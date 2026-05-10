using UnityEngine;

/// <summary>
/// 肉鸽卡牌模板：只用3个模板（新卡/升级卡/突破卡），动态绑定技能数据
/// </summary>
[CreateAssetMenu(menuName = "Game/Roguelike/Card Template", fileName = "RoguelikeCardTemplate")]
public class RoguelikeCardTemplate : ScriptableObject
{
    [Tooltip("模板类型（决定用哪个UI样式）")]
    public CardTemplateType templateType;

    [Tooltip("关联的视觉样式（绿色/黄色/红色）")]
    public CardViewStyle style;

    [Tooltip("标签文字（如新卡显示 新，升级卡显示 升）")]
    public string labelText;

    [Tooltip("卡片标题格式：{0}=技能名称")]
    public string titleFormat = "{0}";

    [Tooltip("描述格式：{0}=当前等级，{1}=下一级效果预览")]
    public string descriptionFormat = "当前等级：{0}\n{1}";
}

public enum CardTemplateType
{
    NewSkill,       // 绿色 - 新获得技能
    UpgradeSkill,   // 黄色 - 升级已有技能
    Breakthrough    // 红色 - 突破到顶级（配合后期被动系统）
}
