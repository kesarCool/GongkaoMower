/// <summary>
/// 技能 ID（强类型枚举，后续与肉鸽卡牌、表格配置对齐）
/// </summary>
public enum SkillId
{
    None = 0,
    /// <summary>自动索敌发射子弹（可由 PlayerController 迁移而来）</summary>
    AutoProjectile = 1,
    /// <summary>射线/线段范围伤害（2D RaycastAll）</summary>
    LineBeam = 2,
    /// <summary>环绕刀片（子物体旋转 + Trigger 伤害）</summary>
    OrbitingBlades = 3,
    /// <summary>固定 CD 向最近敌人抛物线投掷，落地圆形 AOE</summary>
    ThrowGrenade = 4,
    /// <summary>附着玩家的持续溶解力场（圆形范围 DPS）</summary>
    FieldGenerator = 5,
    /// <summary>随机落雷，范围内 AOE 伤害；升级增加道数与伤害</summary>
    LightningStrike = 6,
    /// <summary>自动索敌（手枪版）—— 奋斗哥</summary>
    AutoProjectilePistol = 7,
    /// <summary>自动索敌（飞刀版）—— 上岸侠</summary>
    AutoProjectileSword = 8,
    /// <summary>自动索敌（符箓版）—— 茅山道士</summary>
    AutoProjectileTalisman = 9,
    /// <summary>追踪导弹，命中后 AOE 爆炸 —— 机甲小宝专属</summary>
    HomingMissile = 10,
    /// <summary>追踪弹（弱化版）—— 全角色通用</summary>
    HomingMissileBasic = 11,
    /// <summary>弹射雷，命中后弹跳再爆 —— 熊猫侠专属</summary>
    BouncingGrenade = 12,

    // ── 被动技能（101+，预留 13~100 给主动技能扩展）──
    /// <summary>血量上限 +X%</summary>
    PassiveMaxHp = 101,
    /// <summary>攻击 +X%</summary>
    PassiveAttack = 102,
    /// <summary>移速 +X%</summary>
    PassiveMoveSpeed = 103,
    /// <summary>防御 +X%</summary>
    PassiveDefense = 104,
    /// <summary>暴击率 +X%</summary>
    PassiveCritRate = 105,
    /// <summary>每秒恢复 X% 最大血量</summary>
    PassiveRegen = 106,
    /// <summary>攻击范围 +X%</summary>
    PassiveAttackRange = 107,
    /// <summary>破防率 +X%</summary>
    PassiveArmorPen = 108,
}

public static class SkillIdExtensions
{
    public static bool IsPassive(this SkillId id) => (int)id >= 100;
}
