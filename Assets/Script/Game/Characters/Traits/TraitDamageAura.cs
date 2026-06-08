using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 伤害光环：半径内敌人周期性受伤害 + 角色周身脉冲闪白。参数：[radius, damagePerSecMul, tickIntervalSec]
/// </summary>
public sealed class TraitDamageAura : TraitBehaviour
{
    private float _radius = 5f;
    private float _damageMul = 0.5f;
    private float _tickInterval = 0.5f;

    private float _timer;
    private PlayerSkills _skills;
    private SpriteRenderer _sr;
    private readonly List<Transform> _targets = new List<Transform>(32);

    // 脉冲闪白
    private static readonly Color PulseColor = new Color(0.75f, 0.8f, 1f); // 淡蓝灰
    private float _pulseFade;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 3) { _radius = p[0]; _damageMul = p[1]; _tickInterval = p[2]; }
    }

    private void Start()
    {
        _skills = GetComponent<PlayerSkills>();
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        // 脉冲渐退
        if (_pulseFade > 0f)
        {
            _pulseFade -= Time.deltaTime / Mathf.Max(0.05f, _tickInterval);
            if (_sr != null)
                _sr.color = Color.Lerp(Color.white, PulseColor, _pulseFade);
        }

        _timer += Time.deltaTime;
        if (_timer < _tickInterval) return;
        _timer = 0f;

        if (_skills == null) return;
        float dmg = _skills.attackMultiplier * _damageMul;

        _targets.Clear();
        CombatTargetRegistry.CollectTargets("monster", transform.position, _radius, _targets);

        if (_targets.Count > 0)
        {
            // 剑气脉冲：角色闪白蓝
            _pulseFade = 1f;
            if (_sr != null) _sr.color = PulseColor;

            foreach (var t in _targets)
            {
                if (t == null) continue;
                var eb = t.GetComponent<EnemyBase>();
                if (eb != null) eb.TakeDamage(dmg);
            }
        }
    }
}
