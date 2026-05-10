using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/LineBeam Definition", fileName = "SkillDef_LineBeam")]
public class LineBeamSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.8f, 0.75f, 0.70f, 0.65f, 0.60f };
    public float[] damageByLevel = { 2f, 2.5f, 3f, 3.8f, 4.6f };
    public int[] beamCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("多射线散射角（度）。")]
    public float spreadDegrees = 14f;

    public float IntervalAt(int level) => intervalByLevel[Mathf.Clamp(level, 1, intervalByLevel.Length) - 1];
    public float DamageAt(int level) => damageByLevel[Mathf.Clamp(level, 1, damageByLevel.Length) - 1];
    public int BeamCountAt(int level) => beamCountByLevel[Mathf.Clamp(level, 1, beamCountByLevel.Length) - 1];
}

