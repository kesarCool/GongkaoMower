/// <summary>Rare 阶位解锁的英雄特质类型。</summary>
public enum HeroTraitType
{
    None = 0,
    /// <summary>击杀叠攻：killsPerStack / maxStacks / attackPerStack% / moveSpeedPerStack% / durationSec</summary>
    KillStreak = 1,
    /// <summary>伤害光环：radius / damagePerSecMul / tickIntervalSec</summary>
    DamageAura = 2,
    /// <summary>反应护盾：maxShields / cooldownSec / knockbackRadius / knockbackForce</summary>
    ReactiveShield = 3,
    /// <summary>低血增伤：hpThreshold% / attackBonus% / moveSpeedBonus%</summary>
    Berserk = 4,
    /// <summary>击杀回血：healHpPercent% / pickupDurationSec / maxPickups</summary>
    VampiricHeal = 5,
    /// <summary>符箓环绕：maxTalismans / orbitRadius / orbitSpeed / flySpeed / arrivalDist / collisionRadius / contactDamage</summary>
    TalismanOrbit = 6,
}
