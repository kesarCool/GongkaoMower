using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/AutoProjectile Definition", fileName = "SkillDef_AutoProjectile")]
public class AutoProjectileSkillDefinition : SkillDefinitionBase
{
    [Header("Combat")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;

    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.5f, 0.45f, 0.40f, 0.36f, 0.32f };
    public float[] damageByLevel = { 50f, 58f, 66f, 76f, 88f };
    public int[] projectileCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("多发散射角（度）。例如 10 表示左右总夹角约 10 度。")]
    public float spreadDegrees = 10f;

    public float IntervalAt(int level) => intervalByLevel[Mathf.Clamp(level, 1, intervalByLevel.Length) - 1];
    public float DamageAt(int level) => damageByLevel[Mathf.Clamp(level, 1, damageByLevel.Length) - 1];
    public int ProjectileCountAt(int level) => projectileCountByLevel[Mathf.Clamp(level, 1, projectileCountByLevel.Length) - 1];

    protected override string GenerateLevelDescription(int level)
    {
        float interval = IntervalAt(level);
        int count = ProjectileCountAt(level);
        return $"伤害{DamageAt(level):0.#},射速{interval:0.##}s,子弹x{count}";
    }

    protected override ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings)
    {
        if (bulletPrefab == null) return null;

        int lv = 1;
        var s = new SkillAutoProjectile(bulletPrefab, bulletSpeed, IntervalAt(lv));
        s.damage = Mathf.Max(0.01f, DamageAt(lv));
        return s;
    }

    public override void ApplyStatsToSkill(ISkill skill, int level)
    {
        var s = skill as SkillAutoProjectile;
        if (s == null) return;

        level = ClampLevel(level);
        s.interval = Mathf.Max(0.05f, IntervalAt(level));
        s.damage = Mathf.Max(0.01f, DamageAt(level));
        s.projectileCount = Mathf.Max(1, ProjectileCountAt(level));
        s.spreadDegrees = Mathf.Max(0f, spreadDegrees);
    }
}
