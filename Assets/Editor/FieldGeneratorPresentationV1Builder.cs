#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class FieldGeneratorPresentationV1Builder
{
    public const string RecipePath = "Tools/VFX/Library/Persistent/field_generator_v1.json";
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_field_generator_v1.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_field_generator_v1.mat";
    public const string RingMaterialPath = "Assets/Res/Materials/VFX/mat_field_ring_v1.mat";
    public const string MeshPath = "Assets/Res/Meshes/VFX/mesh_field_ring_v1.asset";
    public const string PrefabPath = "Assets/Res/Prefabs/Effects/Aura/fx_field_generator_v1.prefab";
    public const string DefinitionPath = "Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_FieldGenerator.asset";
    const string Output = "ArtOutput/VFX/FieldGeneratorV1/";

    [MenuItem("工具/VFX/Build Field Generator Presentation V1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var recipe = ReadRecipe();
        string definitionBefore = File.ReadAllText(DefinitionPath);
        foreach (var path in new[] { AtlasPath, MaterialPath, RingMaterialPath, MeshPath, PrefabPath }) Directory.CreateDirectory(Path.GetDirectoryName(path));
        Directory.CreateDirectory(Output + "Audit"); Pack(recipe); AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Multiple; ti.spritePixelsPerUnit = recipe.pixelsPerUnit;
        ti.sRGBTexture = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false; ti.isReadable = false; ti.maxTextureSize = 512; ti.npotScale = TextureImporterNPOTScale.None;
        ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings(); ti.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(settings);
        ti.spritesheet = new[] {
            new SpriteMetaData { name="aura_gangqi_normal_v1", rect=new Rect(0,0,256,256), alignment=(int)SpriteAlignment.Center, pivot=new Vector2(.5f,.5f) },
            new SpriteMetaData { name="aura_gangqi_breakthrough_v1", rect=new Rect(256,0,256,256), alignment=(int)SpriteAlignment.Center, pivot=new Vector2(.5f,.5f) }
        };
        var web = ti.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = 512;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched; web.crunchedCompression = true; web.compressionQuality = recipe.compressionQuality; ti.SetPlatformTextureSettings(web); ti.SaveAndReimport();
        var material = Material(MaterialPath); material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        var ringMaterial = Material(RingMaterialPath); ringMaterial.mainTexture = null; EditorUtility.SetDirty(ringMaterial); AssetDatabase.SaveAssetIfDirty(ringMaterial);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath); var built = FieldGeneratorPresentation.CreateRingMesh();
        if (mesh == null) { mesh = built; AssetDatabase.CreateAsset(mesh, MeshPath); }
        else { EditorUtility.CopySerialized(built, mesh); UnityEngine.Object.DestroyImmediate(built); EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        var root = new GameObject("fx_field_generator_v1"); GameObject prefab;
        try
        {
            var boundary = FieldGeneratorPresentation.MakeRing(root.transform, "Boundary", mesh, ringMaterial);
            var pulse = FieldGeneratorPresentation.MakeRing(root.transform, "Pulse", mesh, ringMaterial);
            var flow = new GameObject("RotatingQi"); flow.transform.SetParent(root.transform, false);
            var renderer = flow.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
            var presentation = root.AddComponent<FieldGeneratorPresentation>();
            presentation.Author(sprites.Single(s => s.name == "aura_gangqi_normal_v1"), sprites.Single(s => s.name == "aura_gangqi_breakthrough_v1"), renderer, boundary, pulse, recipe.normalRotationSpeed, recipe.breakthroughRotationSpeed);
            presentation.Configure(1.25f, .7f, 1.4f, false, recipe.sortingOrder, recipe.visibleArtworkDiameter);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var def = AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        def.fieldVisualPrefab = prefab; def.visualBaseDiameter = recipe.visibleArtworkDiameter; def.sortingOrder = recipe.sortingOrder; EditorUtility.SetDirty(def); AssetDatabase.SaveAssetIfDirty(def);
        // Keep every gameplay field and original line ending; only substitute three presentation fields.
        string after = File.ReadAllText(DefinitionPath); const string pattern = @"(?m)^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\r\n]*";
        string final = Regex.Replace(definitionBefore, pattern, m => Regex.Match(after, @"(?m)^  " + m.Groups[1].Value + @":[^\r\n]*").Value);
        File.WriteAllText(DefinitionPath, final); AssetDatabase.ImportAsset(DefinitionPath, ImportAssetOptions.ForceUpdate);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        File.WriteAllText(Output + "Audit/resources.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
            sourcePngBytes = new FileInfo(AtlasPath).Length, texture.width, texture.height, format = texture.format.ToString(),
            etc2BlockBytesWithoutMipmaps = 512 * 256, editorTextureMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            sprites = 2, renderers = 3, materials = 2, sharedMeshes = 1, trianglesPerAura = mesh.triangles.Length / 3 * 2 + 2,
            runtimeTextureGeneration = false, perFrameObjectCreation = false, recipe.sortingOrder, recipe.visibleArtworkDiameter, recipe.pngBudgetKiB,
            dependencies = AssetDatabase.GetDependencies(PrefabPath, true), note = "Persistent aura, not pooled impact. Editor memory/source PNG/ETC2 bytes are not final WeChat upload size."
        }, Newtonsoft.Json.Formatting.Indented));
        Audit();
    }
    static Material Material(string path)
    {
        var shader = Shader.Find("Sprites/Default"); if (shader == null) throw new InvalidOperationException("Missing Sprites/Default");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.color = Color.white; return material;
    }
    static void Pack(FieldAuraRecipe recipe)
    {
        var atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false);
        try
        {
            atlas.SetPixels32(new Color32[512 * 256]);
            for (int cell=0;cell<2;cell++)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(cell == 0 ? recipe.normalSource : recipe.breakthroughSource))) throw new InvalidOperationException("Missing source artwork.");
                    if (source.width != source.height) throw new InvalidOperationException("Expected square centered aura source.");
                    var pixels = source.GetPixels32(); float half = source.width * .5f, extent = 0;
                    bool hasTransparent = false;
                    for (int y=0;y<source.height;y++) for (int x=0;x<source.width;x++)
                    {
                        var c = pixels[y * source.width + x]; if (c.a == 0) hasTransparent = true;
                        if (c.a > 16) extent = Mathf.Max(extent, new Vector2(x + .5f - half, y + .5f - half).magnitude);
                    }
                    if (!hasTransparent || extent < 1) throw new InvalidOperationException("Source must contain real transparency and visible artwork.");
                    // Normalize the complete visible radial extent to 112px, adding a 16px gutter.
                    float sourcePerPixel = extent / 112f;
                    for (int y=0;y<224;y++) for (int x=0;x<224;x++)
                    {
                        float u = (half + (x + .5f - 112) * sourcePerPixel) / source.width;
                        float v = (half + (y + .5f - 112) * sourcePerPixel) / source.height;
                        if (u >= 0 && u <= 1 && v >= 0 && v <= 1) atlas.SetPixel(cell * 256 + 16 + x, 16 + y, source.GetPixelBilinear(u, v));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            atlas.Apply(); byte[] png = atlas.EncodeToPNG(); if (png.Length > recipe.pngBudgetKiB * 1024) throw new InvalidOperationException("Aura atlas exceeds PNG budget: " + png.Length);
            File.WriteAllBytes(AtlasPath, png);
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }
    public static FieldAuraRecipe ReadRecipe()
    {
        var r = Newtonsoft.Json.JsonConvert.DeserializeObject<FieldAuraRecipe>(File.ReadAllText(RecipePath), new Newtonsoft.Json.JsonSerializerSettings { MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Error });
        if (r == null || r.schemaVersion != 1 || r.mode != "persistent-aura" || r.id != "field_generator_v1" || r.prefabPath != PrefabPath || r.texturePath != AtlasPath || r.width != 512 || r.height != 256 || r.pixelsPerUnit != 256 || r.visibleArtworkDiameter != .875f)
            throw new InvalidOperationException("Persistent aura recipe layout/path does not match this builder.");
        if (!(r.normalRotationSpeed > 0 && r.normalRotationSpeed <= 180 && r.breakthroughRotationSpeed > 0 && r.breakthroughRotationSpeed <= 180) || r.pngBudgetKiB < 1 || r.pngBudgetKiB > 256 || r.compressionQuality < 0 || r.compressionQuality > 100 || r.sortingOrder >= 0)
            throw new InvalidOperationException("Invalid rotation, budget, compression or sorting in persistent recipe.");
        foreach (var path in new[] {r.normalSource, r.breakthroughSource}) if (path == null || !path.StartsWith("ArtOutput/VFX/") || path.Contains("..")) throw new InvalidOperationException("Invalid authored source path.");
        return r;
    }
    [MenuItem("工具/VFX/常驻特效/检查 V1 历史罡气配置")]
    public static void Audit()
    {
        var r = ReadRecipe(); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var view = prefab.GetComponent<FieldGeneratorPresentation>(); var so = new SerializedObject(view);
        var def = AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath); var ti = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        bool passed = view != null && prefab.GetComponentsInChildren<Renderer>().Length == 3 && prefab.GetComponentsInChildren<ParticleSystem>().Length == 0 && prefab.GetComponentsInChildren<Collider2D>().Length == 0 &&
            so.FindProperty("normalRotationSpeed").floatValue == r.normalRotationSpeed && so.FindProperty("breakthroughRotationSpeed").floatValue == r.breakthroughRotationSpeed &&
            def.fieldVisualPrefab == prefab && def.visualBaseDiameter == r.visibleArtworkDiameter && def.sortingOrder == r.sortingOrder && new FileInfo(AtlasPath).Length <= r.pngBudgetKiB * 1024 &&
            !ti.mipmapEnabled && !ti.isReadable && ti.spritePixelsPerUnit == r.pixelsPerUnit && ti.GetPlatformTextureSettings("WebGL").compressionQuality == r.compressionQuality;
        Directory.CreateDirectory(Output + "Audit"); File.WriteAllText(Output + "Audit/configuration.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, recipe = r, checkedUtc = DateTime.UtcNow }, Newtonsoft.Json.Formatting.Indented));
        if (!passed) throw new InvalidOperationException("Persistent aura configuration drift; inspect configuration.json.");
    }
}
[Serializable] public sealed class FieldAuraRecipe
{
    public int schemaVersion, width, height, pixelsPerUnit, pngBudgetKiB, compressionQuality, sortingOrder;
    public string id, mode, displayName, status, prefabPath, texturePath, normalSource, breakthroughSource, briefPath, promptPath, validationReport;
    public float visibleArtworkDiameter, normalRotationSpeed, breakthroughRotationSpeed;
}
#endif
