#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Pack four authored animations into one small runtime atlas and bind existing damage events.</summary>
public static class ExplosionLibraryV2Builder
{
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_explosion_library_v2.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_explosion_library_v2.mat";
    public const string Prefix = "Assets/Res/Prefabs/Effects/Impact/fx_explosion_";
    static readonly string[] Names = { "compact", "chain", "missile", "heavy" };
    static readonly float[] Durations = { .40f, .30f, .32f, .48f };
    static readonly float[] Sizes = { 4f, 3.2f, 3.3f, 3.3f };
    const string Sources = "ArtOutput/VFX/ExplosionLibraryV2/Sources/";

    [MenuItem("工具/VFX/Build Shared Explosion Library V2")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before rebuilding explosion assets.");
        Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        Pack();
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
        var web = importer.GetPlatformTextureSettings("WebGL");
        web.overridden = true;
        web.maxTextureSize = 1024;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched;
        web.textureCompression = TextureImporterCompression.Compressed;
        web.crunchedCompression = true;
        web.compressionQuality = 60;
        importer.SetPlatformTextureSettings(web);
        importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        var prefabs = new GameObject[4];
        for (int i = 0; i < 4; i++) prefabs[i] = MakePrefab(Names[i], i, Durations[i], Sizes[i], material);
        var echo = MakePrefab("echo", 1, .22f, Sizes[1], material);
        BindGrenade("SkillDef_ThrowGrenade", prefabs[0]);
        BindGrenade("SkillDef_BouncingGrenade", prefabs[1]);
        SetPrefabReference("Assets/Prefab/Combat/Grenade.prefab", typeof(GrenadeProjectile), "defaultExplosionFxPrefab", prefabs[0]);
        SetPrefabReference("Assets/Prefab/Combat/PandaGrenade.prefab", typeof(GrenadeProjectile), "defaultExplosionFxPrefab", prefabs[1]);
        SetPrefabReference("Assets/Prefab/Combat/PandaGrenade.prefab", typeof(BouncingGrenade), "chainExplosionFxPrefab", echo);
        BindMissile("SkillDef_HomingMissileBasic", prefabs[2]);
        BindMissile("SkillDef_HomingMissile", prefabs[3]);
        SetPrefabReference("Assets/Prefab/Combat/playerBullet/HomingMissileMaxBullet.prefab", typeof(HomingMissileBullet), "explosionFxPrefab", prefabs[3]);
        AssetDatabase.SaveAssets();
        WriteAudit();
    }

    static void Pack()
    {
        // Texture packing/downsampling only: generated art remains untouched in Sources/.
        // 32 keyframes, 128px each. Keep source art and select eight phases per variant.
        // A 12px empty gutter is added inside each cell.
        var selected = new[] { 0, 2, 4, 6, 8, 10, 12, 15 };
        var atlas = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
        try
        {
            atlas.SetPixels32(new Color32[1024 * 512]);
            for (int variant = 0; variant < 4; variant++)
            {
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(Sources + Names[variant] + ".png"))) throw new InvalidOperationException("Cannot load " + Names[variant]);
                    if (source.width != source.height) throw new InvalidOperationException("Expected a square 4x4 source atlas.");
                    for (int keyframe = 0; keyframe < 8; keyframe++)
                    {
                        int frame = selected[keyframe];
                        int dx = keyframe * 128 + 12;
                        int dy = 512 - (variant + 1) * 128 + 12;
                        for (int y = 0; y < 104; y++) for (int x = 0; x < 104; x++)
                        {
                            float u = ((frame % 4) + (x + .5f) / 104f) / 4f;
                            float v = (3 - frame / 4 + (y + .5f) / 104f) / 4f;
                            atlas.SetPixel(dx + x, dy + y, source.GetPixelBilinear(u, v));
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
            atlas.Apply(false, false);
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            if (new FileInfo(AtlasPath).Length > 512 * 1024) throw new InvalidOperationException("Atlas exceeds the 512 KiB source-PNG budget.");
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); }
    }

