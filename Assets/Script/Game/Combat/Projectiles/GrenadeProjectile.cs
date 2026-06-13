using UnityEngine;

/// <summary>
/// 抛物线手雷：飞行阶段无碰撞，落地对范围内敌人造成一次 AOE 伤害后回池。
/// 爆炸表现由 <see cref="CombatVfxSpawner"/> 单独池化，不与本体同生同死。
/// </summary>
[DisallowMultipleComponent]
public class GrenadeProjectile : MonoBehaviour, IPoolReceiver
{
    public const string SpawnLimiterKey = "Grenade";

    [SerializeField] private float spinSpeedDeg = 540f;
    [SerializeField] private GameObject defaultExplosionFxPrefab;

    protected GameObject _explosionFxPrefab;
    private Vector2 _start;
    private Vector2 _end;
    private float _arcHeight;
    private float _duration;
    private float _elapsed;
    private float _damage;
    private float _aoeRadius;
    protected SkillId _skillId;
    protected string _enemyTag;
    protected bool _exploded;
    protected bool _isCrit;
    protected bool _isPenetration;

    public void Launch(
        Vector2 start,
        Vector2 end,
        float flightDuration,
        float arcHeight,
        float damage,
        float aoeRadius,
        SkillId skillId,
        string enemyTag,
        GameObject explosionFxPrefab,
        bool isCrit = false,
        bool isPenetration = false)
    {
        _start = start;
        _end = end;
        _duration = Mathf.Max(0.05f, flightDuration);
        _arcHeight = arcHeight;
        _damage = Mathf.Max(0.01f, damage);
        _aoeRadius = Mathf.Max(0.1f, aoeRadius);
        _skillId = skillId;
        _enemyTag = enemyTag;
        _explosionFxPrefab = explosionFxPrefab != null ? explosionFxPrefab : defaultExplosionFxPrefab;
        _isCrit = isCrit;
        _isPenetration = isPenetration;
        _elapsed = 0f;
        _exploded = false;

        transform.position = _start;
    }

    protected virtual void Update()
    {
        if (_exploded) return;

        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration;

        if (t >= 1f)
        {
            transform.position = _end;
            Explode();
            return;
        }

        transform.position = ArcMotor2D.Evaluate(_start, _end, _arcHeight, t);

        if (Mathf.Abs(spinSpeedDeg) > 0.01f)
            transform.Rotate(0f, 0f, spinSpeedDeg * Time.deltaTime, Space.Self);
    }

    protected virtual void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        Vector2 center = transform.position;
        ApplyAoeDamage(center);
        var fxPos = new Vector3(center.x, center.y, -0.1f);
        CombatVfxSpawner.TryPlayPooled(_explosionFxPrefab, fxPos, Quaternion.identity);

        SpawnLimiter.Instance?.Unregister(SpawnLimiterKey, gameObject);
        GameObjectPool.Release(gameObject);

        if (_skillId != SkillId.None)
        {
            EventBus.Publish(new SkillCastEvent
            {
                skillId = _skillId,
                worldPosition = fxPos
            });
        }
    }

    protected virtual void ApplyAoeDamage(Vector2 center)
    {
        if (string.IsNullOrEmpty(_enemyTag))
            return;

        bool prevQueries = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, _aoeRadius);
        var damaged = new System.Collections.Generic.HashSet<int>(8);
        float radiusSq = _aoeRadius * _aoeRadius;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            EnemyBase eb = col.GetComponent<EnemyBase>();
            if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
            if (eb == null || !eb.gameObject.CompareTag(_enemyTag)) continue;

            int id = eb.GetInstanceID();
            if (!damaged.Add(id)) continue;

            if (((Vector2)eb.transform.position - center).sqrMagnitude > radiusSq)
                continue;

            eb.TakeDamage(_damage, _skillId, _isCrit, _isPenetration);
        }

        Physics2D.queriesHitTriggers = prevQueries;
    }

    public void OnPoolGet()
    {
        _exploded = false;
        _elapsed = 0f;
    }

    public virtual void OnPoolRelease()
    {
        _exploded = true;
        SpawnLimiter.Instance?.Unregister(SpawnLimiterKey, gameObject);
    }
}
