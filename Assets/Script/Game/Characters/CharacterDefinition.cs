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

    [Header("外观（可选）")]
    [Tooltip("角色身体精灵（替换 Body 子物体的 SpriteRenderer.sprite）")]
    public Sprite bodySprite;

    [Tooltip("初始武器（null = 徒手无武器视觉）")]
    public WeaponDefinition defaultWeapon;

    [Header("绑定技能")]
    [Tooltip("角色额外携带的技能（开局即拥有，不含 startingSkill 自身）")]
    public SkillId[] boundSkills;

    [Header("战斗属性（展示用）")]
    [Tooltip("基础攻击力（显示在选角面板）")]
    public float baseAttack = 10f;

    [Tooltip("基础血量（显示在选角面板）")]
    public float baseHp = 100f;

    [Header("角色特性（可选）")]
    [Tooltip("血量加成")]
    public float maxHpBonus;

    [Tooltip("移速加成")]
    public float moveSpeedBonus;

    [Header("解锁")]
    [Tooltip("false = 默认可用；true = 需达成条件解锁")]
    public bool locked;
}
