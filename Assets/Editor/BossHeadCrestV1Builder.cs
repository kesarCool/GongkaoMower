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

public static class BossHeadCrestV1Builder
{
    public const string Output = "ArtOutput/VFX/BossHeadCrestV1/Audit/";
    public const string RecipePath = "Tools/VFX/Library/Boss/boss_head_crest_v1.json";
    static JObject Recipe()
    {
        var r=JObject.Parse(File.ReadAllText(RecipePath));
        if((int)r["schemaVersion"]!=1||(string)r["id"]!="boss_head_crest_v1"||(string)r["mode"]!="boss-head-crest"||
            (string)r["materialPath"]!=BossBladeBurstV1Builder.SolidMaterialPath||(string)r["childName"]!="BossHeadCrestVisual"||
            !((float)r["headGap"]>=.05f&&(float)r["headGap"]<=.3f))throw new InvalidOperationException("Invalid Boss crest recipe.");
        return r;
    }
    [MenuItem("工具/VFX/Build Boss Head Crest V1")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        var recipe=Recipe();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);
        if(material==null)throw new InvalidOperationException("Shared Boss material is missing.");
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                // These two children only held the old decorative xd.png. They are never projectile spawn points.
                var visual=root.transform.Find("Visual");
                foreach(var name in new[]{"LeftHorn","RightHorn"})
                {
                    var old=visual!=null?visual.Find(name):null;
                    if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                var child=root.transform.Find((string)recipe["childName"]);
                if(child==null){child=new GameObject((string)recipe["childName"]).transform;child.SetParent(root.transform,false);}
                child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var view=child.GetComponent<BossHeadCrestPresentation>();if(view==null)view=child.gameObject.AddComponent<BossHeadCrestPresentation>();
                var data=new SerializedObject(view);
                data.FindProperty("solidMaterial").objectReferenceValue=material;
                data.FindProperty("wordLabel").objectReferenceValue=root.GetComponentInChildren<EnemyWordLabel>(true);
                data.FindProperty("headGap").floatValue=(float)recipe["headGap"];data.FindProperty("sortingOrder").intValue=(int)recipe["sortingOrder"];
                data.ApplyModifiedPropertiesWithoutUndo();
                var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=(int)recipe["sortingOrder"];renderer.enabled=false;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            // Unity serializes empty string fields with a trailing space; keep authored YAML clean and repeatable.
            File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(line=>line.TrimEnd()))+"\n",new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        Audit();
    }
    [MenuItem("工具/VFX/检查Boss头顶标识配置")]
    public static void Audit()
    {
        var recipe=Recipe();var checks=new Dictionary<string,bool>();
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var name=Path.GetFileNameWithoutExtension(path);
            var crest=root.GetComponentInChildren<BossHeadCrestPresentation>(true);
            var data=crest!=null?new SerializedObject(crest):null;
            checks[name+":compactCrest"]=crest!=null&&crest.transform.parent==root.transform&&crest.GetComponent<MeshFilter>().sharedMesh==null&&Mathf.Approximately(data.FindProperty("headGap").floatValue,(float)recipe["headGap"])&&data.FindProperty("sortingOrder").intValue==(int)recipe["sortingOrder"];
            checks[name+":oldDecorationsRemoved"]=!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="LeftHorn"||t.name=="RightHorn");
            checks[name+":sharedMaterial"]=crest!=null&&crest.GetComponent<MeshRenderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(BossBladeBurstV1Builder.SolidMaterialPath);
            checks[name+":skillsPreserved"]=root.GetComponentInChildren<BossClonePresentation>(true)!=null&&root.GetComponentInChildren<BossDashPresentation>(true)!=null&&root.GetComponent<BossBrain>().FindPrefab("KnifeBullet")!=null;
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new{passed=checks.Values.All(v=>v),checks,recipe,addedTextures=0,addedMaterials=0,runtimeMeshPerBoss=1,checkedUtc=DateTime.UtcNow},Formatting.Indented));
        if(checks.Values.Any(v=>!v))throw new InvalidOperationException("Boss crest configuration drift.");
    }
}
#endif
