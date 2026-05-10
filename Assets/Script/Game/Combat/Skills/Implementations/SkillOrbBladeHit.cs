using UnityEngine;

/// <summary>
/// 环绕刀片命中：挂在刀片子物体上，对进入 Trigger 的敌人周期性造成伤害
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SkillOrbBladeHit : MonoBehaviour
{
    public float damagePerTick = 1f;
    public float tickInterval = 0.15f;

    private float _nextTick;

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

        eb.TakeDamage(Mathf.Max(0.01f, damagePerTick));
    }
}
