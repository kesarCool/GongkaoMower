using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/ThrowGrenade Definition", fileName = "SkillDef_ThrowGrenade")]
public class GrenadeSkillDefinition : SkillDefinitionBase
{
    public override SkillId SkillFamily => SkillId.ThrowGrenade;
    public SkillId targetSkillId = SkillId.ThrowGrenade;

    [Header("Per-level (size must be >= maxLevel)")]
    [Tooltip("投掷冷却（秒）")]
    public float[] cooldownByLevel = { 3f, 2.7f, 2.4f, 2.1f, 1.8f };
    public float[] damageByLevel = { 1f, 1.15f, 1.3f, 1.5f, 1.75f };
    [Tooltip("落地 AOE 半径（世界单位）")]
    public float[] aoeRadiusByLevel = { 1.2f, 1.35f, 1.5f, 1.65f, 1.85f };
    public float[] arcHeightByLevel = { 1f, 1.1f, 1.2f, 1.3f, 1.4f };
    [Tooltip("弹射次数")]
    public int[] maxBouncesByLevel = { 0, 1, 1, 2, 3 };

    [Header("Flight")]
    public float baseFlightTime = 0.45f;
    public float flightTimePerUnitDistance = 0.06f;
    public float minFlightTime = 0.35f;
    public float maxFlightTime = 1.1f;
    public float maxTargetRange = 14f;

    [Header("Visual")]
    public GameObject grenadePrefab;
    [Tooltip("落地爆炸池化特效 Prefab（须含 PooledCombatVfx；由 CombatVfxSpawner 播放）")]
    public GameObject explosionFxPrefab;

    [Header("弹射配置")]
    public float bounceSearchRadius = 8f;
    public float bounceArcHeight = 2f;
    public float bounceFlightTime;

    public float CooldownAt(int level) => cooldownByLevel[Mathf.Clamp(level, 1, cooldownByLevel.Length) - 1];
    public float DamageAt(int level) => damageByLevel[Mathf.Clamp(level, 1, damageByLevel.Length) - 1];
    public float AoeRadiusAt(int level) => aoeRadiusByLevel[Mathf.Clamp(level, 1, aoeRadiusByLevel.Length) - 1];
    public float ArcHeightAt(int level) => arcHeightByLevel[Mathf.Clamp(level, 1, arcHeightByLevel.Length) - 1];
    public int MaxBouncesAt(int level) => maxBouncesByLevel[Mathf.Clamp(level, 1, maxBouncesByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        return $"伤害{DamageAt(level):0.#},CD {CooldownAt(level):0.##}s,范围 {AoeRadiusAt(level):0.##}";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (grenadePrefab == null) return null;

        int lv = 1;

        var s = new SkillThrowGrenade(grenadePrefab, CooldownAt(lv), targetSkillId);
        s.explosionFxPrefab = explosionFxPrefab;
        s.damage = Mathf.Max(0.01f, DamageAt(lv));
        s.aoeRadius = Mathf.Max(0.1f, AoeRadiusAt(lv));
        s.arcHeight = Mathf.Max(0f, ArcHeightAt(lv));
        s.baseFlightTime = baseFlightTime;
        s.flightTimePerUnitDistance = flightTimePerUnitDistance;
        s.minFlightTime = minFlightTime;
        s.maxFlightTime = maxFlightTime;
        s.maxTargetRange = maxTargetRange;
        s.maxBounces = MaxBouncesAt(lv);
        s.bounceSearchRadius = bounceSearchRadius;
        s.bounceArcHeight = bounceArcHeight;
        s.bounceFlightTime = bounceFlightTime;
        s.maxLevelPrefab = maxLevelPrefab;
        s.skillMaxLevel = maxLevel;
        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillThrowGrenade;
        if (s == null) return;

        level = ClampLevel(level);
        s.cooldown = Mathf.Max(0.2f, CooldownAt(level));
        s.damage = Mathf.Max(0.01f, DamageAt(level));
        s.aoeRadius = Mathf.Max(0.1f, AoeRadiusAt(level));
        s.arcHeight = Mathf.Max(0f, ArcHeightAt(level));
        s.baseFlightTime = baseFlightTime;
        s.flightTimePerUnitDistance = flightTimePerUnitDistance;
        s.minFlightTime = minFlightTime;
        s.maxFlightTime = maxFlightTime;
        s.maxTargetRange = maxTargetRange;
        s.maxBounces = MaxBouncesAt(level);
        s.bounceSearchRadius = bounceSearchRadius;
        s.bounceArcHeight = bounceArcHeight;
        s.bounceFlightTime = bounceFlightTime;
        s.maxLevelPrefab = maxLevelPrefab;
        s.skillMaxLevel = maxLevel;
        if (s.explosionFxPrefab == null)
            s.explosionFxPrefab = explosionFxPrefab;
    }
}
