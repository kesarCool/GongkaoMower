using UnityEngine;

/// <summary>
/// 追踪导弹：每帧向目标转向，命中后圆形 AOE 爆炸。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class HomingMissileBullet : PlayerBullet
{
    [Tooltip("每秒最大转向角度")]
    public float turnRate = 180f;
    [Tooltip("爆炸 AOE 半径")]
    public float explosionRadius = 1.5f;
    [Tooltip("爆炸特效 Prefab")]
    public GameObject explosionFxPrefab;

    private Transform _target;
    private Vector2 _lastTargetPos;
    private bool _hasTarget;
    private string _enemyTag;

    public void Launch(Vector2 direction, float speed, float damage, float lifetime,
        SkillId source, Transform target, float aoeRadius, GameObject fxPrefab,
        string enemyTag, bool isCrit = false, bool isPenetration = false)
    {
        var p = new BulletLaunchParams(speed, damage, lifetime, source, isCrit: isCrit, isPenetration: isPenetration);
        Launch(direction, in p);

        _launched = true;
        targetTag = enemyTag;
        _target = target;
        _hasTarget = target != null;
        _lastTargetPos = target != null ? (Vector2)target.position : direction * 100f;
        explosionRadius = aoeRadius;
        explosionFxPrefab = fxPrefab;
        _enemyTag = enemyTag;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _hasTarget = false;
        _target = null;
        _launched = false;
    }

    private bool _launched;

    protected override void OnFrameMove()
    {
        if (!_launched) return;

        // 目标死亡时重新寻敌
        if (_hasTarget && (_target == null || !_target.gameObject.activeSelf))
        {
            var nearest = CombatTargetRegistry.FindNearest(_enemyTag, transform.position, 10f);
            if (nearest != null)
            {
                _target = nearest.transform;
                _lastTargetPos = _target.position;
            }
            else
            {
                _hasTarget = false;
            }
        }

        // 更新追踪位置
        if (_hasTarget && _target != null)
            _lastTargetPos = _target.position;

        // 转向目标
        Vector2 toTarget = _hasTarget
            ? (_lastTargetPos - (Vector2)transform.position).normalized
            : _dir;

        float maxAngle = turnRate * Time.fixedDeltaTime;
        float currentAngle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxAngle);
        float rad = newAngle * Mathf.Deg2Rad;
        _dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        _rb.velocity = _dir * speed;
        _rb.MoveRotation(newAngle);
    }

    protected override void OnHitEnemy(Collider2D other)
    {
        Explode();
    }

    protected override void Release()
    {
        _fired = false;
        SpawnLimiter.Instance?.Unregister("Missile", gameObject);
        GameObjectPool.Release(gameObject);
    }

    private void Explode()
    {
        Vector2 center = transform.position;
        float radiusSq = explosionRadius * explosionRadius;

        // 爆炸特效
        if (explosionFxPrefab != null)
        {
            var fxPos = new Vector3(center.x, center.y, -0.1f);
            CombatVfxSpawner.TryPlayPooled(explosionFxPrefab, fxPos, Quaternion.identity);
        }

        // AOE 伤害
        if (!string.IsNullOrEmpty(_enemyTag))
        {
            bool prev = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;
            var hits = Physics2D.OverlapCircleAll(center, explosionRadius);
            var damaged = new System.Collections.Generic.HashSet<int>(8);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i];
                if (col == null) continue;
                EnemyBase eb = col.GetComponent<EnemyBase>();
                if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
                if (eb == null || !eb.gameObject.CompareTag(_enemyTag)) continue;
                int id = eb.GetInstanceID();
                if (!damaged.Add(id)) continue;
                if (((Vector2)eb.transform.position - center).sqrMagnitude > radiusSq) continue;

                // AOE 伤害不适用穿透/暴击的 isCrit（导弹爆炸统一判定）
                eb.TakeDamage(damage, skillSource, IsCrit, IsPenetration);
            }
            Physics2D.queriesHitTriggers = prev;
        }

        Release();
    }
}
