using System;
using UnityEngine;

/// <summary>
/// 角色属性，配置在 CharacterDefinition 中，由 CharacterConfigApplier 写入运行时组件。
/// </summary>
[Serializable]
public struct CharacterAttributes
{
    [Header("攻击")]
    [Tooltip("攻击力，作为技能伤害系数（1.0 = 技能表原始伤害）。")]
    public float attack;

    [Tooltip("攻速倍率（1.0 = 不变，0.8 = 快 20%）。")]
    public float attackSpeedMul;

    [Header("防御")]
    [Tooltip("最大血量。")]
    public float maxHp;

    [Tooltip("减伤值，公式：最终伤害 = 原始伤害 × 100 / (100 + defense)。")]
    public float defense;

    [Header("机动")]
    [Tooltip("移动速度。")]
    public float moveSpeed;

    [Header("特殊")]
    [Tooltip("暴击率 (0~1，如 0.15 = 15%)。")]
    [Range(0f, 1f)]
    public float critRate;

    [Tooltip("暴击倍率。")]
    public float critDamageMul;

    [Tooltip("穿透率 (0~1，如 0.2 = 20%)。")]
    [Range(0f, 1f)]
    public float pierceRate;

    [Tooltip("穿透数（0 = 不穿透，子弹命中后不回收可继续飞行）。")]
    public int pierceCount;

    public static CharacterAttributes Defaults => new CharacterAttributes
    {
        attack = 1f,
        attackSpeedMul = 1f,
        maxHp = 100f,
        defense = 0f,
        moveSpeed = 6f,
        critRate = 0f,
        critDamageMul = 2f,
        pierceRate = 0f,
        pierceCount = 0,
    };

    /// <summary>将 0 值字段回填为合理默认值，保证未配置的角色也能正常运行。</summary>
    public CharacterAttributes SafeDefaults()
    {
        var d = Defaults;
        if (attack <= 0f) attack = d.attack;
        if (attackSpeedMul <= 0f) attackSpeedMul = d.attackSpeedMul;
        if (maxHp <= 0f) maxHp = d.maxHp;
        if (moveSpeed <= 0f) moveSpeed = d.moveSpeed;
        if (critDamageMul <= 0f) critDamageMul = d.critDamageMul;
        return this;
    }
}
