using UnityEngine;

/// <summary>
/// 技能运行时上下文（由 PlayerSkills 构造并传给各技能）
/// </summary>
public struct SkillContext
{
    public Transform player;
    public string enemyTag;
}
