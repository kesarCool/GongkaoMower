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

public static class BossSummonV1Builder
{
    public const string RecipePath="Tools/VFX/Library/Boss/boss_summon_v1.json";
    public const string Output="ArtOutput/VFX/BossSummonV1/Audit/";
    static JObject Recipe()
    {
        var r=JObject.Parse(File.ReadAllText(RecipePath));
        if((int)r["schemaVersion"]!=1||(string)r["id"]!="boss_summon_v1"||(string)r["mode"]!="boss-summon"||
            (string)r["materialPath"]!=BossBladeBurstV1Builder.SolidMaterialPath||(string)r["childName"]!="BossSummonVisual"||
            (int)r["birthCapacityPerCaster"]!=32||!((float)r["birthSeconds"]>=.4f&&(float)r["birthSeconds"]<=1.2f)||!((float)r["releaseSeconds"]>=.2f&&(float)r["releaseSeconds"]<=.6f))throw new InvalidOperationException("Invalid summon recipe");
        return r;
    }
    [MenuItem("工具/VFX/Build Boss Summon V1")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        var recipe=Recipe();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);
        if(material==null)throw new InvalidOperationException("Missing shared Boss material.");
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child=root.transform.Find((string)recipe["childName"]);if(child==null){child=new GameObject((string)recipe["childName"]).transform;child.SetParent(root.transform,false);}
                child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var component=child.GetComponent<BossSummonPresentation>();if(component==null)component=child.gameObject.AddComponent<BossSummonPresentation>();
                var data=new SerializedObject(component);data.FindProperty("solidMaterial").objectReferenceValue=material;data.FindProperty("sortingOrder").intValue=(int)recipe["sortingOrder"];
                foreach(var field in new[]{"birthSeconds","releaseSeconds"})data.FindProperty(field).floatValue=(float)recipe[field];data.ApplyModifiedPropertiesWithoutUndo();
                var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=(int)recipe["sortingOrder"];renderer.enabled=false;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                if(child.GetComponent<MeshFilter>().sharedMesh!=null)throw new InvalidOperationException("Summon mesh must remain runtime-only.");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(line=>line.TrimEnd()))+"\n",new System.Text.UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        Audit();
    }
    [MenuItem("工具/VFX/检查Boss召唤配置")]
    public static void Audit()
    {
        var recipe=Recipe();var checks=new Dictionary<string,bool>();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);
        checks["sharedUntexturedMaterial"]=material!=null&&material.shader.name=="Sprites/Default"&&material.mainTexture==null;
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var view=root.GetComponentInChildren<BossSummonPresentation>(true);var name=Path.GetFileNameWithoutExtension(path);
            bool valid=view!=null&&view.transform.parent==root.transform;
            if(valid)
            {
                var data=new SerializedObject(view);var renderer=view.GetComponent<MeshRenderer>();valid=data.FindProperty("solidMaterial").objectReferenceValue==material&&renderer.sharedMaterial==material&&!renderer.enabled&&view.GetComponent<MeshFilter>().sharedMesh==null&&renderer.sortingOrder==(int)recipe["sortingOrder"];
                foreach(var field in new[]{"birthSeconds","releaseSeconds"})valid&=Mathf.Approximately(data.FindProperty(field).floatValue,(float)recipe[field]);
            }
            checks[name+":summonVisual"]=valid;
            checks[name+":otherPresentationsPreserved"]=root.GetComponentInChildren<BossClonePresentation>(true)!=null&&root.GetComponentInChildren<BossHeadCrestPresentation>(true)!=null&&root.GetComponentInChildren<BossDashPresentation>(true)!=null&&root.GetComponentInChildren<BossBladeBurstPresentation>(true)!=null&&root.GetComponentInChildren<BossKnifePresentation>(true)!=null;
        }
        bool passed=checks.Values.All(v=>v);Directory.CreateDirectory(Output);File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new{passed,checks,recipe,checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new{addedPngBytes=0,addedTextures=0,addedMaterials=0,meshRenderersPerCaster=1,birthCapacityPerCaster=32,perBirthGameObjects=0,sharedMaterial=(string)recipe["materialPath"],note="Birth marks use final post-clamp positions and do not delay or fade real enemies. No WeChat device / final package measurement."},Formatting.Indented));
        if(!passed)throw new InvalidOperationException("Summon presentation drift.");
    }
}
#endif
