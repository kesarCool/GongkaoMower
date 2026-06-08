using UnityEngine;

/// <summary>
/// 低血增伤：血量低于 hpThreshold 时 +attack% +moveSpeed%。
/// 参数：[hpThreshold, attackBonus, moveSpeedBonus]
/// </summary>
public sealed class TraitBerserk : TraitBehaviour
{
    private float _hpThreshold = 0.5f;
    private float _attackBonus = 0.3f;
    private float _moveSpeedBonus = 0.15f;

    private PlayerHealth _health;
    private PlayerSkills _skills;
    private PlayerController _controller;
    private SpriteRenderer _sr;
    private float _baseAttackMul;
    private float _baseMoveSpeed;
    private bool _active;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 3) { _hpThreshold = p[0]; _attackBonus = p[1]; _moveSpeedBonus = p[2]; }
    }

    private void Start()
    {
        _health = GetComponent<PlayerHealth>();
        _skills = GetComponent<PlayerSkills>();
        _controller = GetComponent<PlayerController>();
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_skills != null) _baseAttackMul = _skills.attackMultiplier;
        if (_controller != null) _baseMoveSpeed = _controller.moveSpeed;
    }

    private void Update()
    {
        if (_health == null) return;
        bool low = (float)_health.Hp / Mathf.Max(1, _health.MaxHp) < _hpThreshold;

        if (low == _active) return;
        _active = low;

        if (_skills != null) _skills.attackMultiplier = _baseAttackMul * (1f + (_active ? _attackBonus : 0f));
        if (_controller != null) _controller.moveSpeed = _baseMoveSpeed * (1f + (_active ? _moveSpeedBonus : 0f));
        if (_sr != null) _sr.color = _active ? new Color(1f, 0.4f, 0.3f) : Color.white;
    }
}
