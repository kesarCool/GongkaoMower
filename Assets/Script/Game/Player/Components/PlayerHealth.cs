using UnityEngine;

/// <summary>
/// 主角血量：被敌人碰撞伤害；归零发布 <see cref="PlayerDiedEvent"/>。
/// </summary>
[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float hp = 100f;

    private bool _dead;

    public float Hp => hp;
    public float MaxHp => maxHp;
    public bool IsAlive => !_dead && hp > 0f;

    private void Awake()
    {
        hp = Mathf.Max(1f, maxHp);
    }

    public void ResetToFull()
    {
        _dead = false;
        hp = Mathf.Max(1f, maxHp);
    }

    public void TakeDamage(float amount)
    {
        if (_dead) return;
        if (amount <= 0f) return;

        hp -= amount;
        if (hp > 0f) return;

        hp = 0f;
        _dead = true;
        EventBus.Publish(new PlayerDiedEvent { playerHealth = this });
    }
}
