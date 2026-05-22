using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/LightningStrike Definition", fileName = "SkillDef_LightningStrike")]
public class LightningStrikeSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    [Tooltip("落雷间隔（秒）")]
    public float[] intervalByLevel = { 2.2f, 2f, 1.85f, 1.7f, 1.55f };
    [Tooltip("单次落雷伤害")]
    public float[] damageByLevel = { 80f, 100f, 125f, 155f, 190f };
    [Tooltip("落雷 AOE 半径（世界单位）")]
    public float[] strikeRadiusByLevel = { 1.1f, 1.15f, 1.2f, 1.25f, 1.35f };
    [Tooltip("每次触发落雷道数")]
    public int[] strikeCountByLevel = { 1, 1, 2, 2, 3 };

    [Header("Targeting")]
    [Tooltip("从玩家起算，可选取落雷点的最远距离")]
    public float maxRange = 14f;
    [Tooltip("同一轮多道落雷之间的间隔（秒）")]
    public float strikeStagger = 0.18f;

    [Header("Visual")]
    [Tooltip("落雷命中池化特效（须含 PooledCombatVfx；可后续换成雷电 Prefab）")]
    public GameObject strikeFxPrefab;

    public float IntervalAt(int level) => intervalByLevel[Mathf.Clamp(level, 1, intervalByLevel.Length) - 1];
    public float DamageAt(int level) => damageByLevel[Mathf.Clamp(level, 1, damageByLevel.Length) - 1];
    public float StrikeRadiusAt(int level) => strikeRadiusByLevel[Mathf.Clamp(level, 1, strikeRadiusByLevel.Length) - 1];
    public int StrikeCountAt(int level) => strikeCountByLevel[Mathf.Clamp(level, 1, strikeCountByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        return $"伤害 {DamageAt(level):0.#}，范围 {StrikeRadiusAt(level):0.##}，落雷 ×{StrikeCountAt(level)}，间隔 {IntervalAt(level):0.##}s";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (strikeFxPrefab == null) return null;

        int lv = 1;
        var s = new SkillLightningStrike(strikeFxPrefab);
        s.ApplyRuntimeStats(
            IntervalAt(lv),
            DamageAt(lv),
            StrikeRadiusAt(lv),
            StrikeCountAt(lv),
            strikeStagger,
            maxRange);
        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillLightningStrike;
        if (s == null) return;

        level = ClampLevel(level);
        s.strikeFxPrefab = strikeFxPrefab;
        s.ApplyRuntimeStats(
            IntervalAt(level),
            DamageAt(level),
            StrikeRadiusAt(level),
            StrikeCountAt(level),
            strikeStagger,
            maxRange);
    }
}
