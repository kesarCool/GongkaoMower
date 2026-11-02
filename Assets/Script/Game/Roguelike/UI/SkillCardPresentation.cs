using System;
using System.Globalization;
using UnityEngine;

/// <summary>Formats UI from the existing skill definitions; never changes skill stats.</summary>
public static class SkillCardPresentation
{
    public struct Content
    {
        public string level, gainLabel, gainValue, description;
    }

    public static Content Build(CardDeck.DrawResult card)
    {
        var def = card.skillDef;
        int from = Mathf.Max(1, card.currentLevel);
        int to = def != null ? def.ClampLevel(card.targetLevel) : Mathf.Max(1, card.targetLevel);
        var result = new Content
        {
            level = card.currentLevel == 0 ? $"新获得 · Lv.{to}" : $"Lv.{card.currentLevel} → Lv.{to}",
            gainLabel = card.currentLevel == 0 ? "获得技能" : "本次强化",
            gainValue = $"Lv.{to}",
            description = def == null ? "" : card.currentLevel == 0 ?
                def.GetLevelDescriptionLine(to) : def.GetLevelUpgradeDescription(from, to)
        };
        if (def == null) return result;

        Func<int, string> metric = null;
        if (def is PassiveSkillDefinition passive && passive.bonusByLevel != null && passive.bonusByLevel.Length > 0)
        {
            result.gainLabel = PassiveLabel(card.skillId);
            metric = passive.FormatBonusAt;
        }
        else if (def is AutoProjectileSkillDefinition projectile)
        {
            bool countChanges = card.currentLevel == 0 || projectile.ProjectileCountAt(from) != projectile.ProjectileCountAt(to);
            result.gainLabel = countChanges ? "发射数量" : "单发伤害倍率";
            metric = countChanges ? (Func<int, string>)(lv => $"{projectile.ProjectileCountAt(lv)} 发") : lv => DamagePercent(projectile.DamageAt(lv));
        }
        else if (def is OrbitingBladesSkillDefinition orbit)
        {
            bool countChanges = card.currentLevel == 0 || orbit.BladeCountAt(from) != orbit.BladeCountAt(to);
            result.gainLabel = countChanges ? "环绕飞刀" : "每跳伤害倍率";
            metric = countChanges ? (Func<int, string>)(lv => $"{orbit.BladeCountAt(lv)} 把") : lv => DamagePercent(orbit.DamagePerTickAt(lv));
        }
        else if (def is LineBeamSkillDefinition beam)
        {
            bool countChanges = card.currentLevel == 0 || beam.BeamCountAt(from) != beam.BeamCountAt(to);
            result.gainLabel = countChanges ? "射线数量" : "射线伤害倍率";
            metric = countChanges ? (Func<int, string>)(lv => $"{beam.BeamCountAt(lv)} 道") : lv => DamagePercent(beam.DamageAt(lv));
        }
        else if (def is GrenadeSkillDefinition grenade)
        {
            result.gainLabel = "爆炸伤害倍率";
            metric = lv => DamagePercent(grenade.DamageAt(lv));
        }
        else if (def is FieldGeneratorSkillDefinition field)
        {
            result.gainLabel = "每秒总伤害";
            metric = lv => DamagePercent(field.DamagePerSecondAt(lv));
        }
        else if (def is LightningStrikeSkillDefinition lightning)
        {
            bool countChanges = card.currentLevel == 0 || lightning.StrikeCountAt(from) != lightning.StrikeCountAt(to);
            result.gainLabel = countChanges ? "落雷数量" : "落雷伤害倍率";
            metric = countChanges ? (Func<int, string>)(lv => $"{lightning.StrikeCountAt(lv)} 道") : lv => DamagePercent(lightning.DamageAt(lv));
        }
        else if (def is HomingMissileSkillDefinition missile)
        {
            bool countChanges = card.currentLevel == 0 || missile.SalvoAt(from) != missile.SalvoAt(to);
            result.gainLabel = countChanges ? "导弹数量" : "导弹伤害倍率";
            metric = countChanges ? (Func<int, string>)(lv => $"{missile.SalvoAt(lv)} 发") : lv => DamagePercent(missile.DamageAt(lv));
        }
        if (metric != null)
            result.gainValue = card.currentLevel == 0 ? metric(to) : $"{metric(from)} → {metric(to)}";
        return result;
    }

    // SkillBase multiplies these per-level values by player attack; 1.25 means 125%, not 1.3 damage.
    private static string DamagePercent(float multiplier)
    {
        return (multiplier * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }

    private static string PassiveLabel(SkillId id)
    {
        switch (id)
        {
            case SkillId.PassiveMaxHp: return "血量上限";
            case SkillId.PassiveAttack: return "攻击提升";
            case SkillId.PassiveMoveSpeed: return "移动速度";
            case SkillId.PassiveDefense: return "防御提升";
            case SkillId.PassiveCritRate: return "暴击几率";
            case SkillId.PassiveRegen: return "每秒回复";
            case SkillId.PassiveArmorPen: return "破防几率";
            default: return "属性提升";
        }
    }
}
