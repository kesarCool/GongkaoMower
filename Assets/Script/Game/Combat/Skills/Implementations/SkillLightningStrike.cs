using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 雷击术：按间隔触发一轮落雷；一轮内多道雷电按 <see cref="strikeStagger"/> 逐道释放。
/// </summary>
public class SkillLightningStrike : SkillBase
{
    public float interval = 2f;
    public float damage = 100f;
    public float strikeRadius = 1.2f;
    public int strikeCount = 1;
    public float strikeStagger = 0.18f;
    public float maxRange = 14f;
    public GameObject strikeFxPrefab;

    private float _cooldownTimer;
    private int _strikesRemaining;
    private float _staggerTimer;

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

    public SkillLightningStrike(GameObject strikeFxPrefab)
    {
        Id = SkillId.LightningStrike;
        this.strikeFxPrefab = strikeFxPrefab;
    }

    public void ApplyRuntimeStats(
        float newInterval,
        float newDamage,
        float newStrikeRadius,
        int newStrikeCount,
        float newStrikeStagger,
        float newMaxRange)
    {
        interval = Mathf.Max(0.3f, newInterval);
        damage = Mathf.Max(0.01f, newDamage);
        strikeRadius = Mathf.Max(0.2f, newStrikeRadius);
        strikeCount = Mathf.Max(1, newStrikeCount);
        strikeStagger = Mathf.Max(0.05f, newStrikeStagger);
        maxRange = Mathf.Max(1f, newMaxRange);
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        _cooldownTimer = 0f;
        _strikesRemaining = 0;
        _staggerTimer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped || _ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        if (_strikesRemaining > 0)
        {
            _staggerTimer += deltaTime;
            if (_staggerTimer < strikeStagger) return;

            _staggerTimer = 0f;
            TryStrikeOnce();
            _strikesRemaining--;
            return;
        }

        _cooldownTimer += deltaTime;
        if (_cooldownTimer < interval) return;

        float rangeMul = (GetPlayerSkills()?.attackRangeMul) ?? 1f;
        if (CombatTargetRegistry.CountInRange(_ctx.enemyTag, _ctx.player.position, maxRange * rangeMul) <= 0)
            return;

        _cooldownTimer = 0f;
        TryStrikeOnce();

        int followUp = strikeCount - 1;
        if (followUp <= 0) return;

        _strikesRemaining = followUp;
        _staggerTimer = 0f;
    }

    private void TryStrikeOnce()
    {
        float range = maxRange * (GetPlayerSkills()?.attackRangeMul ?? 1f);
        if (!CombatTargetRegistry.TryPickRandomInRange(
                _ctx.enemyTag, _ctx.player.position, range, out Transform target) ||
            target == null)
            return;

        Vector2 center = target.position;
        var fxPos = new Vector3(center.x, center.y, -0.1f);
        CombatVfxSpawner.TryPlayPooled(strikeFxPrefab, fxPos, Quaternion.identity);
        ApplyStrikeDamage(center);
        PublishSkillCast(fxPos);
    }

    private void ApplyStrikeDamage(Vector2 center)
    {
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        float finalDmg = GetFinalDamage(damage, out bool isCrit);
        bool prevQueries = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        float radiusSq = strikeRadius * strikeRadius;
        int count = Physics2D.OverlapCircleNonAlloc(center, strikeRadius, OverlapBuffer);
        var damaged = new HashSet<int>(8);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = OverlapBuffer[i];
            if (col == null) continue;

            EnemyBase eb = col.GetComponent<EnemyBase>();
            if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
            if (eb == null || !eb.gameObject.CompareTag(_ctx.enemyTag)) continue;

            int id = eb.GetInstanceID();
            if (!damaged.Add(id)) continue;

            if (((Vector2)eb.transform.position - center).sqrMagnitude > radiusSq)
                continue;

            eb.TakeDamage(finalDmg, SkillId.LightningStrike, isCrit);
        }

        Physics2D.queriesHitTriggers = prevQueries;
    }
}
