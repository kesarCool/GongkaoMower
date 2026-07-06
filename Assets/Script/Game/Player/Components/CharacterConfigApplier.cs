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

        GameLog.Info($"[CharacterConfigApplier] 应用角色: {def.displayName} (id={def.characterId})");

        // 1. 属性（通过 attributes 干净写入，不再用反射）
        var attr = def.attributes.ApplyMinimums();

        // 叠加升级倍率
        if (def.upgradeData != null)
        {
            PlayerProfileService.Instance.LoadOrCreate();
            var s = PlayerProfileService.Instance;
            int lv = s.GetHeroLevel(def.characterId);
            attr.attack         *= s.GetUpgradeMul(def.characterId, def.upgradeData, "attack");
            attr.maxHp          *= s.GetUpgradeMul(def.characterId, def.upgradeData, "maxHp");
            attr.defense        *= s.GetUpgradeMul(def.characterId, def.upgradeData, "defense");
            attr.moveSpeed      *= s.GetUpgradeMul(def.characterId, def.upgradeData, "moveSpeed");
            attr.attackRangeMul *= s.GetUpgradeMul(def.characterId, def.upgradeData, "attackRange");
            attr.critRate       += s.GetUpgradeMul(def.characterId, def.upgradeData, "critRate");
            attr.critDamageMul  *= s.GetUpgradeMul(def.characterId, def.upgradeData, "critDmg");
            attr.pierceRate     += s.GetUpgradeMul(def.characterId, def.upgradeData, "pierceRate");
            attr.pierceCount    += Mathf.RoundToInt(s.GetUpgradeMul(def.characterId, def.upgradeData, "pierceCount"));
            attr.penRate        += s.GetUpgradeMul(def.characterId, def.upgradeData, "penRate");
            GameLog.Info($"[CharacterConfigApplier] 升级倍率已应用：{def.characterId} Lv.{lv}");
        }

        GameLog.Info($"[CharacterConfigApplier] 属性讀取: raw=({def.attributes.moveSpeed:F1}) safe=({attr.moveSpeed:F1})");
        _playerHealth.SetMaxHp(attr.maxHp);
        _playerHealth.SetDefense(attr.defense);
        _playerHealth.ResetToFull();
        float prevSpeed = _playerController.moveSpeed;
        _playerController.moveSpeed = attr.moveSpeed;
        GameLog.Info($"[CharacterConfigApplier] moveSpeed: {prevSpeed:F1} → {_playerController.moveSpeed:F1}");
        _playerSkills.attackMultiplier = attr.attack;
        _playerSkills.critRate = attr.critRate;
        _playerSkills.critDamageMul = attr.critDamageMul;
        _playerSkills.pierceCount = attr.pierceCount;
        _playerSkills.pierceRate = attr.pierceRate;
        _playerSkills.attackRangeMul = attr.attackRangeMul;
        _playerSkills.penRate = attr.penRate;
        _playerSkills.penPercent = attr.penPercent;

        // 2. Rare 特质
        ApplyRareTrait(def);

        // 3. 技能
        ApplySkills(def);

        // 4. Legend 突破（必须在技能创建后调用）
        ApplyLegendBreakthrough(def);

        // 5. 外观
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

    private void ApplyRareTrait(CharacterDefinition def)
    {
        if (def?.upgradeData == null) return;
        int stage = PlayerProfileService.Instance.GetHeroStage(def.characterId);
        if (stage < 1) return; // 未到 Rare

        var traitType = def.upgradeData.rareTrait;
        if (traitType == HeroTraitType.None) return;

        // 移除旧特质（如果有）
        var existing = GetComponents<TraitBehaviour>();
        foreach (var t in existing) Destroy(t);

        // 创建新特质
        TraitBehaviour trait = traitType switch
        {
            HeroTraitType.KillStreak      => gameObject.AddComponent<TraitKillStreak>(),
            HeroTraitType.DamageAura      => gameObject.AddComponent<TraitDamageAura>(),
            HeroTraitType.ReactiveShield  => gameObject.AddComponent<TraitReactiveShield>(),
            HeroTraitType.Berserk         => gameObject.AddComponent<TraitBerserk>(),
            HeroTraitType.VampiricHeal    => gameObject.AddComponent<TraitVampiricHeal>(),
            HeroTraitType.TalismanOrbit  => gameObject.AddComponent<TraitTalismanOrbit>(),
            HeroTraitType.NanoRepair    => gameObject.AddComponent<TraitNanoRepair>(),
            _ => null,
        };

        if (trait != null)
        {
            trait.Initialize(def.upgradeData.rareTraitParams);
            GameLog.Info($"[CharacterConfigApplier] Rare 特质已应用：{traitType}（stage={stage}）");
        }
    }

    private void ApplyLegendBreakthrough(CharacterDefinition def)
    {
        if (def?.upgradeData == null) return;
        int stage = PlayerProfileService.Instance.GetHeroStage(def.characterId);
        if (stage < 2)
        {
            // GameLog.Info($"[CharacterConfigApplier] Legend 突破跳过：stage={stage}，需 stage≥2");
            return;
        }

        if (_playerSkills == null) { Debug.LogWarning("[CharacterConfigApplier] _playerSkills 为空，无法注入突破。"); return; }

        var ids = new System.Collections.Generic.List<SkillId>(4);
        _playerSkills.GetEquippedSkillIdsOrdered(ids);

        int applied = 0;
        foreach (var id in ids)
        {
            if (id == SkillId.None) continue;
            var skill = _playerSkills.GetEquippedSkill<SkillBase>(id);
            if (skill != null)
            {
                skill.ApplyLegendBreakthrough(stage);
                applied++;
                // GameLog.Info($"[CharacterConfigApplier] Legend 突破已注入：skill={id}（{skill.GetType().Name}）");
            }
        }

        if (applied == 0)
            Debug.LogWarning($"[CharacterConfigApplier] Legend 突破未应用任何技能！stage={stage}, skillCount={ids.Count}");
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
                GameLog.Info($"[CharacterConfigApplier] AutoProjectile({id}) 子弹已覆盖为: {def.defaultWeapon.bulletOverridePrefab.name}");
                return;
            }
        }
    }
}
