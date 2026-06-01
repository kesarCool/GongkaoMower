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

    // maxHp/hp/defense 由 CharacterConfigApplier 写入，不再暴露在 Inspector 上
    private float maxHp = 100f;
    private float hp = 100f;
    private float defense = 0f;

    [Tooltip("受击后无敌时间（秒），0 表示不启用")]
    [SerializeField] private float invulnerabilityDuration = 0.35f;

    private bool _dead;
    private float _invulnerableUntil;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public float Defense => defense;
    public bool IsAlive => !_dead && hp > 0f;

    public HealthChangedEvent OnHealthChanged = new HealthChangedEvent();
    public DamagedEvent OnDamaged = new DamagedEvent();

    private void Awake()
    {
        hp = Mathf.Max(1f, maxHp);
    }

    /// <summary>由 CharacterConfigApplier 写入角色属性。</summary>
    public void SetMaxHp(float value)
    {
        maxHp = value;
    }

    /// <summary>
    /// 保持当前血量比值不变的前提下修改 maxHp（被动技能加成/移除时使用）。
    /// </summary>
    public void SetMaxHpKeepRatio(float value)
    {
        float ratio = maxHp > 0f ? hp / maxHp : 1f;
        maxHp = value;
        hp = Mathf.Max(1f, value * ratio);
        NotifyHealthChanged();
    }

    /// <summary>由 CharacterConfigApplier 写入角色属性。</summary>
    public void SetDefense(float value)
    {
        defense = value;
    }

    /// <summary>回复血量（不超过上限）。</summary>
    public void Heal(float amount)
    {
        if (_dead || amount <= 0f) return;
        hp = Mathf.Min(maxHp, hp + amount);
        NotifyHealthChanged();
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

        float finalDmg = defense > 0f
            ? amount * (100f / (100f + defense))
            : amount;

        hp -= finalDmg;
        if (hp < 0f) hp = 0f;

        OnDamaged.Invoke(finalDmg, damageSource);
        EventBus.Publish(new PlayerDamagedEvent
        {
            playerHealth = this,
            damage = finalDmg,
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
