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

    protected override string GenerateLevelDescription(int level)
    {
        float b = BonusAt(level);
        return $"+{b * 100f:F0}%";
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
