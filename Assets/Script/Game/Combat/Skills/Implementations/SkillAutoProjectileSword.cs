using UnityEngine;

/// <summary>
/// AutoProjectile 变体：剑气（爆发突破）。
/// Legend 突破：projectileCount +2 + burstCount ×2 + pierceCount +2 + 暴击分裂
/// </summary>
public sealed class SkillAutoProjectileSword : SkillAutoProjectile
{
    protected override int LegendProjectileBonus => 1;

    private int _baseBurstCount;
    private int _basePierceCount;
    private bool _pierceApplied;

    public SkillAutoProjectileSword(GameObject bulletPrefab, float bulletSpeed, float interval, SkillId skillId)
        : base(bulletPrefab, bulletSpeed, interval, skillId) { }

    protected override void ApplyBreakthroughStats()
    {
        base.ApplyBreakthroughStats();

        // burstCount 翻倍
        if (_baseBurstCount == 0) _baseBurstCount = burstCount;
        burstCount = _baseBurstCount * 2;
        burstEnabled = true;

        // 穿透 +2（仅一次）
        var ps = GetPlayerSkills();
        if (!_pierceApplied && ps != null)
        {
            _basePierceCount = ps.pierceCount;
            ps.pierceCount = _basePierceCount + 2;
            _pierceApplied = true;
        }

        // 暴击分裂：注入 maxLevelPrefab
        if (maxLevelPrefab != null)
        {
            var split = maxLevelPrefab.GetComponent<CritSplitOnHit>();
            if (split == null)
            {
                split = maxLevelPrefab.AddComponent<CritSplitOnHit>();
                // Debug.Log($"[SkillAutoProjectileSword] CritSplitOnHit 已注入 maxLevelPrefab={maxLevelPrefab.name}");
            }
            split.splitCount = 3;
            split.splitDmgMul = 0.4f;
            split.splitLifetime = 1.2f;
            split.splitBulletPrefab = bulletPrefab;
            _needsCritSplit = true;
        }

        // Debug.Log($"[SkillAutoProjectileSword] Legend 突破（爆发）：burstCount={burstCount}, pierceCount={(ps != null ? ps.pierceCount : -1)}, burstEnabled={burstEnabled}");
    }
}
