using UnityEngine;

/// <summary>
/// 固定 CD 向最近敌人投掷抛物线手雷，伤害与 AOE 半径由 <see cref="GrenadeSkillDefinition"/> 驱动。
/// </summary>
public class SkillThrowGrenade : SkillBase
{
    public GameObject grenadePrefab;
    public float cooldown = 2f;
    public float damage = 80f;
    public float aoeRadius = 1.5f;
    public float arcHeight = 1.2f;
    public float baseFlightTime = 0.45f;
    public float flightTimePerUnitDistance = 0.06f;
    public float minFlightTime = 0.35f;
    public float maxFlightTime = 1.1f;
    public float maxTargetRange = 14f;
    public GameObject explosionFxPrefab;

    private float _timer;

    public SkillThrowGrenade(GameObject grenadePrefab, float cooldown)
    {
        Id = SkillId.ThrowGrenade;
        this.grenadePrefab = grenadePrefab;
        this.cooldown = Mathf.Max(0.2f, cooldown);
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (grenadePrefab == null) return;
        if (_ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        _timer += deltaTime;
        if (_timer < cooldown) return;

        Vector3 from = _ctx.player.position;
        GameObject enemy = FindNearestEnemy(from, _ctx.enemyTag, maxTargetRange);
        if (enemy == null) return;

        if (SpawnLimiter.Instance != null && !SpawnLimiter.Instance.CanSpawn(GrenadeProjectile.SpawnLimiterKey, out _))
        {
            _timer = cooldown * 0.5f;
            return;
        }

        _timer = 0f;

        Vector2 start = from;
        Vector2 end = enemy.transform.position;
        float flight = ArcMotor2D.ComputeFlightDuration(
            start, end, baseFlightTime, flightTimePerUnitDistance, minFlightTime, maxFlightTime);

        GameObject go = GameObjectPool.Get(grenadePrefab, start, Quaternion.identity);
        if (go == null)
        {
            Debug.LogWarning("[SkillThrowGrenade] GameObjectPool.Get 返回 null，prefab=" + grenadePrefab.name);
            return;
        }

        GrenadeProjectile grenade = go.GetComponent<GrenadeProjectile>();
        if (grenade == null)
            grenade = go.AddComponent<GrenadeProjectile>();

        float finalDmg = GetFinalDamage(damage, out bool isCrit);
        grenade.Launch(start, end, flight, arcHeight, finalDmg, aoeRadius, SkillId.ThrowGrenade, _ctx.enemyTag, explosionFxPrefab, isCrit);
        SpawnLimiter.Instance?.RegisterSpawned(GrenadeProjectile.SpawnLimiterKey, go);
    }
}
