using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键创建角色模块所需的 SO 资产（CharacterCatalog + WeaponDefinition），
/// 并关联已有 CharacterDefinition。菜单入口：Tools/角色模块 - 初始化配置资产
/// </summary>
public static class CharacterSetupTool
{
    private const string CharacterDir = "Assets/ScriptableObject/Character";

    [MenuItem("Tools/角色模块 - 初始化配置资产", false, 210)]
    public static void Run()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            // 1. 创建 CharacterCatalog（若不存在）
            CharacterCatalog catalog = CreateOrReplace<CharacterCatalog>(
                $"{CharacterDir}/CharacterCatalog.asset", "CharacterCatalog");

            // 2. 创建默认武器（手枪）
            WeaponDefinition pistol = CreateOrReplace<WeaponDefinition>(
                $"{CharacterDir}/Weapon_Pistol.asset", "Weapon_Pistol");
            if (pistol.weaponId == null || pistol.weaponId.Length == 0)
            {
                pistol.weaponId = "weapon_pistol";
                pistol.displayName = "手枪";
                pistol.localPosition = new Vector3(0.35f, 0f, 0f);
                pistol.attackSwingAngle = 14f;
                pistol.attackBobOffset = new Vector3(0.06f, 0.02f, 0f);
                pistol.attackSwingDuration = 0.1f;
                pistol.attackRecoverDuration = 0.08f;
                pistol.sortingOrderOffset = 1;
                pistol.localScale = Vector3.one;
                pistol.weaponSkillId = SkillId.AutoProjectile;
                EditorUtility.SetDirty(pistol);
            }

            // 3. 创建示例武器（刀剑）
            WeaponDefinition sword = CreateOrReplace<WeaponDefinition>(
                $"{CharacterDir}/Weapon_Sword.asset", "Weapon_Sword");
            if (sword.weaponId == null || sword.weaponId.Length == 0)
            {
                sword.weaponId = "weapon_sword";
                sword.displayName = "长剑";
                sword.localPosition = new Vector3(0.4f, -0.05f, 0f);
                sword.attackSwingAngle = 22f;
                sword.attackBobOffset = new Vector3(0.08f, 0.04f, 0f);
                sword.attackSwingDuration = 0.12f;
                sword.attackRecoverDuration = 0.1f;
                sword.sortingOrderOffset = 1;
                sword.localScale = Vector3.one;
                sword.weaponSkillId = SkillId.AutoProjectile;
                EditorUtility.SetDirty(sword);
            }

            // 4. 关联已有角色定义
            CharacterDefinition pistolGuy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                $"{CharacterDir}/Character_PistolGuy.asset");
            if (pistolGuy != null)
            {
                if (pistolGuy.defaultWeapon == null)
                {
                    pistolGuy.defaultWeapon = pistol;
                    EditorUtility.SetDirty(pistolGuy);
                }

                if (catalog.characters.Count == 0 || !catalog.characters.Contains(pistolGuy))
                {
                    catalog.characters.Add(pistolGuy);
                    EditorUtility.SetDirty(catalog);
                }

                if (catalog.defaultCharacter == null)
                {
                    catalog.defaultCharacter = pistolGuy;
                    EditorUtility.SetDirty(catalog);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CharacterSetup] 初始化完成。\n"
                + $"  CharacterCatalog: {catalog.characters.Count} 个角色\n"
                + $"  武器: {pistol.displayName} / {sword.displayName}\n"
                + $"  默认角色: {(catalog.defaultCharacter != null ? catalog.defaultCharacter.displayName : "未设置")}\n\n"
                + "后续步骤：\n"
                + "1. 拖入武器的 Sprite（Weapon_Pistol / Weapon_Sword）\n"
                + "2. 拖入角色的 bodySprite（Character_PistolGuy）\n"
                + "3. Player Prefab 上挂 CharacterConfigApplier，拖 CharacterCatalog 引用");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static T CreateOrReplace<T>(string path, string name) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            if (existing.name != name)
            {
                existing.name = name;
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        T inst = ScriptableObject.CreateInstance<T>();
        inst.name = name;
        AssetDatabase.CreateAsset(inst, path);
        Debug.Log($"[CharacterSetup] 已创建: {path}");
        return inst;
    }
}
