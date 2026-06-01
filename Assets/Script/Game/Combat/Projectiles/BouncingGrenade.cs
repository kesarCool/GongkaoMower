using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弹射雷：抛物线手雷，爆炸后跳向下一个敌人再爆，最多 N 次。
/// </summary>
public class BouncingGrenade : GrenadeProjectile
{
    [Tooltip("弹射最大次数（0=不弹射）")]
    public int maxBounces = 3;
    [Tooltip("弹射搜敌半径")]
    public float bounceSearchRadius = 8f;
    [Tooltip("弹射抛物线高度")]
    public float bounceArcHeight = 2f;
    [Tooltip("弹射飞行时间（秒），0=按距离自动计算")]
    public float bounceFlightTime = 0f;

    private int _bouncesRemaining;
    private Vector2 _lastExplosionPos;
    private float _lastDamage;
    private float _lastAoeRadius;
    private bool _bouncing;
    private readonly HashSet<int> _hitTargets = new HashSet<int>(8);

    public void SetBounceDamage(float damage, float aoeRadius, SkillId skillId)
    {
        _lastDamage = damage;
        _lastAoeRadius = aoeRadius;
        _skillId = skillId;
        _bouncesRemaining = maxBounces;
    }

    public void LaunchBouncing(
        Vector2 start, Vector2 end, float flightDuration, float arcHeight,
        float damage, float aoeRadius, SkillId skillId, string enemyTag,
        GameObject explosionFxPrefab,
        int maxBounces, float bounceRadius, float bounceArc, float bounceFlight)
    {
        Launch(start, end, flightDuration, arcHeight, damage, aoeRadius, skillId, enemyTag, explosionFxPrefab);
        this.maxBounces = maxBounces;
        bounceSearchRadius = Mathf.Max(2f, bounceRadius);
        bounceArcHeight = Mathf.Max(0.5f, bounceArc);
        bounceFlightTime = bounceFlight;
        _bouncesRemaining = maxBounces;
        _hitTargets.Clear();

        _lastDamage = damage;
        _lastAoeRadius = aoeRadius;
    }

    protected override void ApplyAoeDamage(Vector2 center)
    {
        if (string.IsNullOrEmpty(_enemyTag)) return;
        bool prev = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;
        var hits = Physics2D.OverlapCircleAll(center, _lastAoeRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyBase eb = hits[i].GetComponent<EnemyBase>();
            if (eb == null) eb = hits[i].GetComponentInParent<EnemyBase>();
            if (eb == null || !eb.gameObject.CompareTag(_enemyTag)) continue;
            _hitTargets.Add(eb.GetInstanceID());
            eb.TakeDamage(_lastDamage, _skillId, _isCrit);
        }
        Physics2D.queriesHitTriggers = prev;
    }

    protected override void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        Vector2 center = transform.position;
        ApplyAoeDamage(center);
        var fxPos = new Vector3(center.x, center.y, -0.1f);
        CombatVfxSpawner.TryPlayPooled(_explosionFxPrefab, fxPos, Quaternion.identity);

        _lastExplosionPos = center;

        if (_bouncesRemaining > 0)
        {
            _bouncesRemaining--;
            _exploded = false;
            _bouncing = true;
            return;
        }

        FinalRelease();

        if (_skillId != SkillId.None)
        {
            EventBus.Publish(new SkillCastEvent { skillId = _skillId, worldPosition = fxPos });
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!_bouncing) return;
        _bouncing = false;

        // 找最近未被打过的敌人
        Transform nearest = FindNextTarget();
        if (nearest == null)
        {
            FinalRelease();
            return;
        }

        Vector2 start = _lastExplosionPos;
        Vector2 end = nearest.transform.position;
        float dist = Vector2.Distance(start, end);
        float flight = bounceFlightTime > 0.01f ? bounceFlightTime : Mathf.Clamp(dist * 0.15f, 0.5f, 1.2f);
        float arc = bounceArcHeight * Mathf.Max(1f, dist * 0.3f);

        // 使用 ArcMotor2D 飞向下一个目标的抛物线轨迹
        Launch(start, end, flight, arc, _lastDamage, _lastAoeRadius, _skillId, _enemyTag, _explosionFxPrefab, _isCrit);
    }

    private Transform FindNextTarget()
    {
        if (string.IsNullOrEmpty(_enemyTag)) return null;

        float maxSq = bounceSearchRadius * bounceSearchRadius;
        Transform best = null;
        float bestDist = float.MaxValue;

        bool prev = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;
        var hits = Physics2D.OverlapCircleAll(_lastExplosionPos, bounceSearchRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            EnemyBase eb = hits[i].GetComponent<EnemyBase>();
            if (eb == null) eb = hits[i].GetComponentInParent<EnemyBase>();
            if (eb == null || !eb.gameObject.CompareTag(_enemyTag)) continue;

            if (_hitTargets.Contains(eb.GetInstanceID())) continue;

            float d = ((Vector2)eb.transform.position - _lastExplosionPos).sqrMagnitude;
            if (d < maxSq && d < bestDist)
            {
                bestDist = d;
                best = eb.transform;
            }
        }

        Physics2D.queriesHitTriggers = prev;
        return best;
    }

    private void FinalRelease()
    {
        _exploded = true;
        SpawnLimiter.Instance?.Unregister(SpawnLimiterKey, gameObject);
        GameObjectPool.Release(gameObject);
    }

    public override void OnPoolRelease()
    {
        base.OnPoolRelease();
        _hitTargets.Clear();
    }
}
