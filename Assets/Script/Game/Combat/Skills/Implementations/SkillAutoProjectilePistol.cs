using UnityEngine;

/// <summary>
/// AutoProjectile 变体：手枪弹（散射突破）。
/// Legend 突破：projectileCount +2 + ScatterBullet.scatterCount ×2
/// </summary>
public sealed class SkillAutoProjectilePistol : SkillAutoProjectile
{
    private int _baseScatterCount;

    public SkillAutoProjectilePistol(GameObject bulletPrefab, float bulletSpeed, float interval, SkillId skillId)
        : base(bulletPrefab, bulletSpeed, interval, skillId) { }

    protected override void ApplyBreakthroughStats()
    {
        if (_legendStage < 2) return;
        base.ApplyBreakthroughStats();

        var scatter = maxLevelPrefab?.GetComponentInChildren<ScatterBullet>();
        if (scatter == null)
        {
            // Debug.LogWarning($"[SkillAutoProjectilePistol] maxLevelPrefab 上未找到 ScatterBullet。maxLevelPrefab={maxLevelPrefab?.name}");
            return;
        }

        if (_baseScatterCount == 0) _baseScatterCount = scatter.scatterCount;
        scatter.scatterCount = _baseScatterCount * 2;
        // Debug.Log($"[SkillAutoProjectilePistol] Legend 突破（散射）：scatterCount={scatter.scatterCount}（base={_baseScatterCount}）");
    }
}
