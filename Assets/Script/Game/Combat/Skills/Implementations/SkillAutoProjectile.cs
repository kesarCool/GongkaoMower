using UnityEngine;

/// <summary>
/// 自动索敌投射物：周期性向最近敌人方向发射子弹（逻辑等价于原先 PlayerController.AutoShoot）
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

    private float _timer;
    private float _burstTimer;
    private int _burstMaxLevel = 5; // 由 SkillDef.ApplyStatsToSkill 写入

    /// <summary>由 AutoProjectileSkillDefinition.ApplyBurstStats 调用。</summary>
    public void SetBurstMaxLevel(int maxLv) { _burstMaxLevel = maxLv; }

    private bool IsBurstReady => burstEnabled && Level >= _burstMaxLevel;

    public SkillAutoProjectile(GameObject bulletPrefab, float bulletSpeed, float interval, SkillId skillId = SkillId.AutoProjectile)
    {
        Id = skillId;
        this.bulletPrefab = bulletPrefab;
        this.bulletSpeed = bulletSpeed;
        this.interval = Mathf.Max(0.05f, interval);
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        // Burst CD 独立于普攻，没敌人也走
        TryFireBurst(deltaTime);

        if (bulletPrefab == null) return;

        _timer += deltaTime;
        if (_timer < interval) return;

        // 检查上限与节流
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Bullet", out _))
            {
                _timer = interval * 0.5f; // 被限流时提前一点再试
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
            // 检查上限与节流
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
            {
                bullet = Object.Instantiate(bulletPrefab, from, Quaternion.identity);
            }

            PlayerBullet pb = bullet.GetComponent<PlayerBullet>();
            if (pb != null)
            {
                float finalDmg = GetFinalDamage(damage, out bool isCrit);
                var ps = GetPlayerSkills();
                var p = new BulletLaunchParams(bulletSpeed, finalDmg, 5f, Id,
                    ps != null ? ps.pierceCount : 0,
                    isCrit,
                    ps != null ? ps.pierceRate : 0f);
                pb.Launch(d, p);
                // 散射弹需要玩家引用以避开向玩家方向散射
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
            PublishSkillCast(from);
    }

    private void TryFireBurst(float deltaTime)
    {
        if (!IsBurstReady) return;

        if (burstBulletPrefab == null) { Debug.LogWarning($"[SkillAutoProjectile {Id}] burstBulletPrefab is null!"); return; }
        if (_ctx.player == null) { Debug.LogWarning($"[SkillAutoProjectile {Id}] _ctx.player is null!"); return; }

        _burstTimer += deltaTime;
        if (_burstTimer < burstCooldown) return;
        _burstTimer = 0f;

        Vector3 playerPos = _ctx.player.position;

        // 单敌集火：感知范围内仅 1 个敌人时，全部弹丸瞄准该敌
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
            // 环绕位置始终用径向（绕玩家一圈均匀分布）
            Vector2 orbitSpawnDir = Quaternion.Euler(0f, 0f, startAngle) * Vector2.right;
            Vector3 pos = playerPos + (Vector3)(orbitSpawnDir * burstOrbitRadius);

            Vector2 flyDir;
            if (aimTarget.HasValue)
            {
                // 集火：朝向目标，广阔扇形散射
                Vector2 toTarget = (aimTarget.Value - (Vector2)playerPos).normalized;
                float burstSpread = 45f; // 集火散射总角度
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

            var bb = bullet.GetComponent<AutoProjectileBurstBullet>();
            if (bb != null)
            {
                float finalDmg = GetFinalDamage(damage, out bool isCrit);
                var ps = GetPlayerSkills();
                int pierce = ps != null ? ps.pierceCount : 0;
                float pRate = ps != null ? ps.pierceRate : 0f;
                bb.LaunchBurst(flyDir, burstLaunchSpeed, finalDmg, 5f, Id,
                    pierce, isCrit, pRate,
                    burstOrbitDuration, burstOrbitRadius, startAngle, _ctx.player,
                    aimTarget.HasValue, aimTarget.GetValueOrDefault());
            }
            else
            {
                Debug.LogWarning($"[SkillAutoProjectile {Id}] burstBulletPrefab 上未挂 AutoProjectileBurstBullet! prefab={burstBulletPrefab.name}");
            }

            SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);
        }

        PublishSkillCast(playerPos);
    }
}
