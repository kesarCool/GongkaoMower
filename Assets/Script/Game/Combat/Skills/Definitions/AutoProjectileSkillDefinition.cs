using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/AutoProjectile Definition", fileName = "SkillDef_AutoProjectile")]
public class AutoProjectileSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.5f, 0.45f, 0.40f, 0.36f, 0.32f };
    public int[] projectileCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("多发散射角（度）。例如 10 表示左右总夹角约 10 度。")]
    public float spreadDegrees = 10f;

    public float IntervalAt(int level) => intervalByLevel[Mathf.Clamp(level, 1, intervalByLevel.Length) - 1];
    public int ProjectileCountAt(int level) => projectileCountByLevel[Mathf.Clamp(level, 1, projectileCountByLevel.Length) - 1];
}

