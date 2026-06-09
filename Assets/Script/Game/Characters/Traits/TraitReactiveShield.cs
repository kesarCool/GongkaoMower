using System.Collections;
using UnityEngine;

/// <summary>
/// 反应护盾 + 半血开启 + 粒子 VFX。参数：[maxShields, cooldownSec, knockbackRadius, knockbackForce]
/// </summary>
public sealed class TraitReactiveShield : TraitBehaviour
{
    private int _maxShields = 2;
    private float _cooldown = 20f;
    private float _knockbackRadius = 3f;
    private float _knockbackForce = 5f;
    private float _hpThreshold = 0.5f;

    private int _currentShields;
    private float _timer;
    private GameObject _vfxGo;
    private PlayerHealth _health;
    private bool _active;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 4) { _maxShields = Mathf.RoundToInt(p[0]); _cooldown = p[1]; _knockbackRadius = p[2]; _knockbackForce = p[3]; }
    }

    private void Start()
    {
        _health = GetComponent<PlayerHealth>();
        var prefab = Resources.Load<GameObject>("VFX/fx_recover_hp");
        if (prefab != null) { _vfxGo = Instantiate(prefab, transform); _vfxGo.transform.localPosition = Vector3.zero; _vfxGo.transform.localScale = Vector3.one * 0.3f; _vfxGo.SetActive(false); }

        if (_health != null) _health.OnPreDamage += OnPreDamage;
    }

    private void Update()
    {
        if (_health == null) return;

        bool lowHp = (float)_health.Hp / Mathf.Max(1, _health.MaxHp) < _hpThreshold;
        if (lowHp && !_active)
        {
            _active = true;
            _currentShields = _maxShields; // 激活时满盾
            if (_vfxGo != null) _vfxGo.SetActive(true);
        }
        else if (!lowHp && _active)
        {
            _active = false;
            _currentShields = 0;
            if (_vfxGo != null) _vfxGo.SetActive(false);
        }

        if (!_active) return;
        if (_currentShields >= _maxShields) return;
        _timer += Time.deltaTime;
        if (_timer >= _cooldown) { _timer = 0f; _currentShields = Mathf.Min(_currentShields + 1, _maxShields); if (_vfxGo != null) _vfxGo.SetActive(true); }
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnPreDamage -= OnPreDamage;
    }

    private bool OnPreDamage(float damage)
    {
        if (_currentShields <= 0) return false;
        _currentShields--;

        // 盾碎：闪一下 + 清 VFX
        if (_vfxGo != null) StartCoroutine(FlashVfx());
        if (_currentShields <= 0 && _vfxGo != null) _vfxGo.SetActive(false);

        var hits = Physics2D.OverlapCircleAll(transform.position, _knockbackRadius, LayerMask.GetMask("Default"));
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("monster")) continue;
            var rb = hit.GetComponent<Rigidbody2D>();
            if (rb != null) rb.AddForce(((Vector2)(hit.transform.position - transform.position)).normalized * _knockbackForce, ForceMode2D.Impulse);
        }
        return true;
    }

    private IEnumerator FlashVfx()
    {
        _vfxGo.transform.localScale = Vector3.one * 0.5f;
        yield return new WaitForSeconds(0.1f);
        if (_vfxGo != null) _vfxGo.transform.localScale = Vector3.one * 0.3f;
    }
}
