using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害光环 + 半血开启。参数：[radius, damagePerSecMul, tickIntervalSec]
/// </summary>
public sealed class TraitDamageAura : TraitBehaviour
{
    private float _radius = 5f;
    private float _damageMul = 0.5f;
    private float _tickInterval = 0.5f;
    private float _hpThreshold = 0.5f;
    private float _minActiveSec = 3f;

    private float _timer;
    private PlayerSkills _skills;
    private PlayerHealth _health;
    private readonly List<Transform> _targets = new List<Transform>(32);
    private GameObject _vfxGo;
    private bool _active;
    private float _deactivateTimer = -1f;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 3) { _radius = p[0]; _damageMul = p[1]; _tickInterval = p[2]; }
    }

    private void Start()
    {
        _skills = GetComponent<PlayerSkills>();
        _health = GetComponent<PlayerHealth>();
        var prefab = Resources.Load<GameObject>("VFX/TraitDamageAura");
        if (prefab != null) { _vfxGo = Instantiate(prefab, transform); _vfxGo.transform.localPosition = Vector3.zero; _vfxGo.transform.localScale = Vector3.one * 0.3f; _vfxGo.SetActive(false); }
    }

    private void Update()
    {
        if (_health == null || _skills == null) return;

        bool lowHp = (float)_health.Hp / Mathf.Max(1, _health.MaxHp) < _hpThreshold;
        bool shouldActivate = lowHp || (_active && _deactivateTimer > 0f);

        if (shouldActivate && !_active)
        {
            _active = true;
            _deactivateTimer = _minActiveSec;
            if (_vfxGo != null) _vfxGo.SetActive(true);
        }
        else if (!shouldActivate && _active)
        {
            _deactivateTimer -= Time.deltaTime;
            if (_deactivateTimer <= 0f) { _active = false; _deactivateTimer = -1f; if (_vfxGo != null) _vfxGo.SetActive(false); }
        }
        else if (_active && _deactivateTimer > 0f)
        {
            _deactivateTimer -= Time.deltaTime;
        }

        if (!_active) return;

        _timer += Time.deltaTime;
        if (_timer < _tickInterval) return;
        _timer = 0f;

        float dmg = _skills.attackMultiplier * _damageMul;
        _targets.Clear();
        CombatTargetRegistry.CollectTargets("monster", transform.position, _radius, _targets);

        if (_targets.Count > 0)
        {
            if (_vfxGo != null) { _vfxGo.transform.localScale = Vector3.one * 0.4f; StartCoroutine(ScaleBack(_vfxGo.transform, 0.15f)); }
            foreach (var t in _targets)
            {
                if (t == null) continue;
                var eb = t.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(dmg);
            }
        }
    }

    private System.Collections.IEnumerator ScaleBack(Transform t, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (t != null) t.localScale = Vector3.one * 0.3f;
    }
}
