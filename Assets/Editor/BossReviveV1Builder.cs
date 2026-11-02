#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class BossReviveV1Builder
{
    public const string RecipePath="Tools/VFX/Library/Boss/boss_revive_v1.json";
    public const string Output="ArtOutput/VFX/BossReviveV1/Audit/";
    static JObject Recipe()
    {
        var r=JObject.Parse(File.ReadAllText(RecipePath));
        if((int)r["schemaVersion"]!=1||(string)r["id"]!="boss_revive_v1"||(string)r["mode"]!="boss-revive"||
            (string)r["materialPath"]!=BossBladeBurstV1Builder.SolidMaterialPath||(string)r["childName"]!="BossReviveVisual"||
            (int)r["maxGlyphs"]!=32||!((float)r["scatterDistanceMultiplier"]>=1f&&(float)r["scatterDistanceMultiplier"]<=3f)||!Mathf.Approximately((float)r["scatterEnd"],.23f)||!Mathf.Approximately((float)r["gatherStart"],.46f)||
            !Mathf.Approximately((float)r["gatherEnd"],.96f)||!Mathf.Approximately((float)r["recoverySeconds"],.35f)||!Mathf.Approximately((float)r["shieldSeconds"],.5f))throw new InvalidOperationException("Invalid revive recipe");
        return r;
    }
    [MenuItem("工具/VFX/Build Boss Revive V1")]
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
                var view=child.GetComponent<BossRevivePresentation>();if(view==null)view=child.gameObject.AddComponent<BossRevivePresentation>();
                var data=new SerializedObject(view);data.FindProperty("solidMaterial").objectReferenceValue=material;data.FindProperty("sortingOrder").intValue=(int)recipe["sortingOrder"];data.FindProperty("maxGlyphs").intValue=(int)recipe["maxGlyphs"];data.FindProperty("scatterDistanceMultiplier").floatValue=(float)recipe["scatterDistanceMultiplier"];data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(line=>line.TrimEnd()))+"\n",new System.Text.UTF8Encoding(false));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        Audit();
    }
    [MenuItem("工具/VFX/检查Boss复活配置")]
    public static void Audit()
    {
        var recipe=Recipe();var checks=new Dictionary<string,bool>();var material=AssetDatabase.LoadAssetAtPath<Material>((string)recipe["materialPath"]);
        checks["sharedUntexturedMaterial"]=material!=null&&material.shader.name=="Sprites/Default"&&material.mainTexture==null;
        foreach(var path in BossBladeBurstV1Builder.BossPaths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var view=root.GetComponentInChildren<BossRevivePresentation>(true);var name=Path.GetFileNameWithoutExtension(path);bool valid=view!=null&&view.transform.parent==root.transform;
            if(valid){var data=new SerializedObject(view);valid=data.FindProperty("solidMaterial").objectReferenceValue==material&&data.FindProperty("sortingOrder").intValue==(int)recipe["sortingOrder"]&&data.FindProperty("maxGlyphs").intValue==(int)recipe["maxGlyphs"]&&Mathf.Approximately(data.FindProperty("scatterDistanceMultiplier").floatValue,(float)recipe["scatterDistanceMultiplier"])&&view.transform.childCount==0&&view.RuntimeMeshCount==0;}
            checks[name+":reviveVisual"]=valid;
            checks[name+":previousBossPresentations"]=root.GetComponentInChildren<BossSummonPresentation>(true)!=null&&root.GetComponentInChildren<BossClonePresentation>(true)!=null&&root.GetComponentInChildren<BossHeadCrestPresentation>(true)!=null&&root.GetComponent<BossBrain>().FindPrefab("KnifeBullet")!=null;
        }
        bool passed=checks.Values.All(v=>v);Directory.CreateDirectory(Output);File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new{passed,checks,recipe,checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new{addedPngBytes=0,addedTextures=0,addedMaterials=0,perCharacterGameObjects=0,maxGlyphs=32,runtimeMeshes="1 core + 1 per actually used TMP atlas material; lazy, reused and destroyed with owner",shield="one inactive reusable ResistShield component, no runtime shield texture or ring for revive",sharedMaterial=(string)recipe["materialPath"],note="Editor evidence only; no device performance or upload-size measurement."},Formatting.Indented));
        if(!passed)throw new InvalidOperationException("Revive presentation drift.");
    }
}
#endif
