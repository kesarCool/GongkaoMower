#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Targeted patch; does not rebuild approved panels or import another font atlas.</summary>
public static class BattleReadabilityBuilder
{
    [MenuItem("工具/Battle UI/Apply Readability Polish")]
    static void Menu(){Debug.Log(Apply());}
    public static string ApplyWordContrast()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        int labels=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefab/Enemy"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<EnemyWordLabel>(true).Length==0)continue;
            Edit(path,root=>{
                foreach(var label in root.GetComponentsInChildren<EnemyWordLabel>(true))
                {
                    var so=new SerializedObject(label);
                    so.FindProperty("stereoFaceBase").colorValue=path.Contains("/Boss/")?BattleTextTheme.BossFace:BattleTextTheme.WordFace;
                    so.FindProperty("outlineWidthRange").vector2Value=new Vector2(.32f,.32f);
                    so.FindProperty("outlineColor").colorValue=BattleTextTheme.WordOutline;
                    so.FindProperty("gradientTopBrighten").floatValue=.04f;
                    so.ApplyModifiedPropertiesWithoutUndo();labels++;
                }
            });
        }
        AssetDatabase.SaveAssets();return "Updated "+labels+" enemy word styles: bright faces and dark outlines; no font atlas added.";
    }
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        int mapLayer=LayerMask.NameToLayer("Map");if(mapLayer<0)throw new InvalidOperationException("Map layer is missing.");
        Edit("Assets/Prefab/Battle/GameLayer.prefab",root=>{
            var gradient=root.transform.Find("HudTop/TopGradient").GetComponent<Image>();gradient.color=new Color(1,1,1,.82f);
        });
        int mapRenderers=0;
        Edit("Assets/Resources/" + ChapterLevelCatalog.DefaultMapResourcesPath + ".prefab",root=>{
            // This generated map's terrain and props have no collision logic. Existing Map-layer maps already match the camera.
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){renderer.gameObject.layer=mapLayer;mapRenderers++;}
        });
        int labels=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefab/Enemy"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentsInChildren<EnemyWordLabel>(true).Length==0)continue;
            bool boss=path.Contains("/Boss/");
            Edit(path,root=>{
                foreach(var label in root.GetComponentsInChildren<EnemyWordLabel>(true))
                {
                    var so=new SerializedObject(label);
                    so.FindProperty("useCartoonTextTheme").boolValue=true;
                    so.FindProperty("instanceMaterialPerEnemy").boolValue=false;
                    so.FindProperty("enableStereoTextStyle").boolValue=true;
                    so.FindProperty("stereoFaceBase").colorValue=boss?BattleTextTheme.BossFace:BattleTextTheme.WordFace;
                    so.FindProperty("preferWaveSharedTint").boolValue=!boss;
                    so.FindProperty("randomizeStereoHue").boolValue=false;
                    so.FindProperty("perEnemyMicroHueJitter").floatValue=boss?0:.012f;
                    so.FindProperty("outlineWidthRange").vector2Value=new Vector2(.32f,.32f);
                    so.FindProperty("outlineColor").colorValue=BattleTextTheme.WordOutline;
                    so.FindProperty("stereoUseVertexGradient").boolValue=true;
                    so.FindProperty("gradientTopBrighten").floatValue=.04f;
                    so.FindProperty("gradientBottomDarken").floatValue=.04f;
                    so.ApplyModifiedPropertiesWithoutUndo();labels++;
                }
            });
        }
        Edit("Assets/Prefab/DamageFloatText.prefab",root=>{
            var so=new SerializedObject(root.GetComponent<DamageFloatText>());
            so.FindProperty("useCartoonTextTheme").boolValue=true;
            so.FindProperty("textColor").colorValue=new Color32(255,245,221,255);
            so.FindProperty("critColor").colorValue=new Color32(255,196,82,255);
            so.FindProperty("penColor").colorValue=new Color32(237,130,91,255);
            so.FindProperty("resistColor").colorValue=new Color32(215,223,215,255);
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
        return "TopGradient alpha=0.82; default-map renderers moved to Map: "+mapRenderers+"; themed enemy labels: "+labels+"; damage colors updated. No new font asset or atlas.";
    }
    static void Edit(string path,Action<GameObject> action)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
