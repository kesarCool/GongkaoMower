using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单局战斗统计（进入战斗 Reset，结算只读快照）。
/// </summary>
public static class BattleRunMetrics
{
    private static readonly Dictionary<SkillId, float> sDamageBySkill = new Dictionary<SkillId, float>(8);
    private static float sBattleStartUnscaled;

    public static void BeginBattle()
    {
        sDamageBySkill.Clear();
        sBattleStartUnscaled = Time.unscaledTime;
    }

    public static void AddSkillDamage(SkillId id, float amount)
    {
        if (id == SkillId.None) return;
        if (amount <= 0f) return;
        if (sDamageBySkill.TryGetValue(id, out float cur))
            sDamageBySkill[id] = cur + amount;
        else
            sDamageBySkill[id] = amount;
    }

    public static float GetBattleElapsedUnscaled()
    {
        return Mathf.Max(0f, Time.unscaledTime - sBattleStartUnscaled);
    }

    public static float GetSkillDamage(SkillId id)
    {
        return sDamageBySkill.TryGetValue(id, out float v) ? v : 0f;
    }

    public static IReadOnlyDictionary<SkillId, float> SnapshotDamageBySkill() => sDamageBySkill;
}
