#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建/更新 SkillDef_FieldGenerator 并注册到 SkillCatalog（避免手改 .meta GUID）。
/// </summary>
public static class SkillFieldGeneratorSetup
{
    private const string DefPath = "Assets/ScriptableObject/Skill/SkillDef_FieldGenerator.asset";
    private const string CatalogPath = "Assets/ScriptableObject/Skill/SkillCatalog.asset";
    private const string FxPath = "Assets/Res/Prefabs/Effects/fx-Goku-Supper.prefab";

    [MenuItem("Game/Skills/Register FieldGenerator SkillDef")]
    public static void Register()
    {
        var def = AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<FieldGeneratorSkillDefinition>();
            AssetDatabase.CreateAsset(def, DefPath);
        }

        def.id = SkillId.FieldGenerator;
        def.maxLevel = 5;
        def.displayName = "力场发生器";
        def.description = "附着角色的持续溶解力场，对范围内敌人造成持续伤害。";
        def.fieldVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxPath);
        def.visualBaseDiameter = 6f;
        def.sortingOrder = 30;
        def.radiusByLevel = new[] { 1.5f, 1.7f, 1.9f, 2.1f, 2.4f };
        def.damagePerSecondByLevel = new[] { 15f, 20f, 26f, 33f, 42f };
        def.damageTickIntervalByLevel = new[] { 0.25f, 0.22f, 0.19f, 0.16f, 0.13f };
        def.visualIntensityByLevel = new[] { 0.7f, 0.85f, 1f, 1.15f, 1.3f };

        EditorUtility.SetDirty(def);

        var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[SkillFieldGeneratorSetup] 找不到 SkillCatalog: {CatalogPath}");
            AssetDatabase.SaveAssets();
            return;
        }

        bool found = false;
        for (int i = 0; i < catalog.skills.Count; i++)
        {
            if (catalog.skills[i] != null && catalog.skills[i].id == SkillId.FieldGenerator)
            {
                catalog.skills[i] = def;
                found = true;
                break;
            }
        }

        if (!found)
            catalog.skills.Add(def);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[SkillFieldGeneratorSetup] SkillDef_FieldGenerator 已写入并注册到 SkillCatalog。");
    }

    private const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";

    [MenuItem("Game/Skills/Add FieldGenerator Radius Gizmo To Player")]
    public static void AddRadiusGizmoToPlayer()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[SkillFieldGeneratorSetup] 找不到 Player Prefab: {PlayerPrefabPath}");
            return;
        }

        if (prefabRoot.GetComponent<FieldGeneratorRadiusGizmo>() == null)
            prefabRoot.AddComponent<FieldGeneratorRadiusGizmo>();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log("[SkillFieldGeneratorSetup] 已为 Player 挂载 FieldGeneratorRadiusGizmo。");
    }
}
#endif
