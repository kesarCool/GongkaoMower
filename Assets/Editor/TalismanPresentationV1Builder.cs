#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class TalismanPresentationV1Builder
{
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_talisman_presentation_v1.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_talisman_presentation_v1.mat";
    public const string TrailMaterialPath = "Assets/Res/Materials/VFX/mat_talisman_trail_v1.mat";
    public const string HitPath = "Assets/Res/Prefabs/Effects/Impact/fx_talisman_hit_v1.prefab";
    static readonly string[] Paths = {
        "Assets/Prefab/Combat/playerBullet/BulletStraightTalisman.prefab",
        "Assets/Prefab/Combat/playerBullet/BulletStraightTalismanMax.prefab",
        "Assets/Prefab/Combat/playerBullet/BurstBulletTailman.prefab",
        "Assets/Resources/Traits/TalismanOrbit.prefab" };

    [MenuItem("工具/VFX/Build Talisman Presentation V1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building talisman assets.");
        Pack(); AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 180; importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.spritesheet = new[] { new SpriteMetaData { name = "fulu_contrast_v1", rect = new Rect(0, 0, 64, 128), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) } };
        var web = importer.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = 512;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched; web.crunchedCompression = true; web.compressionQuality = 65;
        importer.SetPlatformTextureSettings(web); importer.SaveAndReimport();
        var material = GetMaterial(MaterialPath, "Sprites/Default");
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath); material.color = Color.white; EditorUtility.SetDirty(material);
        var trailMaterial = GetMaterial(TrailMaterialPath, "GongkaoMower/TalismanTrailSoft");
        var hit = MakeHit(material);
        var sprite = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().Single();
        for (int i = 0; i < Paths.Length; i++) Configure(Paths[i], sprite, material, trailMaterial, hit, i);
        AssetDatabase.SaveAssets();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        File.WriteAllText("ArtOutput/VFX/TalismanPresentationV1/Audit/resources.json", Newtonsoft.Json.JsonConvert.SerializeObject(new {
            sourceBytes = new FileInfo(AtlasPath).Length, texture.width, texture.height, format = texture.format.ToString(),
            gpuBlockBytesWithoutMipmaps = 512 * 256, runtimeTextureBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            materialCount = 2, addedTrailTextureCount = 0, hitFrames = 8, paths = Paths,
            note = "Source PNG, ETC2 block bytes and Editor texture memory are separate measures, not final WeChat upload size."
        }, Newtonsoft.Json.Formatting.Indented));
    }
    static Material GetMaterial(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName); if (shader == null) throw new InvalidOperationException("Missing " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; EditorUtility.SetDirty(material); return material;
    }
    static void Pack()
    {
        var atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false);
        var paper = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var hit = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            atlas.SetPixels32(new Color32[512 * 256]);
            if (!paper.LoadImage(File.ReadAllBytes("ArtOutput/VFX/TalismanPresentationV1/Sources/paper.png")) ||
                !hit.LoadImage(File.ReadAllBytes("ArtOutput/VFX/TalismanPresentationV1/Sources/hit.png"))) throw new InvalidOperationException("Missing generated source images.");
            var pixels = paper.GetPixels32(); int minX = paper.width, minY = paper.height, maxX = 0, maxY = 0;
            for (int y = 0; y < paper.height; y++) for (int x = 0; x < paper.width; x++) if (pixels[y * paper.width + x].a > 16)
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            int sw = maxX - minX + 1, sh = maxY - minY + 1; float scale = Mathf.Min(56f / sw, 116f / sh);
            int width = Mathf.Max(1, Mathf.RoundToInt(sw * scale)), height = Mathf.Max(1, Mathf.RoundToInt(sh * scale));
            int dx = (64 - width) / 2, dy = (128 - height) / 2;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                atlas.SetPixel(dx + x, dy + y, paper.GetPixelBilinear((minX + (x + .5f) / width * sw) / paper.width, (minY + (y + .5f) / height * sh) / paper.height));
            var selected = new[] { 0, 2, 4, 6, 8, 10, 12, 15 };
            for (int key = 0; key < 8; key++) for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
            {
                int frame = selected[key]; float u = ((frame % 4) + (x + .5f) / 48) / 4;
                float v = (3 - frame / 4 + (y + .5f) / 48) / 4;
                atlas.SetPixel(key * 64 + 8 + x, 200 + y, hit.GetPixelBilinear(u, v));
            }
            atlas.Apply(false, false); File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            if (new FileInfo(AtlasPath).Length > 96 * 1024) throw new InvalidOperationException("Talisman atlas exceeds 96 KiB source budget.");
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(paper); UnityEngine.Object.DestroyImmediate(hit); }
    }
    static GameObject MakeHit(Material material)
    {
        var go = new GameObject("fx_talisman_hit_v1");
        try
        {
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float duration = .18f * CombatVfxTimingProfile.DurationScale;
            var main = ps.main; main.duration = duration; main.startLifetime = duration; main.loop = false; main.playOnAwake = false;
            main.startSpeed = 0; main.startSize = 1.5f; main.maxParticles = 1; main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.useUnscaledTime = false;
            ps.useAutoRandomSeed = false; ps.randomSeed = 8531;
            var shape = ps.shape; shape.enabled = false;
            var emission = ps.emission; emission.rateOverTime = 0; emission.rateOverDistance = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 8; sheet.numTilesY = 4; sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 7.999f / 32)); sheet.startFrame = 0; sheet.cycleCount = 1;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .35f), new GradientAlphaKey(0, 1) });
            var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -5; renderer.maxParticleSize = 1;
            var pooled = go.AddComponent<PooledCombatVfx>(); var serialized = new SerializedObject(pooled);
            var particles = serialized.FindProperty("_particles"); particles.arraySize = 1; particles.GetArrayElementAtIndex(0).objectReferenceValue = ps;
            serialized.FindProperty("lifetimeOverride").floatValue = .22f * CombatVfxTimingProfile.DurationScale; serialized.FindProperty("sortingOrder").intValue = -5; serialized.FindProperty("referenceRadius").floatValue = .45f;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return PrefabUtility.SaveAsPrefabAsset(go, HitPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    static void Configure(string path, Sprite sprite, Material material, Material trailMaterial, GameObject hit, int index)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sprite = sprite; sr.sharedMaterial = material; sr.color = Color.white;
                sr.transform.localScale = Vector3.one * (index == 1 ? 1.08f : index == 2 ? .96f : 1f);
            }
            foreach (var old in root.GetComponentsInChildren<TrailRenderer>(true)) UnityEngine.Object.DestroyImmediate(old);
            if (index == 1 || index == 2)
            {
                MakeTrail(root, "TalismanTrailOuter", trailMaterial, index == 1 ? .12f : .10f, .115f, false);
                MakeTrail(root, "TalismanTrailCore", trailMaterial, index == 1 ? .10f : .085f, .035f, true);
            }
            var feedback = root.GetComponent<ProjectileHitFeedback>(); if (feedback == null) feedback = root.AddComponent<ProjectileHitFeedback>();
            var serialized = new SerializedObject(feedback); serialized.FindProperty("hitPrefab").objectReferenceValue = hit; serialized.FindProperty("visualRadius").floatValue = index == 1 ? .50f : .45f;
            serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void MakeTrail(GameObject root, string name, Material material, float time, float width, bool core)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        var trail = go.AddComponent<TrailRenderer>(); trail.sharedMaterial = material; trail.time = time; trail.widthMultiplier = width;
        trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.45f, .65f), new Keyframe(1, 0));
        var gradient = new Gradient(); gradient.SetKeys(
            new[] { new GradientColorKey(core ? new Color(1,.86f,.42f) : new Color(.08f,.62f,.77f), 0), new GradientColorKey(core ? new Color(1,.32f,.1f) : new Color(.08f,.16f,.45f), 1) },
            new[] { new GradientAlphaKey(core ? .95f : .78f, 0), new GradientAlphaKey(.40f, .55f), new GradientAlphaKey(0, 1) });
        trail.colorGradient = gradient; trail.minVertexDistance = .035f; trail.numCornerVertices = 2; trail.numCapVertices = 3;
        trail.alignment = LineAlignment.View; trail.textureMode = LineTextureMode.Stretch; trail.autodestruct = false; trail.emitting = true;
        trail.sortingOrder = core ? -3 : -4; trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false; trail.generateLightingData = false;
    }
}
#endif
