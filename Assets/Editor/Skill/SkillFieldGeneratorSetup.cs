#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 重建已注册罡气护体的表现，保留已有技能数值和 Catalog 引用。
/// </summary>
public static class SkillFieldGeneratorSetup
{

    [MenuItem("Game/Skills/Register FieldGenerator SkillDef")]
    public static void Register() { FieldGeneratorPresentationV3Builder.Build(); }

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
