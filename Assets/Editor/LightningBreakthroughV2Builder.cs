#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class LightningBreakthroughV2Builder
{
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_lightning_breakthrough_v2.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_lightning_breakthrough_v2.mat";
    public const string PrefabPath = "Assets/Res/Prefabs/Effects/Impact/fx_lightning_breakthrough_v2.prefab";
    const string Folder = "ArtOutput/VFX/LightningBreakthroughV2/";
    [MenuItem("工具/VFX/Build Red Lightning Breakthrough V2")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding red lightning.");
        string definitionBefore = File.ReadAllText(LightningPresentationV1Builder.DefinitionPath);
        Pack(); AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 1024;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        var web = importer.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = 1024;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched; web.crunchedCompression = true; web.compressionQuality = 70;
        importer.SetPlatformTextureSettings(web); importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find("Sprites/Default"); if (shader == null) throw new InvalidOperationException("Missing Sprites/Default.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath); material.color = Color.white; EditorUtility.SetDirty(material);
        var root = new GameObject("fx_lightning_breakthrough_v2"); GameObject prefab;
        try
        {
            var bolt = LightningPresentationV1Builder.Layer(root, "ScarletCelestialBolt", material, .18f, new Vector2(3.10f, 5.2f), 2.11f, 0, 4601);
            var contact = LightningPresentationV1Builder.Layer(root, "CrimsonContactArcs", material, .32f, new Vector2(2.8f, 5.6f), 0, .5f, 4602);
            var pooled = root.AddComponent<PooledCombatVfx>(); var so = new SerializedObject(pooled);
            var particles = so.FindProperty("_particles"); particles.arraySize = 2;
            particles.GetArrayElementAtIndex(0).objectReferenceValue = bolt;
            particles.GetArrayElementAtIndex(1).objectReferenceValue = contact;
            so.FindProperty("lifetimeOverride").floatValue = .36f * CombatVfxTimingProfile.DurationScale; so.FindProperty("sortingOrder").intValue = -5; so.FindProperty("referenceRadius").floatValue = 1.1f;
            so.ApplyModifiedPropertiesWithoutUndo(); prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var def = AssetDatabase.LoadAssetAtPath<LightningStrikeSkillDefinition>(LightningPresentationV1Builder.DefinitionPath);
        def.maxLevelPrefab = prefab; EditorUtility.SetDirty(def); AssetDatabase.SaveAssets();
        const string pattern = @"(?m)^  maxLevelPrefab:[^\r\n]*";
        string reference = Regex.Match(File.ReadAllText(LightningPresentationV1Builder.DefinitionPath), pattern).Value;
        if (!Regex.IsMatch(definitionBefore, pattern)) throw new InvalidOperationException("Expected the existing breakthrough binding.");
        File.WriteAllText(LightningPresentationV1Builder.DefinitionPath, Regex.Replace(definitionBefore, pattern, m => reference));
        AssetDatabase.ImportAsset(LightningPresentationV1Builder.DefinitionPath, ImportAssetOptions.ForceUpdate);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        File.WriteAllText(Folder + "Audit/resources.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
            width = texture.width, height = texture.height, sourcePngBytes = new FileInfo(AtlasPath).Length,
            format = texture.format.ToString(), gpuBlockBytesWithoutMipmaps = 1024 * 512,
            editorTextureMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            particleSystems = 2, maximumParticlesPerStrike = 2, materials = 1, framesPerLayer = 8,
            boltSeconds = .18f * CombatVfxTimingProfile.DurationScale, residualSeconds = .32f * CombatVfxTimingProfile.DurationScale, pooledLifetimeSeconds = .36f * CombatVfxTimingProfile.DurationScale,
            dependencies = AssetDatabase.GetDependencies(PrefabPath, true), note = "Dedicated authored red artwork; source bytes and ETC2 block data are not final WeChat package size."
        }, Newtonsoft.Json.Formatting.Indented));
    }
    static void Pack()
    {
        // Only resampling and atlas packing; generated source artwork is preserved unchanged.
        var atlas = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
        try
        {
            atlas.SetPixels32(new Color32[1024 * 512]);
            for (int layer = 0; layer < 2; layer++)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(Folder + "Sources/" + (layer == 0 ? "bolt" : "ground") + ".png"))) throw new InvalidOperationException("Missing red animation source.");
                    for (int frame = 0; frame < 8; frame++)
                    {
                        int width = 108, height = layer == 0 ? 224 : 108;
                        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        {
                            float u = (frame % 4 + (x + .5f) / width) / 4;
                            float v = (1 - frame / 4 + (y + .5f) / height) / 2;
                            atlas.SetPixel(frame * 128 + (128 - width) / 2 + x, (layer == 0 ? 256 : 0) + (256 - height) / 2 + y, source.GetPixelBilinear(u, v));
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            atlas.Apply(false, false); File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            if (new FileInfo(AtlasPath).Length > 256 * 1024) throw new InvalidOperationException("Red lightning PNG exceeds 256 KiB source budget.");
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }
}
#endif
