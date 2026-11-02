using UnityEngine;

/// <summary>
/// 被动技能定义：SkillDefinitionBase 的子类，配置无弹丸的纯增益技能。
/// 运行时创建 <see cref="SkillPassive"/> 或子类实例。
/// </summary>
[CreateAssetMenu(menuName = "Game/Skills/Passive Definition", fileName = "SkillDef_Passive")]
public class PassiveSkillDefinition : SkillDefinitionBase
{
    [Header("被动")]
    [Tooltip("加成计算方式：乘算（+X%）=原值×(1+X)，加算=原值+X，绝对值=X。")]
    public PassiveModType modType;

    [Tooltip("Per-level 加成值（size >= maxLevel）。乘算/加算时 X=0.2 表示 20%。")]
    public float[] bonusByLevel = { 0.2f, 0.24f, 0.28f, 0.33f, 0.4f };

    public float BonusAt(int level) =>
        bonusByLevel[Mathf.Clamp(level, 1, bonusByLevel.Length) - 1];

    private bool IsPercentageBonus => id == SkillId.PassiveRegen || modType == PassiveModType.Multiplicative ||
        id == SkillId.PassiveCritRate || id == SkillId.PassiveArmorPen;

    public string FormatBonusAt(int level) => SkillDescriptionStat.Bonus("", BonusAt(level), IsPercentageBonus,
        modType != PassiveModType.Absolute || id == SkillId.PassiveRegen).FormatValue();

    protected override SkillDescriptionStat[] GetDescriptionStats(int level)
    {
        string label;
        switch (id)
        {
            case SkillId.PassiveMaxHp: label = "血量上限"; break;
            case SkillId.PassiveAttack: label = "攻击加成"; break;
            case SkillId.PassiveMoveSpeed: label = "移速加成"; break;
            case SkillId.PassiveDefense: label = "防御加成"; break;
            case SkillId.PassiveCritRate: label = "暴击率加成"; break;
            case SkillId.PassiveArmorPen: label = "破防率加成"; break;
            case SkillId.PassiveRegen: label = "每秒回复"; break;
            case SkillId.PassiveAttackRange: label = "范围加成"; break;
            default: label = "属性加成"; break;
        }
        return new[] { SkillDescriptionStat.Bonus(label, BonusAt(level), IsPercentageBonus,
            modType != PassiveModType.Absolute || id == SkillId.PassiveRegen,
            id == SkillId.PassiveRegen ? "最大血量" : "") };
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        var p = PassiveSkillRegistry.Create(id, modType, BonusAt(1));
        if (p == null)
            Debug.LogWarning($"[PassiveSkillDefinition] 未注册的 SkillId={id}");
        return p;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        if (skill is SkillPassive p)
        {
            int lv = ClampLevel(level);
            p.bonusValue = BonusAt(lv);
            if (p.Level > 1) p.ReapplyBonus(); // 仅升级时重新生效，首次由 OnEquip 触发
        }
    }
}
