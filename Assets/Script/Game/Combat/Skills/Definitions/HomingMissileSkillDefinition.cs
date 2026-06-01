using UnityEngine;

/// <summary>
/// 追踪导弹技能定义：多目标锁定，连发追踪导弹，命中后 AOE 爆炸。
/// </summary>
[CreateAssetMenu(menuName = "Game/Skills/HomingMissile Definition", fileName = "SkillDef_HomingMissile")]
public class HomingMissileSkillDefinition : SkillDefinitionBase
{
    public override SkillId SkillFamily => SkillId.HomingMissileBasic;

    [Header("战斗")]
    public SkillId targetSkillId = SkillId.HomingMissile;
    public GameObject missilePrefab;
    public float missileSpeed = 8f;
    public float turnRate = 180f;
    public float missileLifetime = 4f;
    public GameObject explosionFxPrefab;
    public float maxRange = 10f;
    public float salvoInterval = 0.2f;

    [Header("Per-level (size >= maxLevel)")]
    public float[] cooldownByLevel   = { 2.0f, 1.8f, 1.6f, 1.4f, 1.2f };
    public float[] damageByLevel     = { 1.0f, 1.15f, 1.32f, 1.52f, 1.75f };
    public int[]   salvoByLevel      = { 2, 3, 4, 5, 6 };
    public int[]   maxTargetsByLevel = { 2, 3, 3, 4, 5 };
    public float[] aoeRadiusByLevel  = { 1.2f, 1.3f, 1.4f, 1.5f, 1.7f };

    public float CooldownAt(int lv)   => cooldownByLevel[Mathf.Clamp(lv, 1, cooldownByLevel.Length) - 1];
    public float DamageAt(int lv)     => damageByLevel[Mathf.Clamp(lv, 1, damageByLevel.Length) - 1];
    public int   SalvoAt(int lv)      => salvoByLevel[Mathf.Clamp(lv, 1, salvoByLevel.Length) - 1];
    public int   MaxTargetsAt(int lv) => maxTargetsByLevel[Mathf.Clamp(lv, 1, maxTargetsByLevel.Length) - 1];
    public float AoeRadiusAt(int lv)  => aoeRadiusByLevel[Mathf.Clamp(lv, 1, aoeRadiusByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        int lv = ClampLevel(level);
        return $"CD{CooldownAt(lv):0.#}s, {SalvoAt(lv)}发, AOE{AoeRadiusAt(lv):0.#}m";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (missilePrefab == null) return null;

        int lv = 1;
        var s = new SkillHomingMissile(missilePrefab, targetSkillId);
        ApplyStats(s, lv);
        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        if (skill is SkillHomingMissile s)
            ApplyStats(s, ClampLevel(level));
    }

    private void ApplyStats(SkillHomingMissile s, int lv)
    {
        s.missileSpeed = missileSpeed;
        s.turnRate = turnRate;
        s.missileLifetime = missileLifetime;
        s.explosionFxPrefab = explosionFxPrefab;
        s.maxRange = maxRange;
        s.salvoInterval = salvoInterval;
        s.cooldown = CooldownAt(lv);
        s.damage = DamageAt(lv);
        s.salvoCount = SalvoAt(lv);
        s.maxTargets = MaxTargetsAt(lv);
        s.aoeRadius = AoeRadiusAt(lv);
        s.maxLevelPrefab = maxLevelPrefab;
        s.skillMaxLevel = maxLevel;
    }
}
