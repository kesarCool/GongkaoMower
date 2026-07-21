using UnityEngine;

/// <summary>
/// 自动索敌投射物基类：周期性向最近敌人方向发射子弹。
/// 变体子类重写 ApplyBreakthroughStats 实现差异化 Legend 突破。
/// </summary>
public class SkillAutoProjectile : SkillBase
{
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    public float interval = 0.5f;
    public float damage = 50f;
    public int projectileCount = 1;
    public float spreadDegrees = 10f;
    public GameObject maxLevelPrefab;

    // 满级突破：环绕爆发
    public bool burstEnabled;
    public GameObject burstBulletPrefab;
    public float burstCooldown = 8f;
    public int burstCount = 8;
    public float burstOrbitDuration = 1.5f;
    public float burstOrbitRadius = 1.2f;
    public float burstLaunchSpeed = 10f;
    public bool burstTargetSingleEnemy;
    public float burstTargetSingleRange = 10f;
    public bool burstFullScreenPenetration;

    private float _timer;
    protected float _burstTimer;
    private int _burstMaxLevel = 5;

    public void SetBurstMaxLevel(int maxLv) { _burstMaxLevel = maxLv; }

    protected bool IsBurstReady => burstEnabled && Level >= _burstMaxLevel;

    public SkillAutoProjectile(GameObject bulletPrefab, float bulletSpeed, float interval, SkillId skillId = SkillId.AutoProjectile)
    {
        Id = skillId;
        this.bulletPrefab = bulletPrefab;
        this.bulletSpeed = bulletSpeed;
        this.interval = Mathf.Max(0.05f, interval);
    }

    // ── Legend 突破（基类：projectileCount +2）──

    protected int _legendStage;
    private int _baseProjectileCount;
    protected bool _needsCritSplit;
    protected bool _needsHoming;

    public override void ApplyLegendBreakthrough(int stage)
    {
        _legendStage = stage;
        ApplyBreakthroughStats();
    }

    public override void OnAfterStatsApplied()
    {
        if (_legendStage >= 2) ApplyBreakthroughStats();
    }

    /// <summary>Legend 突破普攻弹数增量（子类可重写）。</summary>
    protected virtual int LegendProjectileBonus => 2;

    protected virtual void ApplyBreakthroughStats()
    {
        if (_legendStage < 2) return;

        _baseProjectileCount = projectileCount;
        projectileCount = _baseProjectileCount + LegendProjectileBonus;
        // Debug.Log($"[SkillAutoProjectile] Legend 突破（基础）：projectileCount={projectileCount}, bonus=+{LegendProjectileBonus}");
    }

    // ── Tick ──

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        TryFireBurst(deltaTime);

        if (bulletPrefab == null) return;

