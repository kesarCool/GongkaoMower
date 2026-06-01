using System;

/// <summary>
/// 被动技能注册表：将 SkillId 映射到具体的 PlayerHealth/PlayerSkills/PlayerController 字段读写。
/// 新增被动技能只需在此注册，无需新建 SkillPassive 子类。
/// </summary>
public static class PassiveSkillRegistry
{
    public static SkillPassive Create(SkillId id, PassiveModType type, float bonus)
    {
        switch (id)
        {
            case SkillId.PassiveMaxHp:
                return new SkillPassiveStat(id, type, bonus,
                    () => GetPlayerHealth() != null ? GetPlayerHealth().MaxHp : 0f,
                    v => { if (GetPlayerHealth() != null) GetPlayerHealth().SetMaxHpKeepRatio(v); });

            case SkillId.PassiveAttack:
                return new SkillPassiveStat(id, type, bonus,
                    () => GetPlayerSkills() != null ? GetPlayerSkills().attackMultiplier : 0f,
                    v => { if (GetPlayerSkills() != null) GetPlayerSkills().attackMultiplier = v; });

            case SkillId.PassiveMoveSpeed:
                return new SkillPassiveStat(id, type, bonus,
                    () => GetPlayerController() != null ? GetPlayerController().moveSpeed : 0f,
                    v => { if (GetPlayerController() != null) GetPlayerController().moveSpeed = v; });

            case SkillId.PassiveDefense:
                return new SkillPassiveStat(id, type, bonus,
                    () => GetPlayerHealth() != null ? GetPlayerHealth().Defense : 0f,
                    v => { if (GetPlayerHealth() != null) GetPlayerHealth().SetDefense(v); });

            case SkillId.PassiveCritRate:
                return new SkillPassiveStat(id, type, bonus,
                    () => GetPlayerSkills() != null ? GetPlayerSkills().critRate : 0f,
                    v => { if (GetPlayerSkills() != null) GetPlayerSkills().critRate = Math.Min(v, 1f); });

            case SkillId.PassiveRegen:
                return new SkillPassiveRegen(id, bonus);

            default:
                return null;
        }
    }

    // 轻量缓存——被动技能 Tick 在 PlayerSkills.Update 中同一帧调用，无需担心生命周期不一致
    private static PlayerHealth _cachedHealth;
    private static PlayerSkills _cachedSkills;
    private static PlayerController _cachedController;

    internal static void SetPlayer(UnityEngine.GameObject player)
    {
        _cachedHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        _cachedSkills = player != null ? player.GetComponent<PlayerSkills>() : null;
        _cachedController = player != null ? player.GetComponent<PlayerController>() : null;
    }

    private static PlayerHealth GetPlayerHealth() => _cachedHealth;
    private static PlayerSkills GetPlayerSkills() => _cachedSkills;
    private static PlayerController GetPlayerController() => _cachedController;
}
