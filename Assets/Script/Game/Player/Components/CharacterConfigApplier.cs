using UnityEngine;

/// <summary>
/// 角色配置引导：Scene Start 时读取 <see cref="SelectedCharacterContext"/>，
/// 将 <see cref="CharacterDefinition"/> 应用到 PlayerHealth / PlayerController / PlayerSkills / CharacterAppearance。
/// 挂载在 Player GameObject 上，CharacterCatalog 由 Inspector 拖拽赋值。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerSkills))]
[RequireComponent(typeof(CharacterAppearance))]
public class CharacterConfigApplier : MonoBehaviour
{
    [Tooltip("角色目录（ScriptableObject，拖拽赋值）")]
    public CharacterCatalog characterCatalog;

    private PlayerHealth _playerHealth;
    private PlayerController _playerController;
    private PlayerSkills _playerSkills;
    private CharacterAppearance _appearance;

    private void Awake()
    {
        _playerHealth = GetComponent<PlayerHealth>();
        _playerController = GetComponent<PlayerController>();
        _playerSkills = GetComponent<PlayerSkills>();
        _appearance = GetComponent<CharacterAppearance>();
    }

    private void Start()
    {
        ApplySelectedCharacter();
    }

    public void ApplySelectedCharacter()
    {
        if (characterCatalog == null)
        {
            Debug.LogWarning("[CharacterConfigApplier] characterCatalog 未赋值，跳过角色应用。");
            return;
        }

        string charId = SelectedCharacterContext.GetEffective(characterCatalog);
        if (string.IsNullOrEmpty(charId))
        {
            Debug.LogWarning("[CharacterConfigApplier] 未选择角色且 Catalog 无默认角色。");
            return;
        }

        CharacterDefinition def = characterCatalog.Get(charId);
        if (def == null)
        {
            Debug.LogWarning($"[CharacterConfigApplier] Catalog 中找不到角色 characterId={charId}");
            return;
        }

        Debug.Log($"[CharacterConfigApplier] 应用角色: {def.displayName} (id={def.characterId})");

        // 1. 基础属性（加法叠加到 Inspector 默认值上）
        TryAddMaxHpBonus(def.maxHpBonus);
        _playerHealth.ResetToFull();
        _playerController.moveSpeed += def.moveSpeedBonus;

        // 2. 技能
        ApplySkills(def);

        // 3. 外观
        ApplyAppearance(def);
    }

    private void TryAddMaxHpBonus(float bonus)
    {
        if (bonus == 0) return;

        var type = typeof(PlayerHealth);
        var field = type.GetField("maxHp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (field != null)
        {
            object currentValue = field.GetValue(_playerHealth);
            if (currentValue is int currentInt)
            {
                field.SetValue(_playerHealth, currentInt + bonus);
                return;
            }
            if (currentValue is float currentFloat)
            {
                field.SetValue(_playerHealth, currentFloat + bonus);
                return;
            }
        }

        var property = type.GetProperty("maxHp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (property != null && property.CanRead && property.CanWrite)
        {
            object currentValue = property.GetValue(_playerHealth);
            if (currentValue is int currentInt)
            {
                property.SetValue(_playerHealth, currentInt + bonus);
                return;
            }
            if (currentValue is float currentFloat)
            {
                property.SetValue(_playerHealth, currentFloat + bonus);
                return;
            }
        }

        Debug.LogWarning("[CharacterConfigApplier] 无法访问 PlayerHealth.maxHp，未应用 maxHp 奖励。");
    }

    private void ApplySkills(CharacterDefinition def)
    {
        if (_playerSkills == null) return;

        // 确定有效初始技能：武器优先
        SkillId effectiveSkill = def.startingSkill;
        if (def.defaultWeapon != null && def.defaultWeapon.weaponSkillId != SkillId.None)
            effectiveSkill = def.defaultWeapon.weaponSkillId;

        // 重建技能列表（PlayerSkills.Awake 已构建过一次，这里以 effectiveSkill 重建）
        _playerSkills.startingSkill = effectiveSkill;
        _playerSkills.RebuildFromStartingSkill();

        // 绑定技能
        if (def.boundSkills != null)
        {
            for (int i = 0; i < def.boundSkills.Length; i++)
            {
                SkillId sid = def.boundSkills[i];
                if (sid != SkillId.None)
                    _playerSkills.TryAddSkill(sid);
            }
        }

        // 武器子弹覆盖
        OverrideBulletPrefab(def);
    }

    private void ApplyAppearance(CharacterDefinition def)
    {
        if (_appearance == null) return;

        _appearance.ApplySkin(def);

        if (def.defaultWeapon != null)
            _appearance.ApplyWeapon(def.defaultWeapon);
    }

    private void OverrideBulletPrefab(CharacterDefinition def)
    {
        if (def.defaultWeapon == null || def.defaultWeapon.bulletOverridePrefab == null)
            return;

        var autoSkill = _playerSkills.GetEquippedSkill<SkillAutoProjectile>(SkillId.AutoProjectile);
        if (autoSkill != null)
        {
            autoSkill.bulletPrefab = def.defaultWeapon.bulletOverridePrefab;
            Debug.Log($"[CharacterConfigApplier] AutoProjectile 子弹已覆盖为: {def.defaultWeapon.bulletOverridePrefab.name}");
        }
    }
}
