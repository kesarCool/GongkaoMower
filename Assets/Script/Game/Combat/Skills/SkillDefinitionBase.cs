using System.Text;
using UnityEngine;

/// <summary>
/// 技能定义（数据层）：用于“肉鸽卡池/升级表”驱动运行时技能实例（ISkill）。
/// </summary>
public abstract class SkillDefinitionBase : ScriptableObject
{
    public SkillId id;

    /// <summary>
    /// 技能家族 ID。同家族的技能在卡池中互斥（如 AutoProjectile 各变体）。
    /// 默认返回自身 id，表示独立技能无家族。
    /// </summary>
    public virtual SkillId SkillFamily => id;

    [Min(1)]
    public int maxLevel = 5;

    [Tooltip("羁绊被动技能 ID。拥有该被动后，本技能才能升到 maxLevel。留空 = 无羁绊限制。")]
    public SkillId bondedPassiveId = SkillId.None;

    [Header("满级变体")]
    [Tooltip("满级后替换的预制体（弹丸/手雷/导弹），留空=保持原样。")]
    public GameObject maxLevelPrefab;

    [Header("UI")]
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("选卡展示（每级一行）")]
    [Tooltip("第 0 条 = Lv.1，第 1 条 = Lv.2 … 长度建议等于 maxLevel。留空则按各技能数值表自动生成。")]
    [TextArea(2, 3)]
    public string[] levelDescriptions;

    public int ClampLevel(int level) => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));

    /// <summary>某一级的单行说明（优先用手写 levelDescriptions，否则走数值表生成）。</summary>
    public string GetLevelDescriptionLine(int level)
    {
        level = ClampLevel(level);
        if(level == 1 && !string.IsNullOrWhiteSpace(description))
        {
            return description;
        }
                
        if (levelDescriptions != null && level <= levelDescriptions.Length)
        {

           
            string custom = levelDescriptions[level - 1];
            if (!string.IsNullOrWhiteSpace(custom))
                return custom.Trim();
        }

        return GenerateLevelDescription(level);
    }

    /// <summary>选卡描述区：列出 Lv.1～maxLevel，可高亮即将升到的等级。</summary>
    public string FormatAllLevelDescriptions(int highlightLevel = -1)
    {
        int max = Mathf.Max(1, maxLevel);
        var sb = new StringBuilder(max * 24);
        string line = GetLevelDescriptionLine(highlightLevel);
        sb.AppendLine($"Lv.{highlightLevel} {line}");
        // for (int lv = 1; lv <= max; lv++)
        // {
        //     string line = GetLevelDescriptionLine(lv);
        //     if (lv == highlightLevel)
        //         sb.AppendLine($"▶ Lv.{lv} {line}");
        //     else
        //         sb.AppendLine($"Lv.{lv} {line}");
        // }

        return sb.ToString().TrimEnd();
    }

    /// <summary>无手写文案时，根据 per-level 数值数组生成简短说明（子类实现）。</summary>
    protected abstract string GenerateLevelDescription(int level);

    /// <summary>由 SkillDef + Inspector 回退创建运行时技能，并套用 Lv.1 数值。</summary>
    public ISkill CreateRuntimeSkill(SkillRuntimeBindings bindings)
    {
        ISkill skill = CreateRuntimeSkillInternal(bindings);
        if (skill != null)
        {
            ApplyStatsToSkill(skill, ClampLevel(skill.Level));
            (skill as SkillBase)?.OnAfterStatsApplied();
        }
        return skill;
    }

    protected abstract ISkill CreateRuntimeSkillInternal(SkillRuntimeBindings bindings);

    /// <summary>按等级把 Def 数值写入已有技能实例（升级 / 创建后调用）。</summary>
    public abstract void ApplyStatsToSkill(ISkill skill, int level);
}

