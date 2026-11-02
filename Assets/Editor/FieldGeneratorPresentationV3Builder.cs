#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class FieldGeneratorPresentationV3Builder
{
    public const string RecipePath="Tools/VFX/Library/Persistent/field_generator_v3.json";
    public const string PrefabPath="Assets/Res/Prefabs/Effects/Aura/fx_field_generator_v3.prefab";
    public const string WaveMeshPath="Assets/Res/Meshes/VFX/mesh_field_orbits_v3.asset";
    public const string DefinitionPath=FieldGeneratorPresentationV2Builder.DefinitionPath;
    const string Output="ArtOutput/VFX/FieldGeneratorV3/Audit/";
    public static FieldOrbitRecipe ReadRecipe()
    {
        var r=JsonConvert.DeserializeObject<FieldOrbitRecipe>(File.ReadAllText(RecipePath),new JsonSerializerSettings {MissingMemberHandling=MissingMemberHandling.Error});
        if(r==null || r.schemaVersion!=3 || r.id!="field_generator_v3" || r.mode!="persistent-field-orbit" || r.prefabPath!=PrefabPath || r.waveMeshPath!=WaveMeshPath || r.materialPath!=FieldGeneratorPresentationV2Builder.MaterialPath || r.dedicatedTextureCount!=0 || r.waveCount!=3 || r.sortingOrder>=0 || r.visualBaseDiameter!=2)
            throw new InvalidOperationException("Invalid orbit field recipe identity/layout.");
        if(!(r.surfaceOpacity>=.01f && r.surfaceOpacity<=.3f && r.waveOpacity>=.1f && r.waveOpacity<=1 && r.travelMultiplier>=1 && r.travelMultiplier<=3 && r.orbitSpeed>=5 && r.orbitSpeed<=90 && r.breakthroughOrbitMultiplier>=1 && r.breakthroughOrbitMultiplier<=2 && r.breakthroughBandWidth>=1.2f && r.breakthroughBandWidth<=2.2f && r.breakthroughAreaBoost>=0 && r.breakthroughAreaBoost<=.1f && r.sourceAssetBudgetKiB>=1 && r.sourceAssetBudgetKiB<=192))
            throw new InvalidOperationException("Invalid orbit motion, opacity or resource budget.");
        foreach(var c in new[]{r.normalColor,r.breakthroughColor}) if(!(c.r>=0 && c.r<=1 && c.g>=0 && c.g<=1 && c.b>=0 && c.b<=1 && c.a==1)) throw new InvalidOperationException("Invalid orbit palette.");
        return r;
    }
    [MenuItem("工具/VFX/Build Field Generator Orbit V3")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var r=ReadRecipe(); string before=File.ReadAllText(DefinitionPath);
        var material=AssetDatabase.LoadAssetAtPath<Material>(r.materialPath);
        var area=AssetDatabase.LoadAssetAtPath<Mesh>(FieldGeneratorPresentationV2Builder.AreaMeshPath);
        var edge=AssetDatabase.LoadAssetAtPath<Mesh>(FieldGeneratorPresentationV2Builder.EdgeMeshPath);
        if(material==null || area==null || edge==null) throw new InvalidOperationException("V2 shared material, area and boundary meshes must exist.");
        var generated=FieldGeneratorPresentation.CreateFieldOrbitMesh(); generated.name=Path.GetFileNameWithoutExtension(WaveMeshPath);
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(WaveMeshPath);
        if(mesh==null) { mesh=generated; AssetDatabase.CreateAsset(mesh,WaveMeshPath); }
        else { EditorUtility.CopySerialized(generated,mesh); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        var root=new GameObject("fx_field_generator_v3"); GameObject prefab;
        try
        {
            var surface=FieldGeneratorPresentation.MakeRing(root.transform,"FieldSurface",area,material);
            var waves=FieldGeneratorPresentation.MakeRing(root.transform,"HexWaves",mesh,material);
            var boundary=FieldGeneratorPresentation.MakeRing(root.transform,"Boundary",edge,material);
            foreach(var renderer in new[]{surface,waves,boundary}) renderer.sortingOrder=r.sortingOrder;
            var view=root.AddComponent<FieldGeneratorPresentation>();
            view.AuthorField(surface,waves,boundary,r.normalColor,r.breakthroughColor,r.surfaceOpacity,r.waveOpacity,r.travelMultiplier);
            view.AuthorFieldOrbit(r.orbitSpeed,r.breakthroughOrbitMultiplier,r.breakthroughBandWidth,r.breakthroughAreaBoost);
            prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var def=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        def.fieldVisualPrefab=prefab; def.visualBaseDiameter=r.visualBaseDiameter; def.sortingOrder=r.sortingOrder; EditorUtility.SetDirty(def); AssetDatabase.SaveAssetIfDirty(def);
        string after=File.ReadAllText(DefinitionPath); const string pattern=@"(?m)^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\r\n]*";
        File.WriteAllText(DefinitionPath,Regex.Replace(before,pattern,m=>Regex.Match(after,@"(?m)^  "+m.Groups[1].Value+@":[^\r\n]*").Value));
        AssetDatabase.ImportAsset(DefinitionPath,ImportAssetOptions.ForceUpdate); Audit();
    }
    [MenuItem("工具/VFX/常驻特效/检查旋转力场 V3 配置")]
    public static void Audit()
    {
        var r=ReadRecipe(); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null) throw new InvalidOperationException("Build orbit field first.");
        var view=prefab.GetComponent<FieldGeneratorPresentation>(); var so=new SerializedObject(view);
        var def=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        var renderers=prefab.GetComponentsInChildren<Renderer>(); var meshes=prefab.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
        var deps=AssetDatabase.GetDependencies(PrefabPath,true);
        var assets=new[]{PrefabPath,r.materialPath,WaveMeshPath,FieldGeneratorPresentationV2Builder.AreaMeshPath,FieldGeneratorPresentationV2Builder.EdgeMeshPath}.Select(p=>new {path=p,bytes=new FileInfo(p).Length}).ToArray();
        bool passed=view.UsesFieldOrbit && view.HexWaveCount==r.waveCount && renderers.Length==3 && renderers.Select(x=>x.sharedMaterial).Distinct().Count()==1 && renderers.All(x=>x.sharedMaterial.mainTexture==null && x.sortingOrder==r.sortingOrder) &&
            !deps.Any(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p)!=null) && prefab.GetComponentsInChildren<ParticleSystem>().Length==0 && prefab.GetComponentsInChildren<Collider2D>().Length==0 &&
            def.fieldVisualPrefab==prefab && def.visualBaseDiameter==r.visualBaseDiameter && def.sortingOrder==r.sortingOrder &&
            so.FindProperty("normalFieldColor").colorValue==r.normalColor && so.FindProperty("breakthroughFieldColor").colorValue==r.breakthroughColor &&
            so.FindProperty("surfaceOpacity").floatValue==r.surfaceOpacity && so.FindProperty("waveOpacity").floatValue==r.waveOpacity && so.FindProperty("fieldTravelMultiplier").floatValue==r.travelMultiplier &&
            so.FindProperty("fieldOrbitSpeed").floatValue==r.orbitSpeed && so.FindProperty("breakthroughOrbitMultiplier").floatValue==r.breakthroughOrbitMultiplier && so.FindProperty("breakthroughBandWidth").floatValue==r.breakthroughBandWidth && so.FindProperty("breakthroughAreaBoost").floatValue==r.breakthroughAreaBoost &&
            assets.Sum(a=>a.bytes)<=r.sourceAssetBudgetKiB*1024 && meshes.Sum(m=>m.triangles.Length/3)<=2000;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new {passed,recipe=JObject.Parse(File.ReadAllText(RecipePath)),checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new {renderers=renderers.Length,sharedMaterials=renderers.Select(x=>x.sharedMaterial).Distinct().Count(),dedicatedTextures=0,triangles=meshes.Sum(m=>m.triangles.Length/3),sharedMeshVertices=meshes.Sum(m=>m.vertexCount),runtimeWaveMeshClonesPerField=1,sourceAssetBytes=assets.Sum(a=>a.bytes),sourceAssets=assets,editorSharedMeshMemoryBytes=meshes.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)),dependencies=deps,note="V2 material/coverage/boundary reused. Source asset bytes and Editor API memory are not WeChat package size or device FPS."},Formatting.Indented));
        if(!passed) throw new InvalidOperationException("Orbit field configuration drift or budget exceeded.");
    }
}
[Serializable] public sealed class FieldOrbitRecipe
{
    public int schemaVersion,waveCount,sortingOrder,dedicatedTextureCount,sourceAssetBudgetKiB;
    public string id,mode,displayName,status,prefabPath,waveMeshPath,materialPath,briefPath,referencePath,validationReport;
    public Color normalColor,breakthroughColor;
    public float surfaceOpacity,waveOpacity,travelMultiplier,visualBaseDiameter,orbitSpeed,breakthroughOrbitMultiplier,breakthroughBandWidth,breakthroughAreaBoost;
}
#endif
