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
    [Tooltip("环绕刀片战斗体 Prefab（含碰撞、SkillOrbBladeHit、OrbitingBladeVisual）；为空则回退代码拼 Sprite")]
    public GameObject bladePrefab;
    public Sprite bladeSprite;
    public Color bladeTint = Color.white;
    public int sortingOrder = 50;
    public float visualScale = 1f;

    public int BladeCountAt(int level) => bladeCountByLevel[Mathf.Clamp(level, 1, bladeCountByLevel.Length) - 1];
    public float DamagePerTickAt(int level) => damagePerTickByLevel[Mathf.Clamp(level, 1, damagePerTickByLevel.Length) - 1];
    public float TickIntervalAt(int level) => tickIntervalByLevel[Mathf.Clamp(level, 1, tickIntervalByLevel.Length) - 1];
    public float OrbitRadiusAt(int level) => orbitRadiusByLevel[Mathf.Clamp(level, 1, orbitRadiusByLevel.Length) - 1];
    public float RotateSpeedAt(int level) => rotateSpeedByLevel[Mathf.Clamp(level, 1, rotateSpeedByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        return $"飞刀 ×{BladeCountAt(level)}，每跳伤害 {DamagePerTickAt(level):0.#}，间隔 {TickIntervalAt(level):0.##}s";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (bladePrefab == null) return null;

        int lv = 1;
        return new SkillOrbitingBlades(
            bladePrefab,
            bladeSprite,
            BladeCountAt(lv),
            OrbitRadiusAt(lv),
            RotateSpeedAt(lv),
            DamagePerTickAt(lv),
            TickIntervalAt(lv),
            sortingOrder,
            bladeTint,
            visualScale);
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillOrbitingBlades;
        if (s == null) return;

        level = ClampLevel(level);
        s.ApplyRuntimeStats(
            Mathf.Max(1, BladeCountAt(level)),
            OrbitRadiusAt(level),
            RotateSpeedAt(level),
            DamagePerTickAt(level),
            TickIntervalAt(level));
    }
}

