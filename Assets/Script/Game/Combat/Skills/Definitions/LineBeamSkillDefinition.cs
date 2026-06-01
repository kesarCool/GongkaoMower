using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/LineBeam Definition", fileName = "SkillDef_LineBeam")]
public class LineBeamSkillDefinition : SkillDefinitionBase
{
    [Header("Combat")]
    public float beamLength = 8f;
    public LayerMask beamHitMask = ~0;

    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.8f, 0.75f, 0.70f, 0.65f, 0.60f };
    public float[] damageByLevel = { 2f, 2.5f, 3f, 3.8f, 4.6f };
    public int[] beamCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("仅 1 只怪时多射线的散布角度（度），避免全部重叠。")]
    public float singleTargetSpreadDeg = 10f;

    [Tooltip("已废弃：多射线由 SkillLineBeam2D 做 360° 随机；仅保留字段兼容旧资源")]
    public float spreadDegrees = 180f;

    [Header("满级变体")]
    [Tooltip("每 N 发普攻触发一次变体（如七色光）。")]
    public int maxLevelVariantInterval = 5;
    [Tooltip("变体时的光束数（如 7 条彩虹）。")]
    public int variantBeamCount = 7;

    public float IntervalAt(int level) => intervalByLevel[Mathf.Clamp(level, 1, intervalByLevel.Length) - 1];
    public float DamageAt(int level) => damageByLevel[Mathf.Clamp(level, 1, damageByLevel.Length) - 1];
    public int BeamCountAt(int level)
    {
        if (beamCountByLevel == null || beamCountByLevel.Length == 0)
            return 1;
        return beamCountByLevel[Mathf.Clamp(level, 1, beamCountByLevel.Length) - 1];
    }

    protected override string GenerateLevelDescription(int level)
    {
        return $"伤害{DamageAt(level):0.#},间隔{IntervalAt(level):0.##}s,随机散射射线x{BeamCountAt(level)}";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        int lv = 1;
        var s = new SkillLineBeam2D(beamLength, DamageAt(lv), IntervalAt(lv), beamHitMask);
        if (bindings != null)
        {
            s.visualDuration = bindings.beamVisualDuration;
            bindings.configureLineBeam?.Invoke(s);
        }

        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillLineBeam2D;
        if (s == null) return;

        level = ClampLevel(level);
        s.interval = Mathf.Max(0.05f, IntervalAt(level));
        s.damage = Mathf.Max(0.01f, DamageAt(level));
        s.beamCount = Mathf.Max(1, BeamCountAt(level));
        s.singleTargetSpreadDeg = Mathf.Max(0f, singleTargetSpreadDeg);
        s.maxLevelVariantInterval = maxLevelVariantInterval;
        s.variantBeamCount = variantBeamCount;
        s.skillMaxLevel = maxLevel;
    }
}
