using System.Collections;
using UnityEngine;

/// <summary>
/// 冲刺技能：蓄力 → 直线高速冲锋 → 收招。
/// elementNum = "6,15,8,30" = cooldown, dashSpeed, dashDistance, damage
/// </summary>
public class DashModule : BossSkillModule
{
    private float _dashSpeed = 15f;
    private float _dashDistance = 8f;
    private float _damage = 30f;
    private float _chargeTime = 0.3f;
    private float _recoveryTime = 0.2f;
    private LayerMask _wallMask;

    private TrailRenderer _trail;
    private Rigidbody2D _rb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        requiresTarget = true;

        float[] p = ParseFloats(rawParams, 4);
        interval     = p[0] > 0f ? p[0] : 6f;
        _dashSpeed   = p[1] > 0f ? p[1] : 15f;
        _dashDistance = p[2] > 0f ? p[2] : 8f;
        _damage       = p[3] > 0f ? p[3] : 30f;
        cooldown     = interval * firstDelayMul;

        _rb = boss.GetComponent<Rigidbody2D>();
        CacheSprites();

        _trail = boss.GetComponent<TrailRenderer>();
        if (_trail != null) { _trail.emitting = false; _trail.autodestruct = false; }
    }

    public override bool CanTrigger() => base.CanTrigger() && _rb != null && FindPlayer() != null;

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true, new Color(1f, 0.3f, 0.3f, 1f));
        if (_trail != null) _trail.emitting = true;
        yield return new WaitForSeconds(_chargeTime);

        Transform target = FindPlayer();
        Vector2 dir = target != null ? ((Vector2)target.position - _rb.position).normalized : Vector2.right;
        SetSpritesFlash(false);
        float remaining = _dashDistance;

        while (remaining > 0f && brain != null)
        {
            float step = Mathf.Min(_dashSpeed * Time.fixedDeltaTime, remaining);
            Vector2 nextPos = _rb.position + dir * step;

            if (_wallMask.value != 0)
            {
                var hit = Physics2D.Raycast(_rb.position, dir, step, _wallMask);
                if (hit.collider != null) { _rb.MovePosition(hit.point - dir * 0.2f); break; }
            }

            _rb.MovePosition(nextPos);
            remaining -= step;
            CheckDashHit();
            yield return new WaitForFixedUpdate();
        }

        if (_trail != null) _trail.emitting = false;
        yield return new WaitForSeconds(_recoveryTime);

        SetSpritesFlash(false);
        if (brain != null) brain.IsBusy = false;
    }

    private void CheckDashHit()
    {
        float dmg = ResolveDamage(_damage, 30f);
        var hit = Physics2D.OverlapCircle(_rb.position, 1.2f);
        if (hit == null || !hit.CompareTag("Player")) return;
        var ph = hit.GetComponent<PlayerHealth>();
        if (ph == null) ph = hit.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(dmg, boss);
    }
}
