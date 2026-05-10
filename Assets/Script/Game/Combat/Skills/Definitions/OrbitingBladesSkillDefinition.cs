using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/OrbitingBlades Definition", fileName = "SkillDef_OrbitingBlades")]
public class OrbitingBladesSkillDefinition : SkillDefinitionBase
{
    [Header("Per-level (size must be >= maxLevel)")]
    public int[] bladeCountByLevel = { 2, 3, 4, 5, 6 };
    public float[] damagePerTickByLevel = { 1f, 1.2f, 1.5f, 1.9f, 2.4f };
    public float[] tickIntervalByLevel = { 0.15f, 0.15f, 0.14f, 0.13f, 0.12f };
    public float[] orbitRadiusByLevel = { 1.2f, 1.25f, 1.3f, 1.35f, 1.4f };
    public float[] rotateSpeedByLevel = { 180f, 200f, 220f, 240f, 260f };

    [Header("Visual")]
    public Sprite bladeSprite;
    public Color bladeTint = Color.white;
    public int sortingOrder = 50;
    public float visualScale = 1f;

    public int BladeCountAt(int level) => bladeCountByLevel[Mathf.Clamp(level, 1, bladeCountByLevel.Length) - 1];
    public float DamagePerTickAt(int level) => damagePerTickByLevel[Mathf.Clamp(level, 1, damagePerTickByLevel.Length) - 1];
    public float TickIntervalAt(int level) => tickIntervalByLevel[Mathf.Clamp(level, 1, tickIntervalByLevel.Length) - 1];
    public float OrbitRadiusAt(int level) => orbitRadiusByLevel[Mathf.Clamp(level, 1, orbitRadiusByLevel.Length) - 1];
    public float RotateSpeedAt(int level) => rotateSpeedByLevel[Mathf.Clamp(level, 1, rotateSpeedByLevel.Length) - 1];
}

