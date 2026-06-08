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

    [Header("角色属性")]
    [Tooltip("攻击/血量/速度/暴击/穿透等，由 CharacterConfigApplier 写入运行时组件。")]
    public CharacterAttributes attributes;

    [Header("外观（可选）")]
    [Tooltip("角色身体精灵（替换 Body 子物体的 SpriteRenderer.sprite）")]
    public Sprite bodySprite;

    [Tooltip("初始武器（null = 徒手无武器视觉）")]
    public WeaponDefinition defaultWeapon;

    [Header("绑定技能")]
    [Tooltip("角色额外携带的技能（开局即拥有，不含 startingSkill 自身）")]
    public SkillId[] boundSkills;

    [Header("升级")]
    [Tooltip("英雄升级曲线配置（等级属性倍率 + 金币消耗公式）。未拖入则无升级加成。")]
    public HeroUpgradeData upgradeData;

    [Header("解锁")]
    [Tooltip("false = 默认可用；true = 需达成条件解锁")]
    public bool locked;
    [Tooltip("通关此关卡后解锁（0 = 不通过关卡解锁）。")]
    public int unlockLevelId;
    [Tooltip("收集到此数量的碎片后解锁（0 = 不通过碎片解锁）。")]
    public int unlockFragmentCount;
}
