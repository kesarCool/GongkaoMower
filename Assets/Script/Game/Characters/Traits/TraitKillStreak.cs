using UnityEngine;

/// <summary>
/// 击杀叠攻：每 killsPerStack 次击杀 +attack% +moveSpeed%，最多 maxStacks 层，持续 durationSec。
/// 参数：[killsPerStack, maxStacks, attackPerStack, moveSpeedPerStack, durationSec]
/// </summary>
public sealed class TraitKillStreak : TraitBehaviour
{
    private int _killsPerStack = 10;
    private int _maxStacks = 3;
    private float _attackPerStack = 0.15f;
    private float _moveSpeedPerStack = 0.05f;
    private float _duration = 12f;

    private int _killCount;
    private int _stacks;
    private float _expireTimer;
    private PlayerSkills _skills;
    private PlayerController _controller;
    private SpriteRenderer _sr;

    private float _baseAttackMul;
    private float _baseMoveSpeed;

    public override void Initialize(float[] p)
    {
        if (p != null && p.Length >= 5) { _killsPerStack = Mathf.RoundToInt(p[0]); _maxStacks = Mathf.RoundToInt(p[1]); _attackPerStack = p[2]; _moveSpeedPerStack = p[3]; _duration = p[4]; }
    }

    private void Start()
    {
        _skills = GetComponent<PlayerSkills>();
        _controller = GetComponent<PlayerController>();
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_skills != null) _baseAttackMul = _skills.attackMultiplier;
        if (_controller != null) _baseMoveSpeed = _controller.moveSpeed;
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        // 恢复属性
        if (_skills != null) _skills.attackMultiplier = _baseAttackMul;
        if (_controller != null) _controller.moveSpeed = _baseMoveSpeed;
    }

    private void OnEnemyDied(EnemyDiedEvent _)
    {
        _killCount++;
        if (_killCount >= _killsPerStack)
        {
            _killCount = 0;
            _stacks = Mathf.Min(_stacks + 1, _maxStacks);
            _expireTimer = _duration;
            ApplyStacks();
        }
    }

    private void Update()
    {
        if (_stacks <= 0) return;
        _expireTimer -= Time.deltaTime;
        if (_expireTimer <= 0f)
        {
            _stacks = 0;
            ApplyStacks();
        }
    }

    private void ApplyStacks()
    {
        if (_skills != null) _skills.attackMultiplier = _baseAttackMul * (1f + _stacks * _attackPerStack);
        if (_controller != null) _controller.moveSpeed = _baseMoveSpeed * (1f + _stacks * _moveSpeedPerStack);

        // 低特效：Sprite 红色渐变
        if (_sr != null)
        {
            float t = (float)_stacks / _maxStacks;
            _sr.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.5f), t);
        }
    }
}
