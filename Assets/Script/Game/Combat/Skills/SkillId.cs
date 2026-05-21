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
}
