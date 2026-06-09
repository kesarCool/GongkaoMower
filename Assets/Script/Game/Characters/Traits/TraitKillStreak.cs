using UnityEngine;

/// <summary>
/// 击杀叠攻：12s 窗口内击杀叠层，+攻击 +攻速。窗口结束进入 10s CD。
/// 参数：[killsPerStack, maxStacks, attackPerStack, attackSpeedPerStack, windowSec, cooldownSec]
/// </summary>
public sealed class TraitKillStreak : TraitBehaviour
{
    private int _killsPerStack = 8;
    private int _maxStacks = 3;
    private float _attackPerStack = 0.15f;
    private float _attackSpeedPerStack = 0.08f;
    private float _windowSec = 12f;
    private float _cooldownSec = 10f;

    private int _killCount;
    private int _stacks;
    private float _timer;       // 窗口计时（正=窗口内，负=冷却倒计时）
    private bool _inCooldown;

    private PlayerSkills _skills;
    private SkillAutoProjectile _autoSkill;
    private float _baseAttackMul;
    private float _baseInterval;
    private ParticleSystem _vfx;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 6)
        {
            _killsPerStack = Mathf.RoundToInt(p[0]);
            _maxStacks = Mathf.RoundToInt(p[1]);
            _attackPerStack = p[2];
            _attackSpeedPerStack = p[3];
            _windowSec = p[4];
            _cooldownSec = p[5];
        }
    }

    private void Start()
    {
        _skills = GetComponent<PlayerSkills>();
        if (_skills != null)
        {
            _baseAttackMul = _skills.attackMultiplier;
            // 找 AutoProjectile 系列技能取其 interval
            var ids = new System.Collections.Generic.List<SkillId>(4);
            _skills.GetEquippedSkillIdsOrdered(ids);
            foreach (var id in ids)
            {
                var sk = _skills.GetEquippedSkill<SkillAutoProjectile>(id);
                if (sk != null) { _autoSkill = sk; _baseInterval = sk.interval; break; }
            }
        }

        var vfxPrefab = Resources.Load<GameObject>("VFX/TraitKillStreak");
        if (vfxPrefab != null)
        {
            var go = Instantiate(vfxPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * 0.3f;
            _vfx = go.GetComponentInChildren<ParticleSystem>();
            if (_vfx != null) _vfx.Stop();
        }

        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        if (_skills != null) _skills.attackMultiplier = _baseAttackMul;
        if (_autoSkill != null) _autoSkill.interval = _baseInterval;
    }

    private void OnEnemyDied(EnemyDiedEvent _)
    {
        if (_inCooldown) return;

        // 首次击杀开始窗口
        if (_timer <= 0f && _stacks == 0)
        {
            _timer = _windowSec;
            _killCount = 0;
        }

        _killCount++;
        if (_killCount >= _killsPerStack && _stacks < _maxStacks)
        {
            _killCount = 0;
            _stacks++;
            ApplyStacks();
        }
    }

    private void Update()
    {
        if (_stacks == 0 && !_inCooldown) return;

        if (_inCooldown)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) { _inCooldown = false; _timer = 0f; _killCount = 0; }
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            // 窗口结束 → 清层 + 进 CD
            _stacks = 0;
            _killCount = 0;
            _inCooldown = true;
            _timer = _cooldownSec;
            ApplyStacks();
        }
    }

    private void ApplyStacks()
    {
        if (_skills != null)
        {
            _skills.attackMultiplier = _baseAttackMul * (1f + _stacks * _attackPerStack);
            _autoSkill.interval = Mathf.Max(0.05f, _baseInterval * (1f - _stacks * _attackSpeedPerStack));
        }
        if (_vfx != null)
        {
            if (_stacks > 0 && !_vfx.isPlaying) _vfx.Play();
            else if (_stacks == 0 && _vfx.isPlaying) _vfx.Stop();
        }
    }
}
