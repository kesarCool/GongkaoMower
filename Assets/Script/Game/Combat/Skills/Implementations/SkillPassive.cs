using System;
using UnityEngine;

/// <summary>
/// 被动技能基类：OnEquip 应用加成，OnUnequip 回退，Tick 空（子类可按需覆写）。
/// </summary>
public abstract class SkillPassive : SkillBase
{
    public float bonusValue;
    public PassiveModType modType;

    protected float _originalValue;

    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx);
        ApplyBonus(bonusValue);
    }

    public override void OnUnequip()
    {
        RemoveBonus();
        base.OnUnequip();
    }

    public override void Tick(float deltaTime) { /* NOOP for stat passives */ }

    /// <summary>升级后重新生效（先还原旧值再应用新值）。</summary>
    public void ReapplyBonus()
    {
        RemoveBonus();
        ApplyBonus(bonusValue);
    }

    protected abstract void ApplyBonus(float value);
    protected abstract void RemoveBonus();
}

/// <summary>
/// L1 纯数值被动：对 PlayerHealth / PlayerSkills / PlayerController 的单个字段做加成。
/// 通过 SkillCatalog 注册时传入目标字段路径（字段名），OnEquip 通过反射读取/写入。
/// </summary>
public class SkillPassiveStat : SkillPassive
{
    private readonly Func<float> _getter;
    private readonly Action<float> _setter;

    public SkillPassiveStat(SkillId id, PassiveModType type, float bonus,
        Func<float> getter, Action<float> setter)
    {
        Id = id;
        modType = type;
        bonusValue = bonus;
        _getter = getter;
        _setter = setter;
    }

    protected override void ApplyBonus(float value)
    {
        if (_getter == null || _setter == null) return;
        _originalValue = _getter();

        float newValue;
        switch (modType)
        {
            case PassiveModType.Multiplicative:
                newValue = _originalValue * (1f + value);
                break;
            case PassiveModType.Additive:
                newValue = _originalValue + value;
                break;
            case PassiveModType.Absolute:
                newValue = value;
                break;
            default:
                newValue = _originalValue;
                break;
        }

        // 特殊处理：暴击率需要 Clamp
        if (Id == SkillId.PassiveCritRate)
            newValue = Mathf.Clamp01(newValue);

        _setter(newValue);
        GameLog.Info($"[PassiveStat] {Id} Lv.{Level} {modType}: {_originalValue:0.##} → {newValue:0.##} (bonus={value:0.##})");
    }

    protected override void RemoveBonus()
    {
        if (_setter != null)
            _setter(_originalValue);
    }
}

/// <summary>
/// 每秒回血被动：Tick 中累加时间，>=1s 时回血。
/// </summary>
public class SkillPassiveRegen : SkillPassive
{
    private float _regenAccum;

    public SkillPassiveRegen(SkillId id, float hpPercentPerSec)
    {
        Id = id;
        bonusValue = hpPercentPerSec;
        modType = PassiveModType.Absolute;
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped || _ctx.player == null) return;

        _regenAccum += deltaTime;
        if (_regenAccum < 1f) return;
        _regenAccum -= 1f;

        var health = _ctx.player.GetComponent<PlayerHealth>();
        if (health != null && health.IsAlive)
            health.Heal(health.MaxHp * bonusValue);
    }

    protected override void ApplyBonus(float value) { /* 无 OnEquip 瞬时效果 */ }
    protected override void RemoveBonus() { }
}
