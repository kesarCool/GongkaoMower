using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色解锁判定。
/// 优先级：SO 默认 unlocked > 已通关解锁 > 碎片手动解锁。
/// 碎片集齐后不会自动解锁，需玩家点击确认消耗碎片。
/// </summary>
public static class CharacterUnlockEvaluator
{
    /// <summary>返回未解锁角色的解锁条件提示文本。</summary>
    public static string GetUnlockHint(CharacterDefinition def)
    {
        if (def == null) return "";
        if (IsUnlocked(def)) return "";
        if (def.unlockLevelId > 0)
        {
            int chapter = def.unlockLevelId / 100;
            int stage = def.unlockLevelId % 100;
            return $"通关关卡 {chapter}-{stage} 解锁";
        }
        if (def.unlockFragmentCount > 0)
        {
            int have = GetFragmentCount(PlayerProfileService.Instance.Data, def.characterId, def.fragmentItemId);
            int need = def.unlockFragmentCount;
            return $"集齐 {need} 片「{def.displayName}」碎片可解锁（{have}/{need}）";
        }
        return "暂未开放";
    }

    /// <summary>角色是否已解锁（SO 默认未锁 + 已通关解锁 + 碎片已手动解锁）。</summary>
    public static bool IsUnlocked(CharacterDefinition def)
    {
        if (def == null) return false;
        if (!def.locked) return true; // SO 默认未锁

        var data = PlayerProfileService.Instance.Data;
        if (data == null) return false;

        // 已通关解锁 / 碎片手动解锁（均在 unlockedCharacters 列表中）
        if (data.unlockedCharacters != null)
        {
            for (int i = 0; i < data.unlockedCharacters.Length; i++)
                if (data.unlockedCharacters[i] == def.characterId)
                    return true;
        }

        return false;
    }

    /// <summary>碎片数量是否足够触发手动解锁流程。</summary>
    public static bool CanFragmentUnlock(CharacterDefinition def)
    {
        if (def == null) return false;
        if (IsUnlocked(def)) return false;
        if (def.unlockFragmentCount <= 0) return false;

        int have = GetFragmentCount(PlayerProfileService.Instance.Data, def.characterId, def.fragmentItemId);
        return have >= def.unlockFragmentCount;
    }

    /// <summary>消耗碎片并解锁角色。返回 (成功, 消耗数量)。</summary>
    public static (bool success, int cost) TryConsumeFragmentUnlock(CharacterDefinition def)
    {
        if (def == null) return (false, 0);
        if (!CanFragmentUnlock(def)) return (false, 0);

        int cost = def.unlockFragmentCount;
        var data = PlayerProfileService.Instance.Data;
        if (data == null) return (false, 0);

        // 扣除碎片（characterFragmentKeys）
        int idx = data.characterFragmentKeys != null
            ? System.Array.IndexOf(data.characterFragmentKeys, def.characterId) : -1;
        if (idx < 0 || idx >= (data.characterFragmentValues?.Length ?? 0))
            return (false, 0);

        data.characterFragmentValues[idx] -= cost;

        // 同步扣除 itemIds/itemCounts（背包面板展示用）
        if (def.fragmentItemId > 0 && data.itemIds != null && data.itemCounts != null)
        {
            int itemIdx = System.Array.IndexOf(data.itemIds, def.fragmentItemId);
            if (itemIdx >= 0 && itemIdx < data.itemCounts.Length)
                data.itemCounts[itemIdx] = Mathf.Max(0, data.itemCounts[itemIdx] - cost);
        }

        // 记录解锁
        AddUnlockedCharacter(data, def.characterId);
        PlayerProfileService.Instance.MarkDirtyAndSave();

        Debug.Log($"[CharUnlock] 碎片解锁：{def.displayName}，消耗 {cost} 碎片");
        return (true, cost);
    }

    /// <summary>通关关卡时记录解锁（胜利结算调用）。返回新解锁角色名列表。</summary>
    public static List<string> OnLevelCleared(int levelId)
    {
        var newUnlocks = new List<string>();
        var data = PlayerProfileService.Instance.Data;
        if (data == null) return newUnlocks;

        var catalog = GetCatalog();
        if (catalog == null) return newUnlocks;

        foreach (var def in catalog.characters)
        {
            if (def == null) continue;
            if (def.unlockLevelId == levelId && def.locked && !IsUnlocked(def))
            {
                AddUnlockedCharacter(data, def.characterId);
                newUnlocks.Add(def.displayName);
            }
        }

        if (newUnlocks.Count > 0)
            PlayerProfileService.Instance.MarkDirtyAndSave();

        return newUnlocks;
    }

    public static int GetFragmentCount(PlayerSaveData data, string charId, int fragmentItemId = 0)
    {
        int countFromKeys = 0;
        int countFromItems = 0;

        // 1. 专用碎片数组
        if (data?.characterFragmentKeys != null)
        {
            int idx = System.Array.IndexOf(data.characterFragmentKeys, charId);
            if (idx >= 0 && idx < (data.characterFragmentValues?.Length ?? 0))
                countFromKeys = data.characterFragmentValues[idx];
        }

        // 2. 通用物品背包
        if (fragmentItemId > 0 && data?.itemIds != null)
        {
            int idx = System.Array.IndexOf(data.itemIds, fragmentItemId);
            if (idx >= 0 && idx < (data.itemCounts?.Length ?? 0))
                countFromItems = data.itemCounts[idx];
        }

        // 取两者较大值（防御 TryRouteFragment 偶发失败导致两数组不一致）
        return Mathf.Max(countFromKeys, countFromItems);
    }

    private static void AddUnlockedCharacter(PlayerSaveData data, string charId)
    {
        if (data.unlockedCharacters == null)
        {
            data.unlockedCharacters = new[] { charId };
            EventBus.Publish(new CharacterUnlockedEvent { characterId = charId });
            return;
        }
        if (System.Array.IndexOf(data.unlockedCharacters, charId) >= 0) return;
        var list = new List<string>(data.unlockedCharacters) { charId };
        data.unlockedCharacters = list.ToArray();
        EventBus.Publish(new CharacterUnlockedEvent { characterId = charId });
    }

    private static CharacterCatalog GetCatalog()
    {
        var cca = Object.FindObjectOfType<CharacterConfigApplier>();
        if (cca != null && cca.characterCatalog != null) return cca.characterCatalog;
        return Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
    }
}
