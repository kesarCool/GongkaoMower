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
    public LayerMask wallMask = -1; // -1 = Everything，Cast 天然排除自身碰撞体

    private TrailRenderer _trail;
    private Rigidbody2D _rb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
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
        Vector2 startPos = _rb.position;
        bool wallBlocked = false;

        while (remaining > 0f && brain != null)
        {
            float step = Mathf.Min(_dashSpeed * Time.fixedDeltaTime, remaining);
            Vector2 nextPos = _rb.position + dir * step;

            if (wallMask.value != 0)
            {
                // Rigidbody2D.Cast 天然排除自身碰撞体，且用完整碰撞形状扫描
                var filter = new ContactFilter2D();
                filter.SetLayerMask(wallMask);
                filter.useTriggers = false;
                var contacts = new RaycastHit2D[4];
                int count = _rb.Cast(dir, filter, contacts, step);
                if (count > 0)
                {
                    _rb.MovePosition(_rb.position + dir * contacts[0].distance);
                    wallBlocked = true;
                    break;
                }
            }

            _rb.MovePosition(nextPos);
            remaining -= step;
            CheckDashHit();
            yield return new WaitForFixedUpdate();
        }

        if (_trail != null) _trail.emitting = false;

        Vector2 endPos = _rb.position;
        float actualDist = Vector2.Distance(startPos, endPos);
        string stopReason = wallBlocked ? "撞墙" : (remaining <= 0f ? "满距" : "中断");
        Debug.Log($"[Dash] {stopReason} | 起点=({startPos.x:F1},{startPos.y:F1}) 终点=({endPos.x:F1},{endPos.y:F1}) 实际={actualDist:F1} 配置={_dashDistance:F1} dir=({dir.x:F2},{dir.y:F2}) CastHits={wallBlocked}");

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
