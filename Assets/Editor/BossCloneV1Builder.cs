#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BossCloneV1Builder
{
    public const string RecipePath="Tools/VFX/Library/Boss/boss_clone_v1.json";
    public const string Output="ArtOutput/VFX/BossCloneV1/Audit/";
    public static JObject Recipe()
    {
        var r=JObject.Parse(File.ReadAllText(RecipePath));
        if((int)r["schemaVersion"]!=1||(string)r["id"]!="boss_clone_v1"||(string)r["mode"]!="boss-clone"||
            (string)r["materialPath"]!=BossBladeBurstV1Builder.SolidMaterialPath||(string)r["exitPrefabPath"]!=BossBladeBurstV1Builder.HitPath||
            (float)r["cloneAlpha"]<.5f||(float)r["cloneAlpha"]>.75f||(string)r["childName"]!="BossCloneVisual")throw new InvalidOperationException("Invalid clone presentation recipe");
        return r;
    }
    [MenuItem("工具/VFX/Build Boss Clone V1")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        var r=Recipe();var material=AssetDatabase.LoadAssetAtPath<Material>((string)r["materialPath"]);var exit=AssetDatabase.LoadAssetAtPath<GameObject>((string)r["exitPrefabPath"]);
        if(material==null||exit==null)throw new InvalidOperationException("Build shared Boss blade resources first.");
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child=root.transform.Find((string)r["childName"]);if(child==null){child=new GameObject((string)r["childName"]).transform;child.SetParent(root.transform,false);}
                child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var component=child.GetComponent<BossClonePresentation>();if(component==null)component=child.gameObject.AddComponent<BossClonePresentation>();
                var data=new SerializedObject(component);data.FindProperty("solidMaterial").objectReferenceValue=material;data.FindProperty("exitPrefab").objectReferenceValue=exit;
                data.FindProperty("sortingOrder").intValue=(int)r["sortingOrder"];
                foreach(var field in new[]{"cloneAlpha","birthSeconds","expiryFadeSeconds"})data.FindProperty(field).floatValue=(float)r[field];data.ApplyModifiedPropertiesWithoutUndo();
                var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=(int)r["sortingOrder"];renderer.enabled=false;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                if(child.GetComponent<MeshFilter>().sharedMesh!=null)throw new InvalidOperationException("Clone meshes must stay runtime-only.");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Audit();
    }
    [MenuItem("工具/VFX/检查Boss分身配置")]
    public static void Audit()
    {
        var recipe=Recipe();var checks=new Dictionary<string,bool>();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);var exit=AssetDatabase.LoadAssetAtPath<GameObject>((string)recipe["exitPrefabPath"]);
        checks["sharedUntexturedMaterial"]=material!=null&&material.shader.name=="Sprites/Default"&&material.mainTexture==null;
        checks["sharedExitFeedback"]=exit!=null&&exit.GetComponent<PooledCombatVfx>()!=null;
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var child=root.transform.Find((string)recipe["childName"]);var view=child!=null?child.GetComponent<BossClonePresentation>():null;
            bool valid=view!=null&&root.GetComponent<CloneMarker>()==null;
            if(valid)
            {
                var data=new SerializedObject(view);var renderer=child.GetComponent<MeshRenderer>();
                valid=data.FindProperty("solidMaterial").objectReferenceValue==material&&data.FindProperty("exitPrefab").objectReferenceValue==exit&&renderer.sharedMaterial==material&&!renderer.enabled&&child.GetComponent<MeshFilter>().sharedMesh==null;
                foreach(var field in new[]{"cloneAlpha","birthSeconds","expiryFadeSeconds"})valid&=Mathf.Approximately(data.FindProperty(field).floatValue,(float)recipe[field]);
            }
            checks["cloneVisual:"+Path.GetFileNameWithoutExtension(path)]=valid;
            var brain=root.GetComponent<BossBrain>();checks["previousBossVisuals:"+Path.GetFileNameWithoutExtension(path)]=brain.FindPrefab("BladeBurstBullet")!=null&&brain.FindPrefab("KnifeBullet")!=null&&root.GetComponentInChildren<BossDashPresentation>(true)!=null;
        }
        bool passed=checks.Values.All(v=>v);Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new{passed,checks,recipe,checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new{addedPngBytes=0,addedTextures=0,addedMaterials=0,identityRenderersPerActor=1,chargeEchoRenderersPerOwner="2 per actual TMP atlas material, charge only; shared font materials",cloneAlpha=(float)recipe["cloneAlpha"],birthSeconds=(float)recipe["birthSeconds"],expiryFadeSeconds=(float)recipe["expiryFadeSeconds"],sharedMaterial=(string)recipe["materialPath"],exitPrefab=(string)recipe["exitPrefabPath"],note="Runtime meshes are reused and destroyed with the actor. No device FPS or final WeChat package-size measurement."},Formatting.Indented));
        if(!passed)throw new InvalidOperationException("Boss clone configuration drift.");
    }
}
#endif
