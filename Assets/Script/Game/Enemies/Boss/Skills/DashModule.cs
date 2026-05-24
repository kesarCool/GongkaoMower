using System.Collections;
using UnityEngine;

/// <summary>
/// 冲刺技能：蓄力 → 直线高速冲锋 → 收招。
/// 参数格式：elementNum = "6,15,8,30" (冷却6s, 速度15f, 距离8f, 伤害30)
///   [0]=cooldown(秒), [1]=dashSpeed, [2]=dashDistance, [3]=damage
/// </summary>
public class DashModule : BossSkillModule
{
    public float dashSpeed = 15f;
    public float dashDistance = 8f;
    public float damage = 30f;
    public float chargeTime = 0.3f;
    public float recoveryTime = 0.2f;
    public string targetTag = "Player";
    public LayerMask wallMask;

    private Transform _target;
    private TrailRenderer _trail;
    private SpriteRenderer[] _sprites;
    private Color[] _originalColors;
    private Rigidbody2D _rb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        requiresTarget = true;

        float[] p = ParseFloats(rawParams, 4);
        interval     = p[0] > 0f ? p[0] : 6f;
        dashSpeed    = p[1] > 0f ? p[1] : 15f;
        dashDistance = p[2] > 0f ? p[2] : 8f;
        damage       = p[3] > 0f ? p[3] : 30f;
        cooldown     = interval * 0.3f; // 出场后 30% 冷却首次攻击

        _rb = boss.GetComponent<Rigidbody2D>();
        _sprites = boss.GetComponentsInChildren<SpriteRenderer>();
        if (_sprites != null && _sprites.Length > 0)
        {
            _originalColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _originalColors[i] = _sprites[i].color;
        }

        _trail = boss.GetComponent<TrailRenderer>();
        if (_trail == null)
            _trail = boss.gameObject.AddComponent<TrailRenderer>();
        ConfigureTrail();

        Debug.Log($"[DashModule] 初始化完成 boss={boss.name} cooldown={interval}s speed={dashSpeed} dist={dashDistance} dmg={damage} rb={_rb != null} trail={_trail != null}");
    }

    private void ConfigureTrail()
    {
        if (_trail == null) return;

        _trail.time = 0.15f;
        _trail.startWidth = 0.35f;
        _trail.endWidth = 0.02f;
        _trail.minVertexDistance = 0.12f;
        _trail.autodestruct = false;
        _trail.emitting = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.7f, 0.15f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f, 0.3f), 0.5f),
                new GradientColorKey(new Color(1f, 0.15f, 0f, 0f), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.5f, 0f),
                new GradientAlphaKey(0.25f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        _trail.colorGradient = gradient;

        _trail.material = new Material(Shader.Find("Sprites/Default"));
        _trail.sortingOrder = 10;

        Debug.Log($"[DashModule] TrailRenderer 已配置 time={_trail.time}s width={_trail.startWidth}→{_trail.endWidth}");
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (_rb == null) return false;

        if (_target == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            _target = go != null ? go.transform : null;
        }
        return _target != null;
    }

    public override void Execute()
    {
        Debug.Log($"[DashModule] Execute! boss={boss.name} target={_target?.name} pos={_rb.position}");
        ResetCooldown();
        brain.StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        brain.IsBusy = true;
        Debug.Log("[DashModule] 蓄力...");

        // ── 蓄力阶段 ──
        SetSpritesFlash(true);
        if (_trail != null) _trail.emitting = true;
        yield return new WaitForSeconds(chargeTime);

        // ── 冲刺阶段 ──
        Vector2 dir = ((Vector2)_target.position - _rb.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        SetSpritesFlash(false);
        Debug.Log($"[DashModule] 冲刺! dir={dir} speed={dashSpeed} dist={dashDistance}");

        float remaining = dashDistance;
        int steps = 0;

        while (remaining > 0f && brain != null)
        {
            float step = Mathf.Min(dashSpeed * Time.fixedDeltaTime, remaining);
            Vector2 nextPos = _rb.position + dir * step;

            // 撞墙检测：只检测 wallMask 指定的层（默认 0 = 不检测任何层 = 不撞墙）
            if (wallMask.value != 0)
            {
                RaycastHit2D hit = Physics2D.Raycast(_rb.position, dir, step, wallMask);
                if (hit.collider != null)
                {
                    _rb.MovePosition(hit.point - dir * 0.2f);
                    Debug.Log($"[DashModule] 撞墙停止: {hit.collider.name}");
                    break;
                }
            }

            _rb.MovePosition(nextPos);
            remaining -= step;

            // 碰撞伤害
            CheckDashHit();

            steps++;
            yield return new WaitForFixedUpdate();
        }

        Debug.Log($"[DashModule] 冲刺结束 steps={steps} remaining={remaining:F2}");

        // ── 收招阶段 ──
        if (_trail != null) _trail.emitting = false;
        yield return new WaitForSeconds(recoveryTime);

        SetSpritesFlash(false);
        if (brain != null) brain.IsBusy = false;
        Debug.Log("[DashModule] 收招完成");
    }

    private void CheckDashHit()
    {
        float dmg = damage > 0f ? damage : boss.GetComponent<EnemyBase>()?.ContactDamage ?? 30f;
        float hitRadius = 1.2f;

        Collider2D hit = Physics2D.OverlapCircle(_rb.position, hitRadius);
        if (hit != null && hit.CompareTag(targetTag))
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph == null) ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(dmg, boss);
                Debug.Log($"[DashModule] 撞到玩家! dmg={dmg}");
            }
        }
    }

    private void SetSpritesFlash(bool flash)
    {
        if (_sprites == null || _originalColors == null) return;
        Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null)
                _sprites[i].color = flash ? flashColor : _originalColors[i];
        }
    }
}
