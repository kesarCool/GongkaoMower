using UnityEngine;

/// <summary>
/// 角色定义：开局自带一个初始技能，可配置角色特性
/// </summary>
[CreateAssetMenu(menuName = "Game/Character", fileName = "CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    [Tooltip("角色唯一标识")]
    public string characterId;

    [Tooltip("显示名称")]
    public string displayName;

    [Tooltip("角色头像")]
    public Sprite portrait;

    [Header("初始技能")]
    [Tooltip("角色开局自带的技能（等级=1）")]
    public SkillId startingSkill;

    [Header("角色特性（可选）")]
    [Tooltip("血量加成")]
    public float maxHpBonus;

    [Tooltip("移速加成")]
    public float moveSpeedBonus;
}
