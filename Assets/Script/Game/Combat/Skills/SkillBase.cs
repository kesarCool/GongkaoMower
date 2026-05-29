using UnityEngine;

/// <summary>
/// 技能基类：管理等级与装备状态
/// </summary>
public abstract class SkillBase : ISkill
{
    public SkillId Id { get; protected set; }
    public int Level { get; protected set; } = 1;

    protected SkillContext _ctx;
    protected bool _equipped;

    private PlayerSkills _cachedPlayerSkills;

    protected PlayerSkills GetPlayerSkills()
    {
        if (_cachedPlayerSkills == null && _ctx.player != null)
            _cachedPlayerSkills = _ctx.player.GetComponent<PlayerSkills>();
        return _cachedPlayerSkills;
    }

    /// <summary>
    /// 根据 PlayerSkills 的 attackMultiplier + critRate 计算最终伤害。
    /// rawDamage = 技能表 per-level 伤害值。
    /// </summary>
    protected float GetFinalDamage(float rawDamage, out bool isCrit)
    {
        isCrit = false;
        var ps = GetPlayerSkills();
        if (ps == null) return rawDamage;
        float dmg = rawDamage * ps.attackMultiplier;
        dmg *= ps.EvaluateCrit(out isCrit);
        return dmg;
    }

    public virtual void OnEquip(SkillContext ctx)
    {
        _ctx = ctx;
        _equipped = true;
    }

    public virtual void OnUnequip()
    {
        _equipped = false;
    }

    public virtual void OnLevelUp()
    {
        Level = Mathf.Max(1, Level + 1);
    }

    public abstract void Tick(float deltaTime);

    protected static GameObject FindNearestEnemy(Vector3 from, string enemyTag, float maxRange = 9999f)
    {
        return CombatTargetRegistry.FindNearest(enemyTag, from, maxRange);
    }

    protected static int CountActiveEnemiesInRange(Vector3 from, string enemyTag, float maxRange)
    {
        if (string.IsNullOrEmpty(enemyTag))
            return 0;

        return CombatTargetRegistry.CountInRange(enemyTag, from, maxRange);
    }

    protected static Vector2 RandomDirection2D()
    {
        float rad = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    protected void PublishSkillCast(Vector3? worldPosition = null)
    {
        if (Id == SkillId.None) return;

        Vector3 pos = worldPosition ?? (_ctx.player != null ? _ctx.player.position : Vector3.zero);
        EventBus.Publish(new SkillCastEvent
        {
            skillId = Id,
            worldPosition = pos
        });
    }
}
