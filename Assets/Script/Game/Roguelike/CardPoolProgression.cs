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

    private const string GlobalUnlockKeyPrefix = "global_skill_unlock_";

    /// <summary>
    /// 检查技能是否已解锁（全局永久：一旦在某关解锁，回退到低关卡仍可用）。
    /// </summary>
    public bool IsUnlocked(SkillId skill, int currentLevel)
    {
        Initialize();
        if (!_map.TryGetValue(skill, out var cond))
            return true; // 未配置默认解锁

        int skillInt = (int)skill;
        string persistentKey = GlobalUnlockKeyPrefix + skillInt;

        // 当前关卡达线 → 记录为永久解锁
        if (currentLevel >= cond.unlockAtLevel)
        {
            int prev = PlayerPrefs.GetInt(persistentKey, 0);
            if (currentLevel > prev)
            {
                PlayerPrefs.SetInt(persistentKey, currentLevel);
                PlayerPrefs.Save();
            }
            return true;
        }

        // 历史解锁：检查是否在高关卡号解锁过
        int maxLv = PlayerPrefs.GetInt(persistentKey, 0);
        return maxLv >= cond.unlockAtLevel;
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
