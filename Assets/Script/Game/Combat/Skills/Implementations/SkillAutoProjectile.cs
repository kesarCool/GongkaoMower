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

    private float _timer;

    public SkillAutoProjectile(GameObject bulletPrefab, float bulletSpeed, float interval)
    {
        Id = SkillId.AutoProjectile;
        this.bulletPrefab = bulletPrefab;
        this.bulletSpeed = bulletSpeed;
        this.interval = Mathf.Max(0.05f, interval);
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (bulletPrefab == null) return;
        if (_ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

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

            GameObject bullet = GameObjectPool.Get(bulletPrefab, from, Quaternion.identity);
            Bullet b = bullet.GetComponent<Bullet>();
            if (b != null)
            {
                b.ApplySkillShot(SkillId.AutoProjectile, damage);
                b.SetDirection(d, bulletSpeed);
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
}
