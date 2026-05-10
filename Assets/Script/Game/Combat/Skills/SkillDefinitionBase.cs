using UnityEngine;

/// <summary>
/// 技能定义（数据层）：用于“肉鸽卡池/升级表”驱动运行时技能实例（ISkill）。
/// </summary>
public abstract class SkillDefinitionBase : ScriptableObject
{
    public SkillId id;

    [Min(1)]
    public int maxLevel = 5;

    [Header("UI")]
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;

    public int ClampLevel(int level) => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
}

