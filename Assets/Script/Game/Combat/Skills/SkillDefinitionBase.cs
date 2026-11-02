using System.Text;
using System.Collections.Generic;
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
    [Tooltip("技能机制和特色描述；伤害、数量、冷却等数值由实际配置自动生成，请勿在此手填。")]
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("每级机制与特色文案")]
    [Tooltip("第 0 条 = Lv.1，第 1 条 = Lv.2。仅填机制或特色，如子弹分裂、环绕爆发；不要填伤害、射速、数量等数值。数值始终从配置生成，填写此处不会覆盖。")]
    [TextArea(2, 3)]
    public string[] levelDescriptions;

    public int ClampLevel(int level) => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));

    /// <summary>实际等级数值 + 该等级的机制说明；手填文案不再覆盖数值。</summary>
    public string GetLevelDescriptionLine(int level)
    {
        level = ClampLevel(level);
        return AppendMechanics(FormatStats(GetDescriptionStats(level)), level);
    }

    /// <summary>仅列出实际变化的数值；无数值变化时显示目标等级数值。机制文案独立追加。</summary>
    public string GetLevelUpgradeDescription(int fromLevel, int toLevel)
    {
        fromLevel = ClampLevel(fromLevel);
        toLevel = ClampLevel(toLevel);
        var previous = new Dictionary<string, SkillDescriptionStat>();
        foreach (var stat in GetDescriptionStats(fromLevel)) previous[stat.Label] = stat;
        var target = GetDescriptionStats(toLevel);
        var lines = new List<string>();
        foreach (var stat in target)
        {
            string line = previous.TryGetValue(stat.Label, out var before) ? stat.FormatUpgrade(before) : stat.FormatSnapshot();
            if (!string.IsNullOrEmpty(line)) lines.Add(line);
        }
        return AppendMechanics(lines.Count > 0 ? string.Join("\n", lines) : FormatStats(target), toLevel);
    }

    private static string FormatStats(SkillDescriptionStat[] stats)
    {
        var lines = new string[stats.Length];
        for (int i = 0; i < stats.Length; i++) lines[i] = stats[i].FormatSnapshot();
        return string.Join("\n", lines);
    }

    private string AppendMechanics(string stats, int level)
    {
        string mechanics = levelDescriptions != null && level <= levelDescriptions.Length ? levelDescriptions[level - 1] : null;
        if (string.IsNullOrWhiteSpace(mechanics) && level == 1) mechanics = description;
        return string.IsNullOrWhiteSpace(mechanics) ? stats : stats + "\n" + mechanics.Trim();
    }

    /// <summary>选卡描述区：列出 Lv.1～maxLevel，可高亮即将升到的等级。</summary>
    public string FormatAllLevelDescriptions(int highlightLevel = -1)
    {
        var sb = new StringBuilder();
        highlightLevel = ClampLevel(highlightLevel);
        string line = GetLevelDescriptionLine(highlightLevel);
        sb.AppendLine($"Lv.{highlightLevel} {line}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>显示数值与运行时技能使用同一组配置字段 / 等级访问器。</summary>
    protected abstract SkillDescriptionStat[] GetDescriptionStats(int level);

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

