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
    private BossDashPresentation _presentation;
    private readonly RaycastHit2D[] _wallContacts = new RaycastHit2D[4];

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
        _presentation = boss.GetComponentInChildren<BossDashPresentation>(true);
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
        bool presented = _presentation != null;
        int token = 0;
        if (presented)
        {
            _presentation.BeginCharge(FindPlayer(), _chargeTime, _dashDistance, wallMask);
            token = _presentation.ChargeToken;
        }
        SetSpritesFlash(true, new Color(1f, 0.3f, 0.3f, 1f));
        if (_trail != null) _trail.emitting = true;
        yield return new WaitForSeconds(_chargeTime);

        if (DashCancelled(presented, token)) { AbortDash(presented, token); yield break; }

        Transform target = FindPlayer();
        Vector2 dir = target != null ? ((Vector2)target.position - _rb.position).normalized : Vector2.right;
        SetSpritesFlash(false);
        float remaining = _dashDistance;
        Vector2 startPos = _rb.position;
        Vector2 finalPosition = startPos;
        bool wallBlocked = false;
        if (presented) _presentation.Commit(startPos, dir, _dashDistance, wallMask);

        while (remaining > 0f && brain != null)
        {
            if (DashCancelled(presented, token)) { AbortDash(presented, token); yield break; }
            float step = Mathf.Min(_dashSpeed * Time.fixedDeltaTime, remaining);
            Vector2 nextPos = _rb.position + dir * step;

            float wallDistance;
            if (TryFindWallDistance(_rb, dir, step, wallMask, _wallContacts, out wallDistance))
            {
                finalPosition = _rb.position + dir * wallDistance;
                _rb.MovePosition(finalPosition);
                wallBlocked = true;
                break;
            }

            finalPosition = nextPos;
            _rb.MovePosition(nextPos);
            remaining -= step;
            CheckDashHit();
            yield return new WaitForFixedUpdate();
        }

        if (_trail != null) _trail.emitting = false;
        if (presented) _presentation.FinishDash(finalPosition, _recoveryTime);

        float actualDist = Vector2.Distance(startPos, _rb.position);
        GameLog.Info($"[Dash] {actualDist:F1}/{_dashDistance:F1}{(wallBlocked ? " 撞墙" : "")}");

        yield return new WaitForSeconds(_recoveryTime);

        if (DashCancelled(presented, token)) { AbortDash(presented, token); yield break; }
        SetSpritesFlash(false);
        if (presented) _presentation.CompleteRecovery();
        if (brain != null) brain.IsBusy = false;
    }

    private bool DashCancelled(bool presented, int token)
    {
        if (brain == null || boss == null || !boss.gameObject.activeInHierarchy || _rb == null) return true;
        var enemy = boss.GetComponent<EnemyBase>();
        if (enemy != null && enemy.Hp <= 0f) return true;
        return presented && (_presentation == null || !_presentation.isActiveAndEnabled || _presentation.ChargeToken != token);
    }

    private void AbortDash(bool presented, int token)
    {
        // A stale coroutine must not cancel a new charge on the same reused instance.
        if (presented && (_presentation == null || _presentation.ChargeToken != token)) return;
        SetSpritesFlash(false);
        if (_trail != null) { _trail.emitting = false; _trail.Clear(); }
        if (_presentation != null) _presentation.Cancel();
        else if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            if (_rb.gameObject.activeInHierarchy) _rb.MovePosition(_rb.position);
        }
        if (brain != null) brain.IsBusy = false;
    }

    public static bool TryFindWallDistance(Rigidbody2D body, Vector2 direction, float distance,
        LayerMask mask, RaycastHit2D[] contacts, out float wallDistance)
    {
        wallDistance = distance;
        if (body == null || mask.value == 0) return false;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = false;
        int count = body.Cast(direction, filter, contacts, distance);
        for (int i = 0; i < count; i++)
        {
            var col = contacts[i].collider;
            if (col == null) continue;
            // Keep the original wall filter: skip enemies and direct Player-tag colliders.
            if (col.GetComponentInParent<EnemyBase>() != null) continue;
            if (col.CompareTag("Player")) continue;
            wallDistance = contacts[i].distance;
            return true;
        }
        return false;
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
