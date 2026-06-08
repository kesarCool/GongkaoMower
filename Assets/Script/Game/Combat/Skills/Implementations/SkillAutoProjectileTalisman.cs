using UnityEngine;

/// <summary>
/// AutoProjectile 变体：符箓弹。
/// Rare: 符箓环绕（TraitVampiricHeal）
/// Legend: 符箓风暴（追踪导弹 2 波连射）+ projectileCount+2 + 暴击率+15%
/// </summary>
public sealed class SkillAutoProjectileTalisman : SkillAutoProjectile
{
    private bool _critApplied;
    private bool _burstStatsSaved;
    private int _baseBurstCount;
    private int _stormWave;
    private float _stormTimer;
    private int _stormBulletsLeft;
    private float _stormSpawnInterval = 0.08f;
    private float _stormSpawnTimer;

    public SkillAutoProjectileTalisman(GameObject bulletPrefab, float bulletSpeed, float interval, SkillId skillId)
        : base(bulletPrefab, bulletSpeed, interval, skillId) { }

    protected override void ApplyBreakthroughStats()
    {
        base.ApplyBreakthroughStats();

        if (!_burstStatsSaved) { _baseBurstCount = burstCount; _burstStatsSaved = true; }
        burstCount = _baseBurstCount * 2;

        if (!_critApplied)
        {
            var ps = GetPlayerSkills();
            if (ps != null) { ps.critRate += 0.15f; _critApplied = true; }
        }
    }

    public override void Tick(float deltaTime)
    {
        if (_stormWave > 0)
            UpdateStorm(deltaTime);
        else
            base.Tick(deltaTime);
    }

    protected override void TryFireBurst(float deltaTime)
    {
        if (_legendStage < 2) { base.TryFireBurst(deltaTime); return; }
        if (!IsBurstReady) { Debug.Log($"[Talisman] 爆发未就绪：burstEnabled={burstEnabled}, Level={Level}"); return; }
        if (burstBulletPrefab == null) { Debug.LogWarning("[Talisman] burstBulletPrefab 为空"); return; }
        if (_ctx.player == null) { Debug.LogWarning("[Talisman] _ctx.player 为空"); return; }

        Debug.Log($"[Talisman] TryFireBurst 触发！legendStage={_legendStage}, burstCount={burstCount}, cd={burstCooldown}");

        _burstTimer += deltaTime;
        if (_burstTimer < burstCooldown) return;
        _burstTimer = 0f;

        // 确保 burstBulletPrefab 带强追踪
        if (burstBulletPrefab.GetComponent<HomingOverride>() == null)
        {
            var h = burstBulletPrefab.AddComponent<HomingOverride>();
            Debug.Log($"[Talisman] HomingOverride 已注入 burstBulletPrefab={burstBulletPrefab.name}, turnRate={h.turnRate}");
        }

        // 启动 3 波风暴
        _stormWave = 1;
        _stormTimer = 0f;
        _stormBulletsLeft = burstCount;
        _stormSpawnTimer = 0f;
    }

    private void UpdateStorm(float deltaTime)
    {
        // 波间等待
        if (_stormBulletsLeft <= 0)
        {
            _stormTimer += deltaTime;
            if (_stormTimer < 1f) return;
            _stormTimer = 0f;
            _stormWave++;
            if (_stormWave > 2) { _stormWave = 0; PublishSkillCast(_ctx.player.position); return; }
            _stormBulletsLeft = burstCount;
            _stormSpawnTimer = 0f;
        }

        _stormSpawnTimer += deltaTime;
        while (_stormSpawnTimer >= _stormSpawnInterval && _stormBulletsLeft > 0)
        {
            _stormSpawnTimer -= _stormSpawnInterval;
            _stormBulletsLeft--;
            SpawnStormBullet(_stormBulletsLeft);
        }
    }

    private void SpawnStormBullet(int index)
    {
        if (_ctx.player == null) return;
        int total = burstCount;
        float angle = 360f / total * index;
        Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
        Vector3 pos = _ctx.player.position; // 直接从角色身上发出

        GameObject bullet = GameObjectPool.Get(burstBulletPrefab, pos, Quaternion.identity);
        if (bullet == null)
            bullet = Object.Instantiate(burstBulletPrefab, pos, Quaternion.identity);

        var existing = bullet.GetComponent<HomingOverride>();
        if (existing == null)
            bullet.AddComponent<HomingOverride>();

        var bb = bullet.GetComponent<AutoProjectileBurstBullet>();
        if (bb != null)
        {
            float finalDmg = GetFinalDamage(damage, out bool isCrit);
            var ps = GetPlayerSkills();
            bb.LaunchBurst(dir, burstLaunchSpeed, finalDmg, 5f, Id,
                ps != null ? ps.pierceCount : 0, isCrit,
                ps != null ? ps.pierceRate : 0f,
                0f, 0f, angle, _ctx.player, false, Vector2.zero);
        }

        SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);
    }
}
