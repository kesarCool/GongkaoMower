#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class FieldGeneratorPresentationV2Builder
{
    public const string RecipePath = "Tools/VFX/Library/Persistent/field_generator_v2.json";
    public const string PrefabPath = "Assets/Res/Prefabs/Effects/Aura/fx_field_generator_v2.prefab";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_field_surface_v2.mat";
    public const string AreaMeshPath = "Assets/Res/Meshes/VFX/mesh_field_area_v2.asset";
    public const string WaveMeshPath = "Assets/Res/Meshes/VFX/mesh_field_waves_v2.asset";
    public const string EdgeMeshPath = "Assets/Res/Meshes/VFX/mesh_field_edge_v2.asset";
    public const string DefinitionPath = FieldGeneratorPresentationV1Builder.DefinitionPath;
    const string Output = "ArtOutput/VFX/FieldGeneratorV2/Audit/";

    public static FieldSurfaceRecipe ReadRecipe()
    {
        var r = JsonConvert.DeserializeObject<FieldSurfaceRecipe>(File.ReadAllText(RecipePath),new JsonSerializerSettings {MissingMemberHandling=MissingMemberHandling.Error});
        if (r == null || r.schemaVersion != 2 || r.id != "field_generator_v2" || r.mode != "persistent-field" || r.prefabPath != PrefabPath || r.materialPath != MaterialPath || r.dedicatedTextureCount != 0 || r.waveCount != 3 || r.visualBaseDiameter != 2 || r.sortingOrder >= 0)
            throw new InvalidOperationException("Invalid surface field recipe identity/layout.");
        if (!(r.surfaceOpacity >= .01f && r.surfaceOpacity <= .4f && r.waveOpacity >= .1f && r.waveOpacity <= 1 && r.travelMultiplier >= 1 && r.travelMultiplier <= 3)) throw new InvalidOperationException("Invalid field opacity or travel multiplier.");
        foreach (var c in new[] {r.normalColor,r.breakthroughColor}) if (!(c.r>=0 && c.r<=1 && c.g>=0 && c.g<=1 && c.b>=0 && c.b<=1 && c.a==1)) throw new InvalidOperationException("Palette must be normalized RGB with alpha 1.");
        return r;
    }
    [MenuItem("工具/VFX/Build Field Generator Surface V2")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var r=ReadRecipe(); string definitionBefore=File.ReadAllText(DefinitionPath);
        foreach (var p in new[] {PrefabPath,MaterialPath,AreaMeshPath,WaveMeshPath,EdgeMeshPath}) Directory.CreateDirectory(Path.GetDirectoryName(p));
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        var shader=Shader.Find("Sprites/Default"); if (shader==null) throw new InvalidOperationException("Missing Sprites/Default.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,MaterialPath); }
        material.shader=shader; material.mainTexture=null; material.color=Color.white; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        var area=SaveMesh(AreaMeshPath,FieldGeneratorPresentation.CreateFieldSurfaceMesh());
        var waves=SaveMesh(WaveMeshPath,FieldGeneratorPresentation.CreateFieldWaveMesh());
        var edge=SaveMesh(EdgeMeshPath,FieldGeneratorPresentation.CreateFieldBoundaryMesh());
        var root=new GameObject("fx_field_generator_v2"); GameObject prefab;
        try
        {
            var surface=FieldGeneratorPresentation.MakeRing(root.transform,"FieldSurface",area,material);
            var waveRenderer=FieldGeneratorPresentation.MakeRing(root.transform,"HexWaves",waves,material);
            var boundary=FieldGeneratorPresentation.MakeRing(root.transform,"Boundary",edge,material);
            foreach (var renderer in new[] {surface,waveRenderer,boundary}) renderer.sortingOrder=r.sortingOrder;
            root.AddComponent<FieldGeneratorPresentation>().AuthorField(surface,waveRenderer,boundary,r.normalColor,r.breakthroughColor,r.surfaceOpacity,r.waveOpacity,r.travelMultiplier);
            // Do not initialize the runtime wave clone while authoring a prefab.
            prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var def=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        def.fieldVisualPrefab=prefab; def.visualBaseDiameter=r.visualBaseDiameter; def.sortingOrder=r.sortingOrder; EditorUtility.SetDirty(def); AssetDatabase.SaveAssetIfDirty(def);
        string after=File.ReadAllText(DefinitionPath); const string pattern=@"(?m)^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\r\n]*";
        File.WriteAllText(DefinitionPath,Regex.Replace(definitionBefore,pattern,m=>Regex.Match(after,@"(?m)^  "+m.Groups[1].Value+@":[^\r\n]*").Value));
        AssetDatabase.ImportAsset(DefinitionPath,ImportAssetOptions.ForceUpdate); Audit();
    }
    static Mesh SaveMesh(string path,Mesh generated)
    {
        generated.name=Path.GetFileNameWithoutExtension(path);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing==null) { AssetDatabase.CreateAsset(generated,path); return generated; }
        EditorUtility.CopySerialized(generated,existing); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); return existing;
    }
    [MenuItem("工具/VFX/常驻特效/检查罡气护体配置")]
    public static void Audit()
    {
        var current=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        if(current!=null && current.fieldVisualPrefab!=null && current.fieldVisualPrefab.GetComponent<FieldGeneratorPresentation>().UsesFieldOrbit) { FieldGeneratorPresentationV3Builder.Audit(); return; }
        var r=ReadRecipe(); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab==null) throw new InvalidOperationException("Build the V2 field first.");
        var view=prefab.GetComponent<FieldGeneratorPresentation>(); var renderers=prefab.GetComponentsInChildren<Renderer>();
        var deps=AssetDatabase.GetDependencies(PrefabPath,true); var so=new SerializedObject(view);
        var def=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        bool passed=view.UsesFieldSurface && renderers.Length==3 && renderers.All(x=>x.sortingOrder==r.sortingOrder) && renderers.Select(x=>x.sharedMaterial).Distinct().Count()==1 &&
            renderers.All(x=>x.sharedMaterial.mainTexture==null) && !deps.Any(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p)!=null) && prefab.GetComponentsInChildren<ParticleSystem>().Length==0 && prefab.GetComponentsInChildren<Collider2D>().Length==0 &&
            def.fieldVisualPrefab==prefab && def.visualBaseDiameter==r.visualBaseDiameter && def.sortingOrder==r.sortingOrder &&
            so.FindProperty("normalFieldColor").colorValue==r.normalColor && so.FindProperty("breakthroughFieldColor").colorValue==r.breakthroughColor &&
            so.FindProperty("surfaceOpacity").floatValue==r.surfaceOpacity && so.FindProperty("waveOpacity").floatValue==r.waveOpacity && so.FindProperty("fieldTravelMultiplier").floatValue==r.travelMultiplier;
        var meshes=prefab.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new {passed,recipe=JObject.Parse(File.ReadAllText(RecipePath)),checkedUtc=DateTime.UtcNow},Formatting.Indented));
        var sourceAssets=new[] {PrefabPath,MaterialPath,AreaMeshPath,WaveMeshPath,EdgeMeshPath}.Select(p=>new {path=p,bytes=new FileInfo(p).Length}).ToArray();
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new {renderers=renderers.Length,sharedMaterials=renderers.Select(x=>x.sharedMaterial).Distinct().Count(),dedicatedTextures=0,triangles=meshes.Sum(m=>m.triangles.Length/3),sharedMeshVertices=meshes.Sum(m=>m.vertexCount),runtimeWaveMeshClonesPerField=1,sourceAssetBytes=sourceAssets.Sum(a=>a.bytes),sourceAssets,editorSharedMeshMemoryBytes=meshes.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)),dependencies=deps,note="Geometry and vertex colours only. No dedicated PNG. Text-serialized asset bytes and Editor API memory are not final WeChat upload size or device performance."},Formatting.Indented));
        if (!passed) throw new InvalidOperationException("V2 field configuration drift.");
    }
}
[Serializable] public sealed class FieldSurfaceRecipe
{
    public int schemaVersion,waveCount,sortingOrder,dedicatedTextureCount;
    public string id,mode,displayName,status,prefabPath,materialPath,briefPath,referencePath,validationReport;
    public Color normalColor,breakthroughColor;
    public float surfaceOpacity,waveOpacity,travelMultiplier,visualBaseDiameter;
}
#endif
