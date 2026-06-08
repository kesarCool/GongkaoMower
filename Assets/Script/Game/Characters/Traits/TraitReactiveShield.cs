using System.Collections;
using UnityEngine;

/// <summary>
/// 反应护盾：cd 秒生成 1 层，最多 max 层，受击时消耗 1 层抵消伤害 + 击退。
/// 参数：[maxShields, cooldownSec, knockbackRadius, knockbackForce]
/// </summary>
public sealed class TraitReactiveShield : TraitBehaviour
{
    private int _maxShields = 2;
    private float _cooldown = 20f;
    private float _knockbackRadius = 3f;
    private float _knockbackForce = 5f;

    private int _currentShields;
    private float _timer;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 4) { _maxShields = Mathf.RoundToInt(p[0]); _cooldown = p[1]; _knockbackRadius = p[2]; _knockbackForce = p[3]; }
    }

    private void Start()
    {
        _currentShields = _maxShields;
        var health = GetComponent<PlayerHealth>();
        if (health != null) health.OnPreDamage += OnPreDamage;
    }

    private void Update()
    {
        if (_currentShields >= _maxShields) return;
        _timer += Time.deltaTime;
        if (_timer >= _cooldown)
        {
            _timer = 0f;
            _currentShields = Mathf.Min(_currentShields + 1, _maxShields);
        }
    }

    private void OnDestroy()
    {
        var health = GetComponent<PlayerHealth>();
        if (health != null) health.OnPreDamage -= OnPreDamage;
    }

    private bool OnPreDamage(float damage)
    {
        if (_currentShields <= 0) return false; // 不拦截

        _currentShields--;

        // 击退周围敌人
        var hits = Physics2D.OverlapCircleAll(transform.position, _knockbackRadius, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            var rb = hit.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 dir = (hit.transform.position - transform.position).normalized;
                rb.AddForce(dir * _knockbackForce, ForceMode2D.Impulse);
            }
        }

        return true; // 拦截此次伤害
    }
}
