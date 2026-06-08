using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色解锁判定。
/// 优先级：通关解锁 > 碎片解锁 > SO 默认 unlocked（未被任何逻辑覆盖时回退）。
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
            return $"集齐 {def.unlockFragmentCount} 片碎片解锁";
        return "暂未开放";
    }

    /// <summary>角色是否已解锁（读存档 + SO 默认）。</summary>
    public static bool IsUnlocked(CharacterDefinition def)
    {
        if (def == null) return false;
        if (!def.locked) return true; // SO 默认未锁

        var data = PlayerProfileService.Instance.Data;
        if (data == null) return false;

        // 已通关解锁
        if (data.unlockedCharacters != null)
        {
            for (int i = 0; i < data.unlockedCharacters.Length; i++)
                if (data.unlockedCharacters[i] == def.characterId)
                    return true;
        }

        // 碎片解锁（暂不启用，默认片段数阈值 = 999）
        int frags = GetFragmentCount(data, def.characterId);
        if (frags >= def.unlockFragmentCount && def.unlockFragmentCount > 0)
            return true;

        return false;
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

    public static int GetFragmentCount(PlayerSaveData data, string charId)
    {
        if (data?.characterFragmentKeys == null) return 0;
        int idx = System.Array.IndexOf(data.characterFragmentKeys, charId);
        if (idx < 0 || idx >= (data.characterFragmentValues?.Length ?? 0)) return 0;
        return data.characterFragmentValues[idx];
    }

    private static void AddUnlockedCharacter(PlayerSaveData data, string charId)
    {
        if (data.unlockedCharacters == null)
        {
            data.unlockedCharacters = new[] { charId };
            return;
        }
        if (System.Array.IndexOf(data.unlockedCharacters, charId) >= 0) return;
        var list = new List<string>(data.unlockedCharacters) { charId };
        data.unlockedCharacters = list.ToArray();
    }

    private static CharacterCatalog GetCatalog()
    {
        var cca = Object.FindObjectOfType<CharacterConfigApplier>();
        if (cca != null && cca.characterCatalog != null) return cca.characterCatalog;
        return Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
    }
}
