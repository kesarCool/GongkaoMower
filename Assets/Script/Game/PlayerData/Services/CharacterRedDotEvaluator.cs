/// <summary>
/// 角色红点共用评估逻辑。
/// 供 RedDotService（Tab 聚合计数）、CharacterSelectionElement（列表 cell 红点）、
/// CharacterSelectionPanel（Toggle 红点）共用，避免四处散落相同判断。
/// </summary>
public static class CharacterRedDotEvaluator
{
    /// <summary>该角色是否有待处理项（碎片解锁 / 可升级 / 可升阶 任一满足）。</summary>
    public static bool HasPendingAction(CharacterDefinition def)
    {
        if (def == null) return false;

        // 1. 碎片解锁（未解锁且碎片集齐）
        if (CharacterUnlockEvaluator.CanFragmentUnlock(def))
            return true;

        if (!CharacterUnlockEvaluator.IsUnlocked(def))
            return false;

        var svc = PlayerProfileService.Instance;

        // 2. 可升级（未达当前阶位满级 + 金币够）
        if (CanUpgrade(def))
            return true;

        // 3. 可升阶
        if (CanPromote(def))
            return true;

        return false;
    }

    /// <summary>已解锁角色是否可升级（未满级 + 金币够）。未解锁返回 false。</summary>
    public static bool CanUpgrade(CharacterDefinition def)
    {
        if (def == null) return false;
        if (!CharacterUnlockEvaluator.IsUnlocked(def)) return false;
        if (def.upgradeData == null) return false;

        var svc = PlayerProfileService.Instance;
        int lv = svc.GetHeroLevel(def.characterId);
        int maxLv = svc.GetEffectiveMaxLevel(def.characterId, def.upgradeData);
        if (lv >= maxLv) return false;

        int cost = def.upgradeData.GetCostForLevel(lv + 1);
        return svc.CanAffordGold(cost);
    }

    /// <summary>已解锁角色是否可升阶。未解锁或已是最高阶返回 false。</summary>
    public static bool CanPromote(CharacterDefinition def)
    {
        if (def == null) return false;
        if (!CharacterUnlockEvaluator.IsUnlocked(def)) return false;
        if (def.upgradeData == null) return false;

        return PlayerProfileService.Instance.CanPromoteStage(
            def.characterId, def.upgradeData, out _, out _);
    }
}
