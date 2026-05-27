using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 卡组：管理抽卡逻辑、过滤、加权随机
/// </summary>
[CreateAssetMenu(menuName = "Game/Roguelike/Card Deck", fileName = "CardDeck")]
public class CardDeck : ScriptableObject
{
    [Tooltip("技能目录（查询所有技能定义）")]
    public SkillCatalog skillCatalog;

    [Tooltip("卡池解锁进度配置")]
    public CardPoolProgression progression;

    [Header("卡牌模板")]
    [Tooltip("新技能卡模板（绿色）")]
    public RoguelikeCardTemplate newSkillTemplate;

    [Tooltip("升级技能卡模板（黄色）")]
    public RoguelikeCardTemplate upgradeTemplate;

    [Tooltip("突破卡模板（红色）- 预留后期使用")]
    public RoguelikeCardTemplate breakthroughTemplate;

    [Header("抽卡配置")]
    [Tooltip("每次抽几张卡供玩家选择")]
    public int drawCount = 3;

    [Tooltip("升级卡权重加成（已有技能更容易抽到升级）")]
    public float upgradeWeightBonus = 1.5f;

    [Tooltip("新技能在满5个后是否还出现（建议false）")]
    public bool allowNewSkillWhenFull = false;

    /// <summary>
    /// 抽卡结果数据（包含技能定义和应使用的模板）
    /// </summary>
    public class DrawResult
    {
        public SkillId skillId;
        public SkillDefinitionBase skillDef;
        public RoguelikeCardTemplate template;
        public int currentLevel;      // 0=未拥有, 1~5=当前等级
        public int targetLevel;       // 升级后等级
        public float weight;          // 抽选权重（调试用）
    }

    /// <summary>
    /// 根据当前状态抽卡
    /// </summary>
    /// <param name="currentLevel">当前关卡进度</param>
    /// <param name="playerSkills">玩家技能管理器</param>
    /// <param name="excludeSkills">本次抽卡要排除的技能（刷新用）</param>
    /// <returns>抽卡结果列表</returns>
    public List<DrawResult> Draw(int currentLevel, PlayerSkills playerSkills, List<SkillId> excludeSkills = null)
    {
        var candidates = new List<DrawResult>();
        excludeSkills ??= new List<SkillId>();

        foreach (var def in skillCatalog.All())
        {
            if (def == null) continue;

            var id = def.id;

            // 1. 硬性解锁检查
            if (!progression.IsUnlocked(id, currentLevel)) continue;

            // 2. 检查排除列表（刷新用）
            if (excludeSkills.Contains(id)) continue;

            // 3. 判断是否已拥有
            bool hasSkill = playerSkills.HasSkill(id);
            int currentLv = hasSkill ? playerSkills.GetSkillLevel(id) : 0;
            int maxLv = Mathf.Max(1, def.maxLevel);

            // 已满级不再入选卡池
            if (hasSkill && currentLv >= maxLv) continue;

            // 4. 判断是否还有空槽位
            if (!hasSkill && !playerSkills.HasEmptySlot && !allowNewSkillWhenFull)
                continue;

            // 5. 确定模板类型
            RoguelikeCardTemplate template;
            float weight = progression.GetWeight(id);
            int targetLv = currentLv + 1;

            if (!hasSkill)
            {
                template = newSkillTemplate;
            }
            else if (targetLv >= maxLv)
            {
                template = breakthroughTemplate; // 等级4→5，满级突破卡
            }
            else
            {
                template = upgradeTemplate;
                weight *= upgradeWeightBonus;
            }

            candidates.Add(new DrawResult
            {
                skillId = id,
                skillDef = def,
                template = template,
                currentLevel = currentLv,
                targetLevel = targetLv,
                weight = weight
            });
        }

        // 没有候选卡
        if (candidates.Count == 0) return new List<DrawResult>();

        // 加权随机抽取
        return WeightedRandomPick(candidates, Mathf.Min(drawCount, candidates.Count));
    }

    /// <summary>
    /// 加权随机抽取（不放回）
    /// </summary>
    private List<DrawResult> WeightedRandomPick(List<DrawResult> pool, int count)
    {
        var result = new List<DrawResult>(count);
        var temp = new List<DrawResult>(pool);

        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            float totalWeight = 0f;
            foreach (var c in temp) totalWeight += c.weight;

            float roll = Random.value * totalWeight;
            float accum = 0f;

            DrawResult picked = null;
            for (int j = 0; j < temp.Count; j++)
            {
                accum += temp[j].weight;
                if (roll <= accum)
                {
                    picked = temp[j];
                    temp.RemoveAt(j);
                    break;
                }
            }

            // 兜底
            if (picked == null && temp.Count > 0)
            {
                picked = temp[0];
                temp.RemoveAt(0);
            }

            if (picked != null) result.Add(picked);
        }

        return result;
    }
}
