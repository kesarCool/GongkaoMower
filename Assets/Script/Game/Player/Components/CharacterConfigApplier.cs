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

        // 1. 属性（通过 attributes 干净写入，不再用反射）
        var attr = def.attributes.ApplyMinimums();
        Debug.Log($"[CharacterConfigApplier] 属性讀取: raw=({def.attributes.moveSpeed:F1}) safe=({attr.moveSpeed:F1})");
        _playerHealth.SetMaxHp(attr.maxHp);
        _playerHealth.SetDefense(attr.defense);
        _playerHealth.ResetToFull();
        float prevSpeed = _playerController.moveSpeed;
        _playerController.moveSpeed = attr.moveSpeed;
        Debug.Log($"[CharacterConfigApplier] moveSpeed: {prevSpeed:F1} → {_playerController.moveSpeed:F1}");
        _playerSkills.attackMultiplier = attr.attack;
        _playerSkills.critRate = attr.critRate;
        _playerSkills.critDamageMul = attr.critDamageMul;
        _playerSkills.pierceCount = attr.pierceCount;
        _playerSkills.pierceRate = attr.pierceRate;

        // 2. 技能
        ApplySkills(def);

        // 3. 外观
        ApplyAppearance(def);
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

        // 查找任意 AutoProjectile 变体技能（不限定 SkillId）
        var ids = new System.Collections.Generic.List<SkillId>(4);
        _playerSkills.GetEquippedSkillIdsOrdered(ids);
        foreach (var id in ids)
        {
            var autoSkill = _playerSkills.GetEquippedSkill<SkillAutoProjectile>(id);
            if (autoSkill != null)
            {
                autoSkill.bulletPrefab = def.defaultWeapon.bulletOverridePrefab;
                Debug.Log($"[CharacterConfigApplier] AutoProjectile({id}) 子弹已覆盖为: {def.defaultWeapon.bulletOverridePrefab.name}");
                return;
            }
        }
    }
}
