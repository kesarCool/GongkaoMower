using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 追踪导弹技能：CD 到期后锁定范围内敌人，连发追踪导弹，命中后 AOE 爆炸。
/// </summary>
public class SkillHomingMissile : SkillBase
{
    public GameObject missilePrefab;
    public float missileSpeed = 8f;
    public float turnRate = 180f;
    public float missileLifetime = 4f;
    public GameObject explosionFxPrefab;
    public float cooldown = 2f;
    public float damage = 1f;
    public int salvoCount = 2;
    public int maxTargets = 2;
    public float aoeRadius = 1.2f;
    public float maxRange = 10f;
    public float salvoInterval = 0.12f;
    public GameObject maxLevelPrefab;
    public int skillMaxLevel = 5;

    private float _timer;
    private int _salvosRemaining;
    private float _salvoTimer;
    private List<Transform> _savedTargets = new List<Transform>(6);

    public SkillHomingMissile(GameObject missilePrefab, SkillId skillId = SkillId.HomingMissile)
    {
        Id = skillId;
        this.missilePrefab = missilePrefab;
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        // 连发中
        if (_salvosRemaining > 0)
        {
            _salvoTimer += deltaTime;
            while (_salvosRemaining > 0 && _salvoTimer >= salvoInterval)
            {
                _salvoTimer -= salvoInterval;
                _salvosRemaining--;
                FireSalvoRound(_salvosRemaining);
            }
            if (_salvosRemaining <= 0)
            {
                PublishSkillCast(_ctx.player.position);
            }
            return;
        }

        _timer += deltaTime;
        if (_timer < cooldown) return;

        // 收集目标
        CombatTargetRegistry.CollectTargets(_ctx.enemyTag, _ctx.player.position, maxRange, _savedTargets);
        if (_savedTargets.Count == 0) return;

        // 始终发射 salvoCount 枚导弹，敌少时多枚追同一目标
        int count = salvoCount;
        if (count <= 0) return;

        // 按距离排序取前 maxTargets 个最近目标
        _savedTargets.Sort((a, b) =>
        {
            if (a == null || b == null) return 0;
            float da = ((Vector2)(a.position - _ctx.player.position)).sqrMagnitude;
            float db = ((Vector2)(b.position - _ctx.player.position)).sqrMagnitude;
            return da.CompareTo(db);
        });

        _timer = 0f;
        _salvosRemaining = count;
        _salvoTimer = 0f;

        // 第一发立即发射
        _salvosRemaining--;
        FireSalvoRound(count - _salvosRemaining - 1);
    }

    private void FireSalvoRound(int roundIndex)
    {
        Vector3 playerPos = _ctx.player.position;
        int capped = Mathf.Min(_savedTargets.Count, maxTargets);
        if (capped <= 0) return;
        int idx = roundIndex % capped;
        Transform target = _savedTargets[idx];
        if (target == null) return;

        // 从角色外围发射，加横向随机散布避免多弹同时生成在同一位置
        Vector2 toTarget = (Vector2)(target.position - playerPos);
        if (toTarget.sqrMagnitude < 0.001f) toTarget = Random.insideUnitCircle;
        Vector2 perp = new Vector2(-toTarget.y, toTarget.x).normalized;
        Vector3 spawnPos = playerPos + (Vector3)(toTarget.normalized * 1.5f + perp * Random.Range(-0.5f, 0.5f));
        Vector2 dir = ((Vector2)(target.position - spawnPos)).normalized;

        GameObject prefab = (maxLevelPrefab != null && Level >= skillMaxLevel) ? maxLevelPrefab : missilePrefab;
        GameObject missile = Object.Instantiate(prefab, spawnPos, Quaternion.identity);

        var hm = missile.GetComponent<HomingMissileBullet>();
        if (hm != null)
        {
            float finalDmg = GetFinalDamage(damage, out bool isCrit);
            hm.turnRate = turnRate;
            hm.Launch(dir, missileSpeed, finalDmg, missileLifetime, Id,
                target, aoeRadius, explosionFxPrefab, _ctx.enemyTag);
        }
        else
        {
            Debug.LogWarning($"[SkillHomingMissile] 导弹Prefab上未挂 HomingMissileBullet! prefab={missilePrefab?.name}");
        }

        SpawnLimiter.Instance?.RegisterSpawned("Missile", missile);
    }
}