        _timer += deltaTime;
        if (_timer < interval) return;

        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Bullet", out _))
            {
                _timer = interval * 0.5f;
                return;
            }
        }

        _timer = 0f;

        Vector3 from = _ctx.player.position;
        GameObject enemy = FindNearestEnemy(from, _ctx.enemyTag);
        if (enemy == null) return;

        Vector2 dir = (Vector2)(enemy.transform.position - from);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        int count = Mathf.Max(1, projectileCount);
        float total = Mathf.Max(0f, spreadDegrees);
        float step = count <= 1 ? 0f : total / (count - 1);
        float start = -total * 0.5f;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (SpawnLimiter.Instance != null)
            {
                if (!SpawnLimiter.Instance.CanSpawn("Bullet", out _))
                    break;
            }

            float ang = start + step * i;
            Vector2 d = Quaternion.Euler(0f, 0f, ang) * dir;

            GameObject prefab = (maxLevelPrefab != null && Level >= _burstMaxLevel) ? maxLevelPrefab : bulletPrefab;
            GameObject bullet = GameObjectPool.Get(prefab, from, Quaternion.identity);
            if (bullet == null)
                bullet = Object.Instantiate(prefab, from, Quaternion.identity);

            // 对象池旧实例可能缺组件，运行时补上
            if (_needsCritSplit && prefab == maxLevelPrefab && bullet.GetComponent<CritSplitOnHit>() == null)
            {
                var cs = bullet.AddComponent<CritSplitOnHit>();
                cs.splitCount = 3;
                cs.splitDmgMul = 0.4f;
                cs.splitLifetime = 1.2f;
                cs.splitBulletPrefab = bulletPrefab;
            }
            if (_needsHoming && bullet.GetComponent<HomingOverride>() == null)
            {
                var h = bullet.AddComponent<HomingOverride>();
                h.turnRate = 80f;
                h.homingRange = 6f;
            }

            PlayerBullet pb = bullet.GetComponent<PlayerBullet>();
            if (pb != null)
            {
                float finalDmg = GetFinalDamage(damage, out bool isCrit, out bool isPenetration);
                var ps = GetPlayerSkills();
                var p = new BulletLaunchParams(bulletSpeed, finalDmg, 5f, Id,
                    ps != null ? ps.pierceCount : 0,
                    isCrit,
                    ps != null ? ps.pierceRate : 0f,
                    isPenetration);
                pb.Launch(d, p);
                if (pb is ScatterBullet sc)
                    sc.SetPlayerRef(_ctx.player);
            }
            else
            {
                Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
                if (rb != null) rb.velocity = d * bulletSpeed;
            }

            SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);
            spawned++;
        }

        if (spawned > 0)
        {
            // Debug.Log($"[SkillAutoProjectile] 普攻射击：spawned={spawned}, projectileCount={projectileCount}, level={Level}, legendStage={_legendStage}");
            PublishSkillCast(from);
        }
    }

    // ── 爆发 ──

    protected virtual void TryFireBurst(float deltaTime)
    {
        if (!IsBurstReady) return;

        if (burstBulletPrefab == null) return;
        if (_ctx.player == null) return;

        _burstTimer += deltaTime;
        if (_burstTimer < burstCooldown) return;
        _burstTimer = 0f;

        Vector3 playerPos = _ctx.player.position;

        Vector2? aimTarget = null;
        if (burstTargetSingleEnemy)
        {
            if (CountActiveEnemiesInRange(playerPos, _ctx.enemyTag, burstTargetSingleRange) == 1)
            {
                var nearest = FindNearestEnemy(playerPos, _ctx.enemyTag, burstTargetSingleRange);
                if (nearest != null)
                    aimTarget = nearest.transform.position;
            }
        }

        for (int i = 0; i < burstCount; i++)
        {
            float startAngle = 360f / burstCount * i;
            Vector2 orbitSpawnDir = Quaternion.Euler(0f, 0f, startAngle) * Vector2.right;
            Vector3 pos = playerPos + (Vector3)(orbitSpawnDir * burstOrbitRadius);

            Vector2 flyDir;
            if (aimTarget.HasValue)
            {
                Vector2 toTarget = (aimTarget.Value - (Vector2)playerPos).normalized;
                float burstSpread = 45f;
                float t = burstCount > 1 ? (float)i / (burstCount - 1) : 0.5f;
                float angleOffset = Mathf.Lerp(-burstSpread * 0.5f, burstSpread * 0.5f, t);
                flyDir = Quaternion.Euler(0f, 0f, angleOffset) * toTarget;
            }
            else
            {
                flyDir = orbitSpawnDir;
            }

            GameObject bullet = GameObjectPool.Get(burstBulletPrefab, pos, Quaternion.identity);
            if (bullet == null)
                bullet = Object.Instantiate(burstBulletPrefab, pos, Quaternion.identity);

            // 对象池旧实例可能缺 HomingOverride，运行时补上
            if (_needsHoming && bullet.GetComponent<HomingOverride>() == null)
                bullet.AddComponent<HomingOverride>();

            var bb = bullet.GetComponent<AutoProjectileBurstBullet>();
            if (bb != null)
            {
                float finalDmg = GetFinalDamage(damage, out bool isCrit, out bool isPenetration);
                var ps = GetPlayerSkills();
                int pierce = ps != null ? ps.pierceCount : 0;
                float pRate = ps != null ? ps.pierceRate : 0f;
                bb.LaunchBurst(flyDir, burstLaunchSpeed, finalDmg, 5f, Id,
                    pierce, isCrit, pRate, isPenetration,
                    burstOrbitDuration, burstOrbitRadius, startAngle, _ctx.player,
                    aimTarget.HasValue, aimTarget.GetValueOrDefault());

                if (burstFullScreenPenetration)
                {
                    bb.FullScreenPenetration = true;
                    bb.lifetime = 10f; // 安全兜底（直线飞行 1~3s 必出屏）
                }
            }
            else
            {
                // Debug.LogWarning($"[SkillAutoProjectile {Id}] burstBulletPrefab 上未挂 AutoProjectileBurstBullet! prefab={burstBulletPrefab.name}");
            }

            SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);
        }

        // Debug.Log($"[SkillAutoProjectile] 爆发射击：burstCount={burstCount}, burstEnabled={burstEnabled}, IsBurstReady={IsBurstReady}, level={Level}, cd={burstCooldown}");
        PublishSkillCast(playerPos);
    }
}
