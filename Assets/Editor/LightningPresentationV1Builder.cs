#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LightningPresentationV1Builder
{
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_lightning_v1.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_lightning_v1.mat";
    public const string PrefabPath = "Assets/Res/Prefabs/Effects/Impact/fx_lightning_v1.prefab";
    public const string DefinitionPath = "Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LightningStrike.asset";
    const string Folder = "ArtOutput/VFX/LightningPresentationV1/";

    [MenuItem("工具/VFX/Build Lightning Presentation V1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding lightning.");
        string definitionBefore = File.ReadAllText(DefinitionPath);
        Pack(); AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        var web = importer.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = 512;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched; web.crunchedCompression = true; web.compressionQuality = 65;
        importer.SetPlatformTextureSettings(web); importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find("Sprites/Default"); if (shader == null) throw new InvalidOperationException("Missing Sprites/Default.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader; material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath); material.color = Color.white; EditorUtility.SetDirty(material);
        var root = new GameObject("fx_lightning_v1");
        GameObject prefab;
        try
        {
            var bolt = Layer(root, "FlashBolt", material, .16f, new Vector2(1.65f, 4.6f), 1.88f, 0, 2109);
            var ground = Layer(root, "ContactAndResidual", material, .30f, new Vector2(2.8f, 5.6f), 0, .5f, 2110);
            var pooled = root.AddComponent<PooledCombatVfx>(); var so = new SerializedObject(pooled);
            var particles = so.FindProperty("_particles"); particles.arraySize = 2;
            particles.GetArrayElementAtIndex(0).objectReferenceValue = bolt; particles.GetArrayElementAtIndex(1).objectReferenceValue = ground;
            so.FindProperty("lifetimeOverride").floatValue = .34f * CombatVfxTimingProfile.DurationScale; so.FindProperty("sortingOrder").intValue = -5; so.FindProperty("referenceRadius").floatValue = 1.1f;
            so.ApplyModifiedPropertiesWithoutUndo(); prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var def = AssetDatabase.LoadAssetAtPath<LightningStrikeSkillDefinition>(DefinitionPath);
        def.strikeFxPrefab = prefab; EditorUtility.SetDirty(def); AssetDatabase.SaveAssets();
        // Preserve unrelated fields and line endings; Unity may materialize inherited default fields when saving.
        const string referencePattern = @"(?m)^  strikeFxPrefab:[^\r\n]*";
        string reference = Regex.Match(File.ReadAllText(DefinitionPath), referencePattern).Value;
        File.WriteAllText(DefinitionPath, Regex.Replace(definitionBefore, referencePattern, match => reference));
        AssetDatabase.ImportAsset(DefinitionPath, ImportAssetOptions.ForceUpdate);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        File.WriteAllText(Folder + "Audit/after-resources.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
            sourcePngBytes = new FileInfo(AtlasPath).Length, width = texture.width, height = texture.height, format = texture.format.ToString(),
            gpuBlockBytesWithoutMipmaps = 512 * 256, editorTextureMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            particleSystems = 2, maximumParticlesPerStrike = 2, materials = 1, framesPerLayer = 8,
            lightningSeconds = .16f * CombatVfxTimingProfile.DurationScale, residualSeconds = .30f * CombatVfxTimingProfile.DurationScale, pooledLifetimeSeconds = .34f * CombatVfxTimingProfile.DurationScale,
            dependencies = AssetDatabase.GetDependencies(PrefabPath, true), note = "Source bytes and ETC2 block bytes are not final WeChat package size; shared legacy resources have not been deleted."
        }, Newtonsoft.Json.Formatting.Indented));
    }
    internal static ParticleSystem Layer(GameObject root, string name, Material material, float lifetime, Vector2 size, float y, float startFrame, uint seed)
    {
        lifetime *= CombatVfxTimingProfile.DurationScale;
        var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.localPosition = Vector3.up * y;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.duration = lifetime; main.startLifetime = lifetime; main.loop = false; main.playOnAwake = false;
        main.startSpeed = 0; main.startSize3D = true; main.startSizeX = size.x; main.startSizeY = size.y; main.startSizeZ = 1;
        main.maxParticles = 1; main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.useUnscaledTime = false; ps.useAutoRandomSeed = false; ps.randomSeed = seed;
        var shape = ps.shape; shape.enabled = false;
        var emission = ps.emission; emission.rateOverTime = 0; emission.rateOverDistance = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
        var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 8; sheet.numTilesY = 2; sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 7.999f / 16)); sheet.startFrame = startFrame; sheet.cycleCount = 1;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -5;
        renderer.alignment = ParticleSystemRenderSpace.View; renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.maxParticleSize = 1;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return ps;
    }
    static void Pack()
    {
        var atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false);
        try
        {
            atlas.SetPixels32(new Color32[512 * 256]);
            for (int layer = 0; layer < 2; layer++)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(Folder + "Sources/" + (layer == 0 ? "bolt" : "ground") + ".png"))) throw new InvalidOperationException("Missing generated source.");
                    for (int frame = 0; frame < 8; frame++)
                    {
                        int width = layer == 0 ? 56 : 64, height = layer == 0 ? 116 : 64;
                        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        {
                            float u = (frame % 4 + (x + .5f) / width) / 4;
                            float v = (1 - frame / 4 + (y + .5f) / height) / 2;
                            atlas.SetPixel(frame * 64 + (64 - width) / 2 + x, (layer == 0 ? 128 : 0) + (128 - height) / 2 + y, source.GetPixelBilinear(u, v));
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            atlas.Apply(false, false); File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            if (new FileInfo(AtlasPath).Length > 128 * 1024) throw new InvalidOperationException("Lightning PNG exceeds 128 KiB source budget.");
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }
}
#endif
