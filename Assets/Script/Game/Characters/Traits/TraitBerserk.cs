using UnityEngine;

/// <summary>
/// 低血增伤 + 粒子 VFX。参数：[hpThreshold, attackBonus, moveSpeedBonus]
/// </summary>
public sealed class TraitBerserk : TraitBehaviour
{
    private float _hpThreshold = 0.5f;
    private float _attackBonus = 0.3f;
    private float _moveSpeedBonus = 0.15f;
    private float _minActiveSec = 3f;

    private PlayerHealth _health;
    private PlayerSkills _skills;
    private PlayerController _controller;
    private float _baseAttackMul;
    private float _baseMoveSpeed;
    private bool _active;
    private float _deactivateTimer = -1f;
    private GameObject _vfxGo;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 3) { _hpThreshold = p[0]; _attackBonus = p[1]; _moveSpeedBonus = p[2]; }
    }

    private void Start()
    {
        _health = GetComponent<PlayerHealth>();
        _skills = GetComponent<PlayerSkills>();
        _controller = GetComponent<PlayerController>();
        if (_skills != null) _baseAttackMul = _skills.attackMultiplier;
        if (_controller != null) _baseMoveSpeed = _controller.moveSpeed;

        var prefab = Resources.Load<GameObject>("VFX/TraitBerserk");
        if (prefab != null) { _vfxGo = Instantiate(prefab, transform); _vfxGo.transform.localPosition = Vector3.zero; _vfxGo.transform.localScale = Vector3.one * 0.3f; _vfxGo.SetActive(false); }
    }

    private void Update()
    {
        if (_health == null) return;
        bool lowHp = (float)_health.Hp / Mathf.Max(1, _health.MaxHp) < _hpThreshold;
        bool shouldActivate = lowHp || (_active && _deactivateTimer > 0f);

        if (shouldActivate && !_active)
        {
            _active = true;
            _deactivateTimer = _minActiveSec;
            ApplyBerserk(true);
        }
        else if (!shouldActivate && _active)
        {
            _deactivateTimer -= Time.deltaTime;
            if (_deactivateTimer <= 0f) { _active = false; _deactivateTimer = -1f; ApplyBerserk(false); }
        }
        else if (_active && _deactivateTimer > 0f)
        {
            _deactivateTimer -= Time.deltaTime;
        }
    }

    private void ApplyBerserk(bool on)
    {
        if (_skills != null) _skills.attackMultiplier = _baseAttackMul * (1f + (on ? _attackBonus : 0f));
        if (_controller != null) _controller.moveSpeed = _baseMoveSpeed * (1f + (on ? _moveSpeedBonus : 0f));
        if (_vfxGo != null) _vfxGo.SetActive(on);
    }
}
