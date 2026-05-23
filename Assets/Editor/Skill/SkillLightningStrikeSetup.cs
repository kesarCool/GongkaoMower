#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建/更新 SkillDef_LightningStrike 并注册到 SkillCatalog。
/// </summary>
public static class SkillLightningStrikeSetup
{
    private const string DefPath = "Assets/ScriptableObject/Skill/SkillDef_LightningStrike.asset";
    private const string CatalogPath = "Assets/ScriptableObject/Skill/SkillCatalog.asset";
    private const string FxPath = "Assets/Res/Prefabs/Effects/fx-explosive-2.prefab";

    [MenuItem("Game/Skills/Register LightningStrike SkillDef")]
    public static void Register()
    {
        var def = AssetDatabase.LoadAssetAtPath<LightningStrikeSkillDefinition>(DefPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<LightningStrikeSkillDefinition>();
            AssetDatabase.CreateAsset(def, DefPath);
        }

        def.id = SkillId.LightningStrike;
        def.maxLevel = 5;
        def.displayName = "雷击术";
        def.description = "随机对范围内敌人落雷，造成圆形范围伤害。";
        def.strikeFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxPath);
        def.maxRange = 14f;
        def.strikeStagger = 0.18f;
        def.intervalByLevel = new[] { 2.2f, 2f, 1.85f, 1.7f, 1.55f };
        def.damageByLevel = new[] { 80f, 100f, 125f, 155f, 190f };
        def.strikeRadiusByLevel = new[] { 1.1f, 1.15f, 1.2f, 1.25f, 1.35f };
        def.strikeCountByLevel = new[] { 1, 1, 2, 2, 3 };

        EditorUtility.SetDirty(def);

        var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[SkillLightningStrikeSetup] 找不到 SkillCatalog: {CatalogPath}");
            AssetDatabase.SaveAssets();
            return;
        }

        bool found = false;
        for (int i = 0; i < catalog.skills.Count; i++)
        {
            if (catalog.skills[i] != null && catalog.skills[i].id == SkillId.LightningStrike)
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
        Debug.Log("[SkillLightningStrikeSetup] SkillDef_LightningStrike 已写入并注册到 SkillCatalog。");
    }
}
#endif
