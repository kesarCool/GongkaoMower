/// <summary>
/// 技能伤害形态分类，供 Boss 免伤/抵抗技能按类型过滤。
/// </summary>
public enum SkillDamageType
{
    /// <summary>实体弹丸、刀片、投掷物、弹射雷。</summary>
    Physical,
    /// <summary>射线、力场、落雷、符箓。</summary>
    Energy,
    /// <summary>导弹、爆炸类。</summary>
    Explosive,
}

public static class SkillDamageTypeExtensions
{
    /// <summary>返回技能 ID 对应的伤害形态。</summary>
    public static SkillDamageType GetDamageType(this SkillId id)
    {
        switch (id)
        {
            // ── Physical：实体弹丸 / 刀片 / 投掷 ──
            case SkillId.AutoProjectile:
            case SkillId.AutoProjectilePistol:
            case SkillId.AutoProjectileSword:
            case SkillId.OrbitingBlades:
            case SkillId.ThrowGrenade:
            case SkillId.BouncingGrenade:
                return SkillDamageType.Physical;

            // ── Energy：射线 / 力场 / 落雷 / 符箓 ──
            case SkillId.LineBeam:
            case SkillId.FieldGenerator:
            case SkillId.LightningStrike:
            case SkillId.AutoProjectileTalisman:
                return SkillDamageType.Energy;

            // ── Explosive：导弹 ──
            case SkillId.HomingMissile:
            case SkillId.HomingMissileBasic:
                return SkillDamageType.Explosive;

            default:
                return SkillDamageType.Physical;
        }
    }
}
