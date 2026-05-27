using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 主角血量：被敌人碰撞伤害；归零发布 <see cref="PlayerDiedEvent"/>。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [Serializable]
    public class DamagedEvent : UnityEvent<float, Transform> { }

    [Serializable]
    public class HealthChangedEvent : UnityEvent<float, float> { }

    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float hp = 100f;

    [Tooltip("受击后无敌时间（秒），0 表示不启用")]
    [SerializeField] private float invulnerabilityDuration = 0.35f;

    private bool _dead;
    private float _invulnerableUntil;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public bool IsAlive => !_dead && hp > 0f;

    public HealthChangedEvent OnHealthChanged = new HealthChangedEvent();
    public DamagedEvent OnDamaged = new DamagedEvent();

    private void Awake()
    {
        hp = Mathf.Max(1f, maxHp);
    }

    /// <summary>外部控制无敌（如 Boss 击杀后延迟结算期间防止玩家意外死亡）。</summary>
    public void SetInvulnerable(bool inv)
    {
        if (inv)
            _invulnerableUntil = float.MaxValue;
        else
            _invulnerableUntil = 0f;
    }

    public void ResetToFull()
    {
        _dead = false;
        _invulnerableUntil = 0f;
        hp = Mathf.Max(1f, maxHp);
        NotifyHealthChanged();
    }

    public void TakeDamage(float amount, Transform damageSource = null)
    {
        if (_dead) return;
        if (amount <= 0f) return;
        if (invulnerabilityDuration > 0f && Time.time < _invulnerableUntil)
            return;

        hp -= amount;
        if (hp < 0f) hp = 0f;

        OnDamaged.Invoke(amount, damageSource);
        EventBus.Publish(new PlayerDamagedEvent
        {
            playerHealth = this,
            damage = amount,
            hpLeft = hp,
            damageSource = damageSource
        });
        NotifyHealthChanged();

        if (hp > 0f)
        {
            if (invulnerabilityDuration > 0f)
                _invulnerableUntil = Time.time + invulnerabilityDuration;
            return;
        }

        _dead = true;
        EventBus.Publish(new PlayerDiedEvent { playerHealth = this });
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged.Invoke(hp, maxHp);
    }
}
