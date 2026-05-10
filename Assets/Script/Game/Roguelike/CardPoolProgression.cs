using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 卡池进度解锁配置：控制技能在第几关解锁（硬性解锁）
/// </summary>
[CreateAssetMenu(menuName = "Game/Roguelike/Card Pool Progression", fileName = "CardPoolProgression")]
public class CardPoolProgression : ScriptableObject
{
    [System.Serializable]
    public class UnlockCondition
    {
        [Tooltip("技能ID")]
        public SkillId skill;

        [Tooltip("第几关解锁（1=第一关就可用）")]
        public int unlockAtLevel = 1;

        [Tooltip("解锁后基础权重（影响抽卡概率）")]
        public float baseWeight = 10f;
    }

    [Tooltip("所有技能的解锁条件列表")]
    public UnlockCondition[] unlockConditions;

    private Dictionary<SkillId, UnlockCondition> _map;

    public void Initialize()
    {
        if (_map != null) return;
        _map = new Dictionary<SkillId, UnlockCondition>();
        foreach (var c in unlockConditions)
        {
            if (c == null) continue;
            _map[c.skill] = c;
        }
    }

    /// <summary>
    /// 检查技能是否已解锁
    /// </summary>
    public bool IsUnlocked(SkillId skill, int currentLevel)
    {
        Initialize();
        if (!_map.TryGetValue(skill, out var cond))
            return true; // 未配置默认解锁
        return currentLevel >= cond.unlockAtLevel;
    }

    /// <summary>
    /// 获取技能解锁后的基础权重
    /// </summary>
    public float GetWeight(SkillId skill)
    {
        Initialize();
        if (!_map.TryGetValue(skill, out var cond))
            return 10f;
        return cond.baseWeight;
    }
}
