using UnityEngine;

/// <summary>
/// 英雄升级数据（ScriptableObject，每个英雄一个 .asset）。
/// 属性曲线：Lv.1 = 1.0×，Lv.max = 配置值，中间线性插值。
/// 金币消耗：cost = baseGoldCost + (targetLevel - 2) × goldGrowth。
/// </summary>
[CreateAssetMenu(menuName = "Game/Hero Upgrade Data", fileName = "HeroUpgradeData")]
public class HeroUpgradeData : ScriptableObject
{
    [Tooltip("对应 CharacterDefinition.characterId。")]
    public string characterId;

    [Tooltip("最大等级（≥2）。")]
    [Range(2, 30)]
    public int maxLevel = 20;

    [Header("金币消耗")]
    [Tooltip("升到 Lv.2 的消耗。升 N→N+1 的消耗 = baseGoldCost + (N-1) × goldGrowth。")]
    public int baseGoldCost = 200;
    [Tooltip("每升一级的消耗增量。")]
    public int goldGrowth = 200;

    [Header("升阶")]
    [Tooltip("升 Rare 所需碎片数。")]
    public int rareFragmentCost = 20;
    [Tooltip("升 Legend 所需碎片数。")]
    public int legendFragmentCost = 50;
    [Tooltip("升 Rare 的最低等级。")]
    [Range(2, 30)]
    public int rareRequiredLevel = 10;
    [Tooltip("升 Legend 的最低等级。")]
    [Range(2, 30)]
    public int legendRequiredLevel = 20;

    [Header("Rare 英雄特质")]
    [Tooltip("特质类型。")]
    public HeroTraitType rareTrait;
    [Tooltip("特质参数（含义取决于类型，见 HeroTraitType 注释）。")]
    public float[] rareTraitParams = new float[0];
    [Tooltip("升阶面板展示的特质描述。")]
    [TextArea(2, 3)]
    public string rareTraitDescription;

    [Header("Legend 奥义突破")]
    [Tooltip("升阶面板展示的突破描述。")]
    [TextArea(2, 3)]
    public string legendBreakthroughDescription;

    [Header("Lv.Max 属性倍率（Lv.1 = 1.0）")]
    [Tooltip("攻击力终值倍率。")]
    public float attackMulAtMax = 3f;
    [Tooltip("血量终值倍率。")]
    public float maxHpMulAtMax = 3f;
    [Tooltip("防御终值倍率。")]
    public float defenseMulAtMax = 2f;
    [Tooltip("移速终值倍率。")]
    public float moveSpeedMulAtMax = 1.3f;
    [Tooltip("攻击范围终值倍率。")]
    public float attackRangeMulAtMax = 1.5f;
    [Tooltip("暴击率终值增量（绝对加值，如 0.1 = +10%）。")]
    public float critRateAddAtMax;
    [Tooltip("暴击倍率终值。")]
    public float critDmgMulAtMax = 2.5f;
    [Tooltip("穿透率终值增量（绝对加值）。")]
    public float pierceRateAddAtMax;
    [Tooltip("穿透数终值增量（取整）。")]
    public int pierceCountAddAtMax;

    /// <summary>倍率类属性：Lerp(1, atMax, level/max)。</summary>
    public float EvaluateMul(float atMax, int level)
    {
        if (maxLevel <= 1 || level <= 1) return 1f;
        float t = (level - 1f) / (maxLevel - 1f);
        return Mathf.Lerp(1f, atMax, Mathf.Clamp01(t));
    }

    /// <summary>增量类属性（暴击率/穿透等）：Lerp(0, atMax, level/max)。</summary>
    public float EvaluateAdd(float atMax, int level)
    {
        if (maxLevel <= 1 || level <= 1) return 0f;
        float t = (level - 1f) / (maxLevel - 1f);
        return Mathf.Lerp(0f, atMax, Mathf.Clamp01(t));
    }

    /// <summary>升级到 targetLevel 所需金币。</summary>
    public int GetCostForLevel(int targetLevel)
    {
        if (targetLevel <= 1) return 0;
        return baseGoldCost + (targetLevel - 2) * goldGrowth;
    }
}
