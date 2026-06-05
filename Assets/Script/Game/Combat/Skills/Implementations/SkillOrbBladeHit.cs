using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 环绕刀片命中：挂在刀片子物体上。首次接触立即伤害，持续贴脸按 tickInterval 限速，
/// 离开清累积器保证下次切入仍然立即伤害。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SkillOrbBladeHit : MonoBehaviour, IPoolReceiver
{
    public float damagePerTick = 1f;
    public float tickInterval = 0.15f;
    public SkillId damageSourceSkillId = SkillId.OrbitingBlades;

    private PlayerSkills _playerSkills;
    private Action _onDamageDealt;
    private readonly Dictionary<int, float> _accum = new Dictionary<int, float>(8);

    public void SetPlayerSkills(PlayerSkills ps) => _playerSkills = ps;
    public void SetOnDamageDealt(Action callback) => _onDamageDealt = callback;
    public void OnPoolGet() => _accum.Clear();
    public void OnPoolRelease() => _accum.Clear();
    private void OnDisable() => _accum.Clear();

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        EnemyBase eb = other.GetComponent<EnemyBase>();
        if (eb == null) eb = other.GetComponentInParent<EnemyBase>();
        if (eb == null) return;

        int id = eb.GetInstanceID();
        float dt = Time.deltaTime;

        bool isFirstHit = !_accum.ContainsKey(id);
        if (!isFirstHit)
        {
            float acc = _accum[id] + dt;
            if (acc < tickInterval) { _accum[id] = acc; return; }
        }
        _accum[id] = 0f;

        float dmg = Mathf.Max(0.01f, damagePerTick);
        bool isCrit = false;
        if (_playerSkills != null)
        {
            dmg *= _playerSkills.attackMultiplier;
            dmg *= _playerSkills.EvaluateCrit(out isCrit);
        }
        eb.TakeDamage(dmg, damageSourceSkillId, isCrit);
        _onDamageDealt?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        EnemyBase eb = other.GetComponent<EnemyBase>();
        if (eb == null) eb = other.GetComponentInParent<EnemyBase>();
        if (eb == null) return;
        _accum.Remove(eb.GetInstanceID());
    }
}
