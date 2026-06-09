using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 符箓环绕 + 半血开启。HP<50% 激活，≥50% 延迟 3s 关闭。
/// </summary>
public sealed class TraitTalismanOrbit : TraitBehaviour
{
    [Header("配置")]
    public int maxTalismans = 5;
    public float orbitRadius = 1.5f;
    public float orbitSpeed = 240f;
    public float flySpeed = 8f;
    public float contactDamageMul = 0.3f;
    public float hpThreshold = 0.5f;
    public float minActiveSec = 3f;

    private const string PrefabPath = "Traits/TalismanOrbit";

    private GameObject _talismanPrefab;
    private readonly List<TalismanOrbiter> _orbiters = new List<TalismanOrbiter>();
    private PlayerHealth _health;
    private float _angleOffset;
    private bool _active;
    private float _deactivateTimer = -1f;

    private void Start()
    {
        _talismanPrefab = Resources.Load<GameObject>(PrefabPath);
        _health = GetComponent<PlayerHealth>();
        if (_health != null) _health.OnPreDamage += OnPreDamage;
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnPreDamage -= OnPreDamage;
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        foreach (var o in _orbiters) if (o != null) Destroy(o.gameObject);
        _orbiters.Clear();
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (!_active) return;
        if (_orbiters.Count >= maxTalismans) return;
        if (_talismanPrefab == null) return;

        var go = Instantiate(_talismanPrefab, e.position, Quaternion.identity);
        go.layer = gameObject.layer;
        var orb = go.AddComponent<TalismanOrbiter>();
        orb.Init(this, flySpeed, orbitRadius);
        _orbiters.Add(orb);
    }

    private void Update()
    {
        if (_health == null) return;

        bool lowHp = (float)_health.Hp / Mathf.Max(1, _health.MaxHp) < hpThreshold;
        bool shouldActivate = lowHp || (_active && _deactivateTimer > 0f);

        if (shouldActivate && !_active)
        {
            _active = true;
            _deactivateTimer = minActiveSec;
        }
        else if (!shouldActivate && _active)
        {
            _deactivateTimer -= Time.deltaTime;
            if (_deactivateTimer <= 0f)
            {
                _active = false;
                _deactivateTimer = -1f;
                foreach (var o in _orbiters) if (o != null) Destroy(o.gameObject);
                _orbiters.Clear();
                return;
            }
        }
        else if (_active && _deactivateTimer > 0f)
        {
            _deactivateTimer -= Time.deltaTime;
        }

        if (!_active) return;

        float dt = Time.deltaTime;
        Vector3 center = transform.position;

        for (int i = _orbiters.Count - 1; i >= 0; i--)
        {
            var o = _orbiters[i];
            if (o == null) { _orbiters.RemoveAt(i); continue; }

            if (o.State == TalismanOrbiter.OrbitState.Flying)
                o.FlyToward(center, dt);
            else
            {
                float angle = _angleOffset + 360f / _orbiters.Count * i;
                o.OrbitAround(center, orbitRadius, angle);
            }

            if (o.CheckEnemyContact()) { _orbiters.RemoveAt(i); continue; }
        }

        _angleOffset += orbitSpeed * dt;
        if (_angleOffset > 360f) _angleOffset -= 360f;
    }

    private bool OnPreDamage(float damage)
    {
        for (int i = _orbiters.Count - 1; i >= 0; i--)
        {
            if (_orbiters[i] != null && _orbiters[i].State == TalismanOrbiter.OrbitState.Orbiting)
            {
                Destroy(_orbiters[i].gameObject);
                _orbiters.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public void RemoveOrbiter(TalismanOrbiter orb) { _orbiters.Remove(orb); }
}

/// <summary>单个符咒：飞行→环绕→碰敌销毁。</summary>
public sealed class TalismanOrbiter : MonoBehaviour
{
    public enum OrbitState { Flying, Orbiting }
    public OrbitState State { get; private set; } = OrbitState.Flying;

    private TraitTalismanOrbit _owner;
    private float _speed;
    private float _orbitRadius;

    public void Init(TraitTalismanOrbit owner, float speed, float orbitRadius)
    {
        _owner = owner;
        _speed = speed;
        _orbitRadius = orbitRadius;
    }

    public void FlyToward(Vector3 target, float dt)
    {
        Vector3 dir = target - transform.position;
        float dist = dir.magnitude;
        if (dist <= _orbitRadius) { State = OrbitState.Orbiting; return; }
        dir /= dist;
        transform.position += dir * _speed * dt;
        FaceDirection(dir);
    }

    private void FaceDirection(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void OrbitAround(Vector3 center, float radius, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        transform.position = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        FaceOutward(center);
    }

    public bool CheckEnemyContact()
    {
        if (State != OrbitState.Orbiting) return false;
        var hits = Physics2D.OverlapCircleAll(transform.position, 0.6f, LayerMask.GetMask("Default"));
        foreach (var h in hits)
        {
            if (!h.CompareTag("monster")) continue;
            var eb = h.GetComponent<EnemyBase>();
            if (eb != null)
            {
                var ps = _owner.GetComponent<PlayerSkills>();
                float dmg = ps != null ? ps.attackMultiplier * _owner.contactDamageMul : 10f;
                eb.TakeDamage(dmg);
            }
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    public void FaceOutward(Vector3 center)
    {
        Vector3 dir = (transform.position - center).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDestroy()
    {
        _owner?.RemoveOrbiter(this);
    }
}
