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

public static class BossZoneV1Builder
{
    public const string RecipePath="Tools/VFX/Library/Boss/boss_zone_v1.json";
    public const string Output="ArtOutput/VFX/BossZoneV1/Audit/";
    static JObject Recipe()
    {
        var r=JObject.Parse(File.ReadAllText(RecipePath));
        if((int)r["schemaVersion"]!=1||(string)r["id"]!="boss_zone_v1"||(string)r["mode"]!="boss-zone"||(string)r["materialPath"]!=BossBladeBurstV1Builder.SolidMaterialPath||
            (string)r["childName"]!="BossZoneVisual"||(int)r["maxZonesPerCast"]!=BossZonePresentation.Capacity||(float)r["warningSeconds"]!=.7f||!((float)r["residueSeconds"]>=.5f&&(float)r["residueSeconds"]<=1.5f))throw new InvalidOperationException("Invalid ground-fissure recipe");
        return r;
    }
    [MenuItem("工具/VFX/Build Boss Zone V1")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        var recipe=Recipe();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);if(material==null)throw new InvalidOperationException("Missing shared Boss material.");
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child=root.transform.Find((string)recipe["childName"]);if(child==null){child=new GameObject((string)recipe["childName"]).transform;child.SetParent(root.transform,false);}
                child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var view=child.GetComponent<BossZonePresentation>();if(view==null)view=child.gameObject.AddComponent<BossZonePresentation>();
                var data=new SerializedObject(view);data.FindProperty("solidMaterial").objectReferenceValue=material;data.FindProperty("sortingOrder").intValue=(int)recipe["sortingOrder"];data.FindProperty("residueSeconds").floatValue=(float)recipe["residueSeconds"];data.ApplyModifiedPropertiesWithoutUndo();
                var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=(int)recipe["sortingOrder"];renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                if(child.GetComponent<MeshFilter>().sharedMesh!=null)throw new InvalidOperationException("Ground-fissure mesh must remain runtime-only.");PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var normalized=string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n";if(File.ReadAllText(path)!=normalized)File.WriteAllText(path,normalized,new System.Text.UTF8Encoding(false));
        }
        Audit();
    }
    [MenuItem("工具/VFX/检查Boss地裂配置")]
    public static void Audit()
    {
        var recipe=Recipe();var checks=new Dictionary<string,bool>();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var name=Path.GetFileNameWithoutExtension(path);var view=root.GetComponentInChildren<BossZonePresentation>(true);bool valid=view!=null&&view.transform.parent==root.transform;
            if(valid){var data=new SerializedObject(view);var mr=view.GetComponent<MeshRenderer>();valid=data.FindProperty("solidMaterial").objectReferenceValue==material&&mr.sharedMaterial==material&&!mr.enabled&&view.GetComponent<MeshFilter>().sharedMesh==null&&mr.sortingOrder==(int)recipe["sortingOrder"]&&Mathf.Approximately(data.FindProperty("residueSeconds").floatValue,(float)recipe["residueSeconds"]);}
            checks[name+":zoneVisual"]=valid;checks[name+":previousPresentations"]=root.GetComponentInChildren<BossRevivePresentation>(true)!=null&&root.GetComponentInChildren<BossSummonPresentation>(true)!=null&&root.GetComponentInChildren<BossClonePresentation>(true)!=null&&root.GetComponentInChildren<BossHeadCrestPresentation>(true)!=null&&root.GetComponentInChildren<BossDashPresentation>(true)!=null&&root.GetComponentInChildren<BossBladeBurstPresentation>(true)!=null&&root.GetComponentInChildren<BossKnifePresentation>(true)!=null;
            checks[name+":physicsAndNoImplicitZoneAssignment"]=root.GetComponent<CircleCollider2D>().radius==1.5f&&root.GetComponent<Rigidbody2D>()!=null&&!root.GetComponent<BossBrain>().inspectorSkills.Any(s=>s.type=="zone");
        }
        bool passed=checks.Values.All(v=>v);Directory.CreateDirectory(Output);File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new{passed,checks,recipe,checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new{addedPngBytes=0,addedTextures=0,addedMaterials=0,meshRenderersPerCaster=1,meshCreatedOnlyOnFirstCast=true,perZoneGameObjects=0,capacity=BossZonePresentation.Capacity,sharedMaterial=(string)recipe["materialPath"],note="Editor verification only; final package and WeChat device memory require separate measurement."},Formatting.Indented));if(!passed)throw new InvalidOperationException("Ground-fissure configuration drift.");
    }
}
#endif
