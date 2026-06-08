using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rare 符箓环绕：击杀→符咒飞向玩家→环绕旋转→碰敌造成伤害 + 抵消弹幕。
/// </summary>
public sealed class TraitTalismanOrbit : TraitBehaviour
{
    [Header("配置")]
    public int maxTalismans = 5;
    public float orbitRadius = 1.5f;
    public float orbitSpeed = 240f;
    public float flySpeed = 8f;
    [Tooltip("碰敌伤害系数（× PlayerSkills.attackMultiplier）")]
    public float contactDamageMul = 0.3f;

    private const string PrefabPath = "Traits/TalismanOrbit"; // Resources 加载路径

    private GameObject _talismanPrefab;
    private readonly List<TalismanOrbiter> _orbiters = new List<TalismanOrbiter>();
    private PlayerHealth _health;
    private float _angleOffset;

    private void Start()
    {
        _talismanPrefab = Resources.Load<GameObject>(PrefabPath);
        if (_talismanPrefab == null) Debug.LogWarning($"[TraitTalismanOrbit] 未找到预制体：Resources/{PrefabPath}");

        _health = GetComponent<PlayerHealth>();
        if (_health != null) _health.OnPreDamage += OnPreDamage;
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
        Debug.Log($"[TraitTalismanOrbit] 已启动，prefab={_talismanPrefab?.name}, maxTalismans={maxTalismans}");
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
        if (_orbiters.Count >= maxTalismans) return;
        if (_talismanPrefab == null) return;

        var go = Instantiate(_talismanPrefab, e.position, Quaternion.identity);
        go.layer = gameObject.layer;

        var orb = go.AddComponent<TalismanOrbiter>();
        orb.Init(this, flySpeed, orbitRadius);
        _orbiters.Add(orb);
     //   Debug.Log($"[TraitTalismanOrbit] 符咒生成：count={_orbiters.Count}/{maxTalismans}, pos={e.position}");
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 center = transform.position;

        // 逆序遍历方便移除
        for (int i = _orbiters.Count - 1; i >= 0; i--)
        {
            var o = _orbiters[i];
            if (o == null) { _orbiters.RemoveAt(i); continue; }

            if (o.State == TalismanOrbiter.OrbitState.Flying)
            {
                o.FlyToward(center, dt);
            }
            else
            {
                float angle = _angleOffset + 360f / _orbiters.Count * i;
                o.OrbitAround(center, orbitRadius, angle);
            }

            // 碰敌检测
            if (o.CheckEnemyContact())
            {
                _orbiters.RemoveAt(i);
                continue;
            }
        }

        _angleOffset += orbitSpeed * dt;
        if (_angleOffset > 360f) _angleOffset -= 360f;
    }

    private bool OnPreDamage(float damage)
    {
        // 消耗最老的一张符抵消伤害
        for (int i = _orbiters.Count - 1; i >= 0; i--)
        {
            if (_orbiters[i] != null && _orbiters[i].State == TalismanOrbiter.OrbitState.Orbiting)
            {
                Destroy(_orbiters[i].gameObject);
                _orbiters.RemoveAt(i);
                return true; // 抵消
            }
        }
        return false;
    }

    public void RemoveOrbiter(TalismanOrbiter orb)
    {
        _orbiters.Remove(orb);
    }
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
        Vector3 dir = (target - transform.position);
        float dist = dir.magnitude;
        dir /= dist;

        transform.position += dir * _speed * dt;
        FaceDirection(dir); // 飞行时朝向玩家

        if (dist <= _orbitRadius)
            State = OrbitState.Orbiting;
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
