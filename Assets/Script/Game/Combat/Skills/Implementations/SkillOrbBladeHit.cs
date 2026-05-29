using UnityEngine;

/// <summary>
/// 环绕刀片命中：挂在刀片子物体上，对进入 Trigger 的敌人周期性造成伤害
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SkillOrbBladeHit : MonoBehaviour, IPoolReceiver
{
    public float damagePerTick = 1f;
    public float tickInterval = 0.15f;
    public SkillId damageSourceSkillId = SkillId.OrbitingBlades;

    private float _nextTick;
    private PlayerSkills _playerSkills;

    public void SetPlayerSkills(PlayerSkills ps)
    {
        _playerSkills = ps;
    }

    public void OnPoolGet() => _nextTick = 0f;

    public void OnPoolRelease() { }

    private void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        EnemyBase eb = other.GetComponent<EnemyBase>();
        if (eb == null) eb = other.GetComponentInParent<EnemyBase>();
        if (eb == null) return;

        if (Time.time < _nextTick) return;
        _nextTick = Time.time + Mathf.Max(0.05f, tickInterval);

        float dmg = Mathf.Max(0.01f, damagePerTick);
        bool isCrit = false;
        if (_playerSkills != null)
        {
            dmg *= _playerSkills.attackMultiplier;
            dmg *= _playerSkills.EvaluateCrit(out isCrit);
        }
        eb.TakeDamage(dmg, damageSourceSkillId, isCrit);
    }
}
