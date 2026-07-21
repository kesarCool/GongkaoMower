using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/AutoProjectile Definition", fileName = "SkillDef_AutoProjectile")]
public class AutoProjectileSkillDefinition : SkillDefinitionBase
{
    public override SkillId SkillFamily => SkillId.AutoProjectile;

    [Header("Combat")]
    public SkillId targetSkillId = SkillId.AutoProjectile;
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;

    [Header("Per-level (size must be >= maxLevel)")]
    public float[] intervalByLevel = { 0.5f, 0.45f, 0.40f, 0.36f, 0.32f };
    public float[] damageByLevel = { 50f, 58f, 66f, 76f, 88f };
    public int[] projectileCountByLevel = { 1, 1, 2, 2, 3 };

    [Tooltip("多发散射角（度）。例如 10 表示左右总夹角约 10 度。")]
    public float spreadDegrees = 10f;

    [Header("突破：环绕爆发（满级激活）")]
    [Tooltip("爆发弹丸 Prefab（须挂 AutoProjectileBurstBullet）。留空则不启用突破。")]
    public GameObject burstBulletPrefab;
    [Tooltip("爆发 CD（秒）。")]
    public float burstCooldown = 8f;
    [Tooltip("爆发弹丸数量。")]
    public int burstCount = 8;
    [Tooltip("旋转环绕耗时（秒），0 = 瞬发无旋转。")]
    public float burstOrbitDuration = 1.5f;
    [Tooltip("环绕半径（世界单位）。")]
    public float burstOrbitRadius = 1.2f;
    [Tooltip("飞出速度（0 = 继承 bulletSpeed）。")]
    public float burstLaunchSpeed = 0f;
    [Tooltip("仅 1 敌时所有弹丸集火该目标（符箓特性）。")]
    public bool burstTargetSingleEnemy = false;
    [Tooltip("单敌集火感知范围（世界单位）。")]
    public float burstTargetSingleRange = 10f;
    [Tooltip("全屏穿透：爆发弹丸穿透所有敌人，仅飞出屏幕边界才回收。")]
    public bool burstFullScreenPenetration;

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
        float spd = bulletSpeed;
        float itv = IntervalAt(lv);

        SkillAutoProjectile s = targetSkillId switch
        {
            SkillId.AutoProjectilePistol    => new SkillAutoProjectilePistol(bulletPrefab, spd, itv, targetSkillId),
            SkillId.AutoProjectileSword     => new SkillAutoProjectileSword(bulletPrefab, spd, itv, targetSkillId),
            SkillId.AutoProjectileTalisman  => new SkillAutoProjectileTalisman(bulletPrefab, spd, itv, targetSkillId),
            _                               => new SkillAutoProjectile(bulletPrefab, spd, itv, targetSkillId),
        };

        s.damage = Mathf.Max(0.01f, DamageAt(lv));
        ApplyBurstStats(s);
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
        ApplyBurstStats(s);
    }

    private void ApplyBurstStats(SkillAutoProjectile s)
    {
        s.burstBulletPrefab = burstBulletPrefab;
        s.burstCooldown = burstCooldown;
        s.burstCount = burstCount;
        s.burstOrbitDuration = burstOrbitDuration;
        s.burstOrbitRadius = burstOrbitRadius;
        s.burstLaunchSpeed = burstLaunchSpeed > 0f ? burstLaunchSpeed : bulletSpeed;
        s.burstTargetSingleEnemy = burstTargetSingleEnemy;
        s.burstTargetSingleRange = burstTargetSingleRange;
        s.burstFullScreenPenetration = burstFullScreenPenetration;
        s.burstEnabled = burstBulletPrefab != null;
        s.SetBurstMaxLevel(maxLevel);
        s.maxLevelPrefab = maxLevelPrefab;
    }
}
