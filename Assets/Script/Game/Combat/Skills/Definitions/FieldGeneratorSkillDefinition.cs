using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/FieldGenerator Definition", fileName = "SkillDef_FieldGenerator")]
public class FieldGeneratorSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    [Tooltip("力场半径（世界单位）")]
    public float[] radiusByLevel = { 1.5f, 1.7f, 1.9f, 2.1f, 2.4f };
    [Tooltip("每秒伤害（DPS）")]
    public float[] damagePerSecondByLevel = { 15f, 20f, 26f, 33f, 42f };
    [Tooltip("伤害结算间隔（秒），越短越频繁；单次伤害 = DPS × 间隔")]
    public float[] damageTickIntervalByLevel = { 0.25f, 0.22f, 0.19f, 0.16f, 0.13f };
    [Tooltip("力场特效强度（粒子 emission 倍率）")]
    public float[] visualIntensityByLevel = { 0.7f, 0.85f, 1f, 1.15f, 1.3f };

    [Header("Visual")]
    [Tooltip("附着玩家的力场特效 Prefab（循环粒子，如 fx-Goku-Supper）")]
    public GameObject fieldVisualPrefab;
    [Tooltip("Prefab 视觉直径为 1 圈时的世界直径，用于按 radius 缩放")]
    public float visualBaseDiameter = 10f;
    public int sortingOrder = 30;

    public float RadiusAt(int level) => radiusByLevel[Mathf.Clamp(level, 1, radiusByLevel.Length) - 1];
    public float DamagePerSecondAt(int level) => damagePerSecondByLevel[Mathf.Clamp(level, 1, damagePerSecondByLevel.Length) - 1];
    public float DamageTickIntervalAt(int level) => damageTickIntervalByLevel[Mathf.Clamp(level, 1, damageTickIntervalByLevel.Length) - 1];
    public float VisualIntensityAt(int level) => visualIntensityByLevel[Mathf.Clamp(level, 1, visualIntensityByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        return $"力场半径 {RadiusAt(level):0.##}，{DamagePerSecondAt(level):0.#} DPS，间隔 {DamageTickIntervalAt(level):0.##}s";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (fieldVisualPrefab == null) return null;

        int lv = 1;
        var s = new SkillFieldGenerator(fieldVisualPrefab)
        {
            visualBaseDiameter = visualBaseDiameter,
            sortingOrder = sortingOrder
        };
        s.ApplyRuntimeStats(
            RadiusAt(lv),
            DamagePerSecondAt(lv),
            DamageTickIntervalAt(lv),
            VisualIntensityAt(lv));
        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillFieldGenerator;
        if (s == null) return;

        level = ClampLevel(level);
        s.visualBaseDiameter = visualBaseDiameter;
        s.sortingOrder = sortingOrder;
        s.ApplyRuntimeStats(
            RadiusAt(level),
            DamagePerSecondAt(level),
            DamageTickIntervalAt(level),
            VisualIntensityAt(level));
    }
}
