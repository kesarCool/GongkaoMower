using System;
using UnityEngine;

/// <summary>
/// 角色属性，配置在 CharacterDefinition 中，由 CharacterConfigApplier 写入运行时组件。
/// </summary>
[Serializable]
public struct CharacterAttributes
{
    [Header("攻击")]
    [Tooltip("攻击力，作为技能伤害系数（1.0 = 技能表原始伤害）。不可为 0。")]
    public float attack;

    [Tooltip("攻速倍率（1.0 = 不变，0.8 = 快 20%）。不可为 0。")]
    public float attackSpeedMul;

    [Header("防御")]
    [Tooltip("最大血量。不可为 0。")]
    public float maxHp;

    [Tooltip("减伤值，公式：最终伤害 = 原始伤害 × 100 / (100 + defense)。0 = 无减伤。")]
    public float defense;

    [Header("机动")]
    [Tooltip("移动速度。不可为 0。")]
    public float moveSpeed;

    [Header("特殊")]
    [Tooltip("攻击范围倍率（1.0=不变，1.3=范围+30%）。")]
    public float attackRangeMul;

    [Tooltip("暴击率 (0~1，如 0.15 = 15%)。0 = 不暴击。")]
    [Range(0f, 1f)]
    public float critRate;

    [Tooltip("暴击倍率。不可为 0。")]
    public float critDamageMul;

    [Tooltip("穿透率 (0~1，如 0.2 = 20%)。0 = 不穿透。")]
    [Range(0f, 1f)]
    public float pierceRate;

    [Tooltip("穿透数。0 = 不穿透。")]
    public int pierceCount;

    [Header("破防")]
    [Tooltip("破防率 (0~1，如 0.1 = 10%)。触发时无视敌人防御。")]
    [Range(0f, 1f)]
    public float penRate;

    [Tooltip("破防比例 (0~1，1=完全无视防御)。")]
    [Range(0f, 1f)]
    public float penPercent;

    /// <summary>
    /// 不可为零字段的保底值，确保角色基础可玩。
    /// defense/critRate/pierceRate/pierceCount/penRate/penPercent 天生可为 0，不在此列。
    /// </summary>
    public static CharacterAttributes Minimums => new CharacterAttributes
    {
        attack = 1f,
        attackSpeedMul = 1f,
        maxHp = 100f,
        defense = 0f,
        moveSpeed = 4f,
        critRate = 0f,
        critDamageMul = 2f,
        pierceRate = 0f,
        pierceCount = 0,
        attackRangeMul = 1f,
        penRate = 0f,
        penPercent = 1f,
    };

    /// <summary>将不可为零的字段补到保底值。defense/暴击/穿透原值保留。</summary>
    public CharacterAttributes ApplyMinimums()
    {
        var m = Minimums;
        if (attack <= 0f) attack = m.attack;
        if (attackSpeedMul <= 0f) attackSpeedMul = m.attackSpeedMul;
        if (maxHp <= 0f) maxHp = m.maxHp;
        if (moveSpeed <= 0f) moveSpeed = m.moveSpeed;
        if (critDamageMul <= 0f) critDamageMul = m.critDamageMul;
        if (attackRangeMul <= 0f) attackRangeMul = m.attackRangeMul;
        return this;
    }
}