    static GameObject MakePrefab(string name, int variant, float baseDuration, float size, Material material)
    {
        float duration = baseDuration * CombatVfxTimingProfile.DurationScale;
        var root = new GameObject("fx_explosion_" + name + "_v2");
        try
        {
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration; main.loop = false; main.playOnAwake = false;
            main.startLifetime = duration; main.startSpeed = 0; main.startSize = size;
            main.startRotation = 0; main.startColor = Color.white; main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.useUnscaledTime = false;
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(6179 + variant);
            var emission = ps.emission;
            emission.enabled = true; emission.rateOverTime = 0; emission.rateOverDistance = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape; shape.enabled = false;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 8; sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            float start = variant * 8f;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0, start / 32f), new Keyframe(.18f, (start + 2) / 32f),
                new Keyframe(.55f, (start + 5) / 32f), new Keyframe(1, (start + 7.999f) / 32f)));
            sheet.startFrame = 0; sheet.cycleCount = 1;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .28f), new GradientAlphaKey(.85f, .52f), new GradientAlphaKey(.42f, .78f), new GradientAlphaKey(0, 1) });
            var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View; renderer.sortingOrder = -5; renderer.maxParticleSize = 1;
            var pooled = root.AddComponent<PooledCombatVfx>();
            var serialized = new SerializedObject(pooled);
            var particles = serialized.FindProperty("_particles"); particles.arraySize = 1;
            particles.GetArrayElementAtIndex(0).objectReferenceValue = ps;
            serialized.FindProperty("lifetimeOverride").floatValue = (baseDuration + .04f) * CombatVfxTimingProfile.DurationScale;
            serialized.FindProperty("sortingOrder").intValue = -5;
            serialized.FindProperty("referenceRadius").floatValue = 1.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, Prefix + name + "_v2.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void BindGrenade(string name, GameObject vfx)
    {
        var definition = AssetDatabase.LoadAssetAtPath<GrenadeSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/" + name + ".asset");
        if (definition == null) throw new InvalidOperationException("Missing " + name);
        definition.explosionFxPrefab = vfx; EditorUtility.SetDirty(definition);
    }
    static void BindMissile(string name, GameObject vfx)
    {
        var definition = AssetDatabase.LoadAssetAtPath<HomingMissileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/" + name + ".asset");
        if (definition == null) throw new InvalidOperationException("Missing " + name);
        definition.explosionFxPrefab = vfx; EditorUtility.SetDirty(definition);
        string primary = AssetDatabase.GetAssetPath(definition.missilePrefab);
        SetPrefabReference(primary, typeof(HomingMissileBullet), "explosionFxPrefab", vfx);
        string max = primary.Replace("Bullet.prefab", "MaxBullet.prefab");
        if (File.Exists(max)) SetPrefabReference(max, typeof(HomingMissileBullet), "explosionFxPrefab", vfx);
        if (definition.maxLevelPrefab != null) SetPrefabReference(AssetDatabase.GetAssetPath(definition.maxLevelPrefab), typeof(HomingMissileBullet), "explosionFxPrefab", vfx);
    }
    static void SetPrefabReference(string path, Type component, string field, GameObject vfx)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var target = root.GetComponent(component);
            if (target == null) throw new InvalidOperationException("Missing " + component.Name + " in " + path);
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing " + field + " in " + path);
            property.objectReferenceValue = vfx; serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void WriteAudit()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var rows = Names.Select((name, i) => new { name, firstFrame = i * 8, frameCount = 8, duration = Durations[i] * CombatVfxTimingProfile.DurationScale, cellPixels = 128 }).ToArray();
        var report = new { atlas = AtlasPath, sourceBytes = new FileInfo(AtlasPath).Length, width = texture.width, height = texture.height,
            format = texture.format.ToString(), mipCount = texture.mipmapCount, texture.isReadable,
            gpuBlockBytesWithoutMipmaps = 1024 * 512, runtimeTextureBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            sharedMaterialCount = 1, atlasCount = 1, variants = rows,
            note = "Source PNG bytes, ETC2 block estimate and Editor texture memory are separate measures; none is the final WeChat upload increment." };
        File.WriteAllText("ArtOutput/VFX/ExplosionLibraryV2/Audit/after-textures.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
    }
}
#endif
