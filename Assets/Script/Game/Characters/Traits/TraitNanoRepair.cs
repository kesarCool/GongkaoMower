using UnityEngine;

/// <summary>
/// 纳米修复：HP<50% 持续回复 + 粒子 VFX。参数：[healPercentPerSec, tickIntervalSec]
/// </summary>
public sealed class TraitNanoRepair : TraitBehaviour
{
    private float _healPercentPerSec = 0.02f; // 每秒回 2% 最大血量
    private float _tickInterval = 0.25f;
    private float _hpThreshold = 0.5f;
    private float _minActiveSec = 3f;

    private PlayerHealth _health;
    private GameObject _vfxGo;
    private float _timer;
    private bool _active;
    private float _deactivateTimer = -1f;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 2) { _healPercentPerSec = p[0]; _tickInterval = p[1]; }
    }

    private void Start()
    {
        _health = GetComponent<PlayerHealth>();
        var prefab = Resources.Load<GameObject>("VFX/TraitNanoRepair");
        if (prefab != null) { _vfxGo = Instantiate(prefab, transform); _vfxGo.transform.localPosition = Vector3.zero; _vfxGo.transform.localScale = Vector3.one; _vfxGo.SetActive(false); }
        Debug.Log($"[NanoRepair] 已启动：healRate={_healPercentPerSec * 100f:F1}%/s, hpThreshold={_hpThreshold}, prefab={prefab?.name}");
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
            if (_vfxGo != null) _vfxGo.SetActive(true);
        }
        else if (!shouldActivate && _active)
        {
            _deactivateTimer -= Time.deltaTime;
            if (_deactivateTimer <= 0f) { _active = false; _deactivateTimer = -1f; if (_vfxGo != null) _vfxGo.SetActive(false); }
        }
        else if (_active && _deactivateTimer > 0f)
        {
            _deactivateTimer -= Time.deltaTime;
        }

        if (!_active) return;

        _timer += Time.deltaTime;
        if (_timer < _tickInterval) return;
        _timer = 0f;

        int heal = Mathf.RoundToInt(_health.MaxHp * _healPercentPerSec * _tickInterval);
        if (heal > 0) { _health.Heal(heal); Debug.Log($"[NanoRepair] 回血 +{heal}, hp={_health.Hp}/{_health.MaxHp}"); }
    }
}
