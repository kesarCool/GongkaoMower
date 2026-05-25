using UnityEngine;

/// <summary>
/// 能量等级配置：Lv1~LvN 各需多少能量触发选卡。
/// 第 0 条 = Lv1 所需能量，第 1 条 = Lv2，以此类推。
/// 数组长度不足时自动推导：最后一级之后每级 +1。
/// </summary>
[CreateAssetMenu(menuName = "Game/Energy Level Config", fileName = "EnergyLevelConfig")]
public class EnergyLevelConfig : ScriptableObject
{
    [Tooltip("Lv1 触发选卡所需能量，第 0 条 = Lv1，第 1 条 = Lv2 ...")]
    public int[] levelThresholds = { 5, 8, 12, 18, 25 };

    /// <summary>获取第 N 级所需能量（1-based: 1=第一次选卡）。</summary>
    public int GetRequiredEnergy(int level)
    {
        int idx = level - 1;
        if (idx < 0) idx = 0;

        if (levelThresholds == null || levelThresholds.Length == 0)
            return level; // 兜底：Lv1=1, Lv2=2 ...

        if (idx < levelThresholds.Length)
            return Mathf.Max(1, levelThresholds[idx]);

        // 超出数组：最后一级的值 + 超出的级数
        int last = levelThresholds[levelThresholds.Length - 1];
        return Mathf.Max(1, last + (idx - levelThresholds.Length + 1));
    }

    /// <summary>最大配置等级数。</summary>
    public int MaxConfiguredLevel => levelThresholds?.Length ?? 0;

    /// <summary>根据当前能量值算当前等级（1-based）。</summary>
    public int GetCurrentLevel(int energy)
    {
        if (energy <= 0) return 1;
        for (int i = 0; i < (levelThresholds?.Length ?? 0); i++)
        {
            if (energy < levelThresholds[i])
                return i + 1; // 已过 i 级，正冲向第 i+1 级（1-based）
        }
        return (levelThresholds?.Length ?? 0) + 1;
    }
}
