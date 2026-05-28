using UnityEngine;

/// <summary>
/// 武器定义：角色可装备的武器配置（渲染参数/攻击摆动/技能覆盖）。
/// 与 <see cref="CharacterDefinition"/> 解耦，可独立用于背包/养成系统。
/// </summary>
[CreateAssetMenu(menuName = "Game/Weapon", fileName = "WeaponDefinition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("基础")]
    [Tooltip("武器唯一标识")]
    public string weaponId;

    [Tooltip("显示名称")]
    public string displayName;

    [Header("渲染")]
    [Tooltip("武器贴图")]
    public Sprite sprite;

    [Tooltip("武器在玩家本地坐标下的静止位置")]
    public Vector3 localPosition = new Vector3(0.35f, 0f, 0f);

    [Tooltip("武器静止旋转")]
    public Vector3 localRotation = Vector3.zero;

    [Tooltip("武器缩放")]
    public Vector3 localScale = Vector3.one;

    [Tooltip("武器 SortingOrder 相对于 Body 的增量")]
    public int sortingOrderOffset = 1;

    [Header("攻击摆动（无动画补偿）")]
    [Tooltip("攻击时武器 Z 轴旋转角度（度）")]
    public float attackSwingAngle = 14f;

    [Tooltip("攻击时武器前后位移")]
    public Vector3 attackBobOffset = new Vector3(0.06f, 0.02f, 0f);

    [Tooltip("攻击摆动时长（秒）")]
    public float attackSwingDuration = 0.1f;

    [Tooltip("攻击后回弹时长（秒）")]
    public float attackRecoverDuration = 0.08f;

    [Header("技能覆盖（可选）")]
    [Tooltip("若设置，装备此武器时替换角色的 startingSkill")]
    public SkillId weaponSkillId;

    [Tooltip("若设置，覆盖 AutoProjectile 技能的子弹 Prefab（皮肤子弹/刀气波等）")]
    public GameObject bulletOverridePrefab;
}
