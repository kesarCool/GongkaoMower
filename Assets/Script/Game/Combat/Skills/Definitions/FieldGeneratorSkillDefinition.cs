using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/FieldGenerator Definition", fileName = "SkillDef_FieldGenerator")]
public class FieldGeneratorSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    [Tooltip("力场半径（世界单位）")]
    public float[] radiusByLevel = { 1.5f, 1.7f, 1.9f, 2.1f, 2.4f };
    [Tooltip("每秒总伤害倍率，1 = 每秒合计 100% 攻击力。每次跳伤 = 此倍率 × 伤害结算间隔。")]
    public float[] damagePerSecondByLevel = { 15f, 20f, 26f, 33f, 42f };
    [Tooltip("伤害结算间隔（秒），越短越频繁；单次伤害 = DPS × 间隔")]
    public float[] damageTickIntervalByLevel = { 0.25f, 0.22f, 0.19f, 0.16f, 0.13f };
    [Tooltip("力场特效不透明度")]
    public float[] visualIntensityByLevel = { 0.7f, 0.85f, 1f, 1.15f, 1.3f };
    [Tooltip("脉冲周期（秒），越小越快。等级越高脉冲越密。")]
    public float[] pulseCycleDurationByLevel = { 1.4f, 1.2f, 1.0f, 0.85f, 0.7f };

    [Header("Visual")]
    [Tooltip("附着玩家的常驻表现 Prefab，包含 FieldGeneratorPresentation")]
    public GameObject fieldVisualPrefab;
    [Tooltip("Prefab 视觉直径为 1 圈时的世界直径，用于按 radius 缩放")]
    public float visualBaseDiameter = .875f;
    public int sortingOrder = -5;

    public float RadiusAt(int level) => radiusByLevel[Mathf.Clamp(level, 1, radiusByLevel.Length) - 1];
    public float DamagePerSecondAt(int level) => damagePerSecondByLevel[Mathf.Clamp(level, 1, damagePerSecondByLevel.Length) - 1];
    public float DamageTickIntervalAt(int level) => damageTickIntervalByLevel[Mathf.Clamp(level, 1, damageTickIntervalByLevel.Length) - 1];
    public float VisualIntensityAt(int level) => visualIntensityByLevel[Mathf.Clamp(level, 1, visualIntensityByLevel.Length) - 1];
    public float PulseCycleDurationAt(int level) => pulseCycleDurationByLevel[Mathf.Clamp(level, 1, pulseCycleDurationByLevel.Length) - 1];

    protected override SkillDescriptionStat[] GetDescriptionStats(int level)
    {
        // Use the same bounds as ApplyRuntimeStats so the displayed tick matches combat.
        float interval = Mathf.Clamp(DamageTickIntervalAt(level), .05f, 1f);
        float dps = Mathf.Max(.01f, DamagePerSecondAt(level));
        return new[] {
            SkillDescriptionStat.Number("跳伤间隔", interval, "秒"),
            SkillDescriptionStat.Number("每次跳伤", dps * interval * 100f, "%攻击力"),
            SkillDescriptionStat.Number("每秒总伤害", dps * 100f, "%攻击力"),
            SkillDescriptionStat.Number("力场半径", RadiusAt(level), "米")
        };
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        int lv = 1;
        var s = new SkillFieldGenerator
        {
            fieldVisualPrefab = fieldVisualPrefab,
            visualBaseDiameter = visualBaseDiameter,
            sortingOrder = sortingOrder,
            pulseCycleDuration = PulseCycleDurationAt(lv)
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
        s.fieldVisualPrefab = fieldVisualPrefab;
        s.visualBaseDiameter = visualBaseDiameter;
        s.sortingOrder = sortingOrder;
        s.pulseCycleDuration = PulseCycleDurationAt(level);
        s.ApplyRuntimeStats(
            RadiusAt(level),
            DamagePerSecondAt(level),
            DamageTickIntervalAt(level),
            VisualIntensityAt(level));
    }
}
