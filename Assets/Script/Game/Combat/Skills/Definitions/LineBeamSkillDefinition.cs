using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/LineBeam Definition", fileName = "SkillDef_LineBeam")]
public class LineBeamSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.8f, 0.75f, 0.70f, 0.65f, 0.60f };
    public float[] damageByLevel = { 2f, 2.5f, 3f, 3.8f, 4.6f };
    public int[] beamCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("已废弃：多射线由 SkillLineBeam2D 做 360° 随机；仅保留字段兼容旧资源")]
    public float spreadDegrees = 180f;

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
        return $"伤害 {DamageAt(level):0.#}，间隔 {IntervalAt(level):0.##}s，随机散射射线 ×{BeamCountAt(level)}";
    }
}

