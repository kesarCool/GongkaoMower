using UnityEngine;

/// <summary>通关星级：按结算时血量比例计算，写入时与历史最高取 max。</summary>
public static class LevelStarRules
{
    public static int ComputeStars(float hp, float maxHp)
    {
        if (maxHp <= 0f) return 1;
        float ratio = Mathf.Clamp01(hp / maxHp);
        if (ratio >= 1f) return 3;
        if (ratio > 0.5f) return 2;
        return 1;
    }
}
