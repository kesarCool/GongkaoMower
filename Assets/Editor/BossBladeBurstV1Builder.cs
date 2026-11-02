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

/// <summary>Builds the shared Boss blade art and attaches presentation without changing combat parameters.</summary>
public static class BossBladeBurstV1Builder
{
    public const string RecipePath = "Tools/VFX/Library/Boss/boss_blade_burst_v1.json";
    public const string AtlasPath = "Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_boss_blade_burst_v1.mat";
    public const string SolidMaterialPath = "Assets/Res/Materials/VFX/mat_boss_blade_solid_v1.mat";
    public const string TrailMaterialPath = "Assets/Res/Materials/VFX/mat_boss_blade_trail_v1.mat";
    public const string HitPath = "Assets/Res/Prefabs/Effects/Impact/fx_boss_blade_dissolve_v1.prefab";
    public const string ProjectilePath = "Assets/Prefab/Enemy/Boss/bossBullet/BladeBurstBulletV1.prefab";
    public const string OriginalProjectilePath = "Assets/Prefab/Enemy/Boss/bossBullet/StraightBullet.prefab";
    public const string Output = "ArtOutput/VFX/BossBladeBurstV1/Audit/";
    public const string RegistryKey = "BladeBurstBullet";
    public static readonly string[] BossPaths = {
        "Assets/Prefab/Enemy/Boss/boss_common.prefab", "Assets/Prefab/Enemy/Boss/boss_30001.prefab",
        "Assets/Prefab/Enemy/Boss/boss_30002.prefab", "Assets/Prefab/Enemy/Boss/boss_30003.prefab"
    };

    public static BossBladeBurstVisualRecipe ReadRecipe()
    {
        var recipe = JsonConvert.DeserializeObject<BossBladeBurstVisualRecipe>(File.ReadAllText(RecipePath),
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (recipe == null || recipe.schemaVersion != 1 || recipe.id != "boss_blade_burst_v1" ||
            recipe.mode != "boss-blade-burst" || recipe.texturePath != AtlasPath || recipe.materialPath != MaterialPath ||
            recipe.solidMaterialPath != SolidMaterialPath || recipe.trailMaterialPath != TrailMaterialPath ||
            recipe.hitPrefabPath != HitPath || recipe.projectilePath != ProjectilePath ||
            recipe.registryKey != RegistryKey || recipe.width != 512 || recipe.height != 256 || recipe.pixelsPerUnit != 256)
            throw new InvalidOperationException("Invalid Boss blade recipe identity/layout.");
        if (recipe.pngBudgetKiB < 1 || recipe.pngBudgetKiB > 96 || recipe.bladeScale <= 0f || recipe.bladeScale > 1f ||
            recipe.trailSeconds <= 0f || recipe.trailSeconds > .08f || recipe.trailWidth <= 0f || recipe.trailWidth > .12f ||
            recipe.hitSeconds < .1f || recipe.hitSeconds > .3f || recipe.poolSeconds < recipe.hitSeconds + .04f || recipe.poolSeconds > .4f ||
            recipe.hitRadius <= 0f || recipe.hitRadius > .4f || recipe.hitSize <= 0f || recipe.hitSize > .8f ||
            recipe.trailOrder >= recipe.spriteOrder || recipe.hitOrder >= recipe.spriteOrder || recipe.telegraphOrder >= recipe.spriteOrder)
            throw new InvalidOperationException("Boss blade recipe exceeds size/timing/layer budget.");
        foreach (var path in new[] { recipe.bladeSource, recipe.hitSource })
            if (string.IsNullOrEmpty(path) || !path.StartsWith("ArtOutput/VFX/BossBladeBurstV1/Sources/") || path.Contains(".."))
                throw new InvalidOperationException("Invalid Boss blade source path.");
        return recipe;
    }

    [MenuItem("工具/VFX/Build Boss Blade Burst V1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var recipe = ReadRecipe();
        Directory.CreateDirectory(Output);
        foreach (var path in new[] { AtlasPath, MaterialPath, SolidMaterialPath, TrailMaterialPath, HitPath, ProjectilePath })
            Directory.CreateDirectory(Path.GetDirectoryName(path));
        Pack(recipe);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(recipe);
        var bladeMaterial = GetMaterial(MaterialPath, "Sprites/Default");
        bladeMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        bladeMaterial.color = Color.white;
        EditorUtility.SetDirty(bladeMaterial);
        AssetDatabase.SaveAssetIfDirty(bladeMaterial);
        var solidMaterial = GetMaterial(SolidMaterialPath, "Sprites/Default");
        solidMaterial.mainTexture = null;
        solidMaterial.color = Color.white;
        EditorUtility.SetDirty(solidMaterial);
        AssetDatabase.SaveAssetIfDirty(solidMaterial);
        var trailMaterial = GetMaterial(TrailMaterialPath, "GongkaoMower/TalismanTrailSoft");
        AssetDatabase.SaveAssetIfDirty(trailMaterial);
        var hit = BuildHit(recipe, bladeMaterial);
        var blade = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().Single(s => s.name == "boss_blade_burst_v1");
        var projectile = ConfigureProjectile(blade, bladeMaterial, trailMaterial, hit, recipe);
        foreach (var path in BossPaths) ConfigureBoss(path, projectile, blade, solidMaterial, bladeMaterial, recipe);
        Audit();
    }

    static Material GetMaterial(string path, string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing " + shaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigureImporter(BossBladeBurstVisualRecipe recipe)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = recipe.pixelsPerUnit;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 512;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.spritesheet = new[] {
            new SpriteMetaData { name = "boss_blade_burst_v1", rect = new Rect(0, 0, 256, 128),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) }
        };
        var web = importer.GetPlatformTextureSettings("WebGL");
        web.overridden = true;
        web.maxTextureSize = 512;
        web.format = TextureImporterFormat.ETC2_RGBA8Crunched;
        web.crunchedCompression = true;
        web.compressionQuality = 70;
        importer.SetPlatformTextureSettings(web);
        importer.SaveAndReimport();
    }

    static void Pack(BossBladeBurstVisualRecipe recipe)
    {
        var atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false);
        var blade = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var hits = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!blade.LoadImage(File.ReadAllBytes(recipe.bladeSource)) || !hits.LoadImage(File.ReadAllBytes(recipe.hitSource)))
                throw new InvalidOperationException("Missing generated Boss blade sources.");
            atlas.SetPixels32(new Color32[512 * 256]);
            var pixels = blade.GetPixels32();
            int minX = blade.width, minY = blade.height, maxX = -1, maxY = -1;
            for (int y = 0; y < blade.height; y++) for (int x = 0; x < blade.width; x++)
                if (pixels[y * blade.width + x].a > 16)
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            if (maxX < minX || maxY < minY) throw new InvalidOperationException("Empty Boss blade source.");
            int left = Mathf.Max(0, minX - 3), bottom = Mathf.Max(0, minY - 3);
            var bounds = new RectInt(left, bottom, Mathf.Min(blade.width - 1, maxX + 3) - left + 1,
                Mathf.Min(blade.height - 1, maxY + 3) - bottom + 1);
            float scale = Mathf.Min(228f / bounds.width, 112f / bounds.height);
            int width = Mathf.RoundToInt(bounds.width * scale), height = Mathf.RoundToInt(bounds.height * scale);
            int dx = (256 - width) / 2, dy = (128 - height) / 2;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                atlas.SetPixel(dx + x, dy + y, blade.GetPixelBilinear(
                    (bounds.x + (x + .5f) / width * bounds.width) / blade.width,
                    (bounds.y + (y + .5f) / height * bounds.height) / blade.height));
            // Keep frame centres and authored timing. Each 64 px cell has a 4 px transparent gutter.
            for (int frame = 0; frame < 8; frame++) for (int y = 0; y < 56; y++) for (int x = 0; x < 56; x++)
                atlas.SetPixel(frame * 64 + 4 + x, 196 + y, hits.GetPixelBilinear(
                    (frame % 4 + (x + .5f) / 56) / 4, (1 - frame / 4 + (y + .5f) / 56) / 2));
            atlas.Apply();
            byte[] png = atlas.EncodeToPNG();
            if (png.Length > recipe.pngBudgetKiB * 1024) throw new InvalidOperationException("Boss blade atlas exceeds PNG budget.");
            File.WriteAllBytes(AtlasPath, png);
            File.WriteAllText(Output + "packing.json", JsonConvert.SerializeObject(new {
                bladeBounds = bounds.ToString(), visibleWidth = width, visibleHeight = height, runtimeWidth = 512,
                runtimeHeight = 256, frames = 8, sourceCellOrder = "4x2 top-left first", gutterPixels = 4,
                reservedBottomRight = true, pngBytes = png.Length
            }, Formatting.Indented));
        }
        finally { UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(blade); UnityEngine.Object.DestroyImmediate(hits); }
    }

    static GameObject BuildHit(BossBladeBurstVisualRecipe recipe, Material material)
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath) != null;
        var root = existing ? PrefabUtility.LoadPrefabContents(HitPath) : new GameObject("fx_boss_blade_dissolve_v1");
        try
        {
            var ps = root.GetComponent<ParticleSystem>();
            if (ps == null) ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = recipe.hitSeconds;
            main.startLifetime = recipe.hitSeconds;
            main.startSpeed = 0;
            main.startSize = recipe.hitSize;
            main.startColor = Color.white;
            main.maxParticles = 1;
            main.loop = false;
            main.playOnAwake = false;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 18723;
            var shape = ps.shape; shape.enabled = false;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.rateOverDistance = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 8;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 7.999f / 32));
            sheet.startFrame = 0;
            sheet.cycleCount = 1;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) });
            var color = ps.colorOverLifetime; color.enabled = true; color.color = fade;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = recipe.hitOrder;
            renderer.maxParticleSize = 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var pool = root.GetComponent<PooledCombatVfx>();
            if (pool == null) pool = root.AddComponent<PooledCombatVfx>();
            var data = new SerializedObject(pool);
            data.FindProperty("_particles").arraySize = 1;
            data.FindProperty("_particles").GetArrayElementAtIndex(0).objectReferenceValue = ps;
            data.FindProperty("lifetimeOverride").floatValue = recipe.poolSeconds;
            data.FindProperty("sortingOrder").intValue = recipe.hitOrder;
            data.FindProperty("referenceRadius").floatValue = recipe.hitRadius;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, HitPath);
        }
        finally { if (existing) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }

    static GameObject ConfigureProjectile(Sprite blade, Material material, Material trailMaterial, GameObject hit, BossBladeBurstVisualRecipe recipe)
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null;
        var root = PrefabUtility.LoadPrefabContents(existing ? ProjectilePath : OriginalProjectilePath);
        try
        {
            root.name = "BladeBurstBulletV1";
            // Only the visual child is scaled. The original .4 collider and root transform stay unchanged.
            var visual = FindOrCreate(root.transform, "BladeVisual");
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            foreach (var previous in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (previous != renderer) UnityEngine.Object.DestroyImmediate(previous);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(recipe.bladeScale, recipe.bladeScale, 1);
            renderer.enabled = true;
            renderer.sprite = blade;
            renderer.sharedMaterial = material;
            renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false;
            renderer.sortingOrder = recipe.spriteOrder;
            var wake = FindOrCreate(root.transform, "BladeWake");
            wake.localPosition = new Vector3(-.2f, 0, 0);
            wake.localRotation = Quaternion.identity;
            wake.localScale = Vector3.one;
            var trail = wake.GetComponent<TrailRenderer>();
            if (trail == null) trail = wake.gameObject.AddComponent<TrailRenderer>();
            foreach (var previous in root.GetComponentsInChildren<TrailRenderer>(true))
                if (previous != trail) UnityEngine.Object.DestroyImmediate(previous);
            trail.sharedMaterial = trailMaterial;
            trail.time = recipe.trailSeconds;
            trail.widthMultiplier = recipe.trailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.4f, .65f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .91f, .64f), 0),
                    new GradientColorKey(new Color(1, .29f, .055f), .35f), new GradientColorKey(new Color(.29f, .025f, .03f), 1) },
                new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.45f, .5f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            trail.minVertexDistance = .06f;
            trail.numCornerVertices = 1;
            trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.autodestruct = false;
            trail.emitting = true;
            trail.sortingOrder = recipe.trailOrder;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;
            var presentation = root.GetComponent<BossBladeProjectilePresentation>();
            if (presentation == null) presentation = root.AddComponent<BossBladeProjectilePresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("dissolvePrefab").objectReferenceValue = hit;
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Transform FindOrCreate(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    static void ConfigureBoss(string path, GameObject projectile, Sprite blade, Material solidMaterial, Material bladeMaterial, BossBladeBurstVisualRecipe recipe)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var brain = root.GetComponent<BossBrain>();
            if (brain == null) throw new InvalidOperationException("Missing BossBrain: " + path);
            var registry = brain.prefabRegistry == null ? new List<BossPrefabEntry>() : brain.prefabRegistry.ToList();
            var matches = registry.Where(e => e != null && string.Equals(e.key, RegistryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate Boss blade registry key: " + path);
            if (matches.Length == 0) registry.Add(new BossPrefabEntry { key = RegistryKey, prefab = projectile });
            else matches[0].prefab = projectile;
            brain.prefabRegistry = registry.ToArray();
            var visual = FindOrCreate(root.transform, "BossBladeBurstVisual");
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            var presentation = visual.GetComponent<BossBladeBurstPresentation>();
            if (presentation == null) presentation = visual.gameObject.AddComponent<BossBladeBurstPresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("bladeSprite").objectReferenceValue = blade;
            data.FindProperty("solidMaterial").objectReferenceValue = solidMaterial;
            data.FindProperty("bladeMaterial").objectReferenceValue = bladeMaterial;
            data.FindProperty("sortingOrder").intValue = recipe.telegraphOrder;
            data.ApplyModifiedPropertiesWithoutUndo();
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { solidMaterial, bladeMaterial };
            renderer.sortingOrder = recipe.telegraphOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (visual.GetComponent<MeshFilter>().sharedMesh != null)
                throw new InvalidOperationException("Boss telegraph mesh must be generated at runtime: " + path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool SameGameplayData(Component original, Component variant)
    {
        if (original == null || variant == null || original.GetType() != variant.GetType()) return false;
        var before = JObject.Parse(EditorJsonUtility.ToJson(original));
        var after = JObject.Parse(EditorJsonUtility.ToJson(variant));
        foreach (var key in new[] { "m_GameObject", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" })
        { before.Remove(key); after.Remove(key); }
        return JToken.DeepEquals(before, after);
    }

    [MenuItem("工具/VFX/检查Boss刀气爆发配置")]
    public static void Audit()
    {
        var recipe = ReadRecipe();
        var checks = new Dictionary<string, bool>();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
        var bladeMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var solidMaterial = AssetDatabase.LoadAssetAtPath<Material>(SolidMaterialPath);
        var trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalProjectilePath);
        var hit = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);
        long pngBytes = File.Exists(AtlasPath) ? new FileInfo(AtlasPath).Length : 0;
        checks["atlasLayoutAndBudget"] = texture != null && texture.width == 512 && texture.height == 256 && pngBytes > 0 && pngBytes <= recipe.pngBudgetKiB * 1024;
        checks["textureImport"] = importer != null && !importer.isReadable && !importer.mipmapEnabled && importer.spritePixelsPerUnit == recipe.pixelsPerUnit && importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear;
        var web = importer != null ? importer.GetPlatformTextureSettings("WebGL") : null;
        checks["webGLCompression"] = web != null && web.overridden && web.format == TextureImporterFormat.ETC2_RGBA8Crunched && web.crunchedCompression && web.compressionQuality == 70;
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        var blade = sprites.SingleOrDefault(s => s.name == "boss_blade_burst_v1");
        checks["bladeSprite"] = sprites.Length == 1 && blade != null && blade.rect == new Rect(0, 0, 256, 128) && blade.pixelsPerUnit == recipe.pixelsPerUnit;
        checks["sharedMaterials"] = bladeMaterial != null && bladeMaterial.shader.name == "Sprites/Default" && bladeMaterial.mainTexture == texture &&
            solidMaterial != null && solidMaterial.shader.name == "Sprites/Default" && solidMaterial.mainTexture == null && trailMaterial != null && trailMaterial.shader.name == "GongkaoMower/TalismanTrailSoft";
        checks["originalCollisionAndBulletFields"] = projectile != null && original != null &&
            projectile.transform.localScale == original.transform.localScale && projectile.transform.localScale == Vector3.one &&
            SameGameplayData(original.GetComponent<CircleCollider2D>(), projectile.GetComponent<CircleCollider2D>()) &&
            SameGameplayData(original.GetComponent<Rigidbody2D>(), projectile.GetComponent<Rigidbody2D>()) &&
            SameGameplayData(original.GetComponent<StraightBullet>(), projectile.GetComponent<StraightBullet>());
        if (projectile != null)
        {
            var renderers = projectile.GetComponentsInChildren<SpriteRenderer>(true);
            var trails = projectile.GetComponentsInChildren<TrailRenderer>(true);
            checks["singleBladeVisual"] = renderers.Length == 1 && renderers[0].sprite == blade && renderers[0].sharedMaterial == bladeMaterial &&
                renderers[0].sortingOrder == recipe.spriteOrder && renderers[0].enabled && !renderers[0].flipX && !renderers[0].flipY &&
                renderers[0].transform.parent == projectile.transform && renderers[0].transform.localRotation == Quaternion.identity &&
                renderers[0].transform.localScale == new Vector3(recipe.bladeScale, recipe.bladeScale, 1);
            checks["singleShortSoftTrail"] = trails.Length == 1 && trails[0].sharedMaterial == trailMaterial && trails[0].time == recipe.trailSeconds &&
                trails[0].widthMultiplier == recipe.trailWidth && trails[0].sortingOrder == recipe.trailOrder && trails[0].transform.localPosition == new Vector3(-.2f, 0, 0) &&
                trails[0].colorGradient.alphaKeys.Last().alpha == 0;
            var presentation = projectile.GetComponent<BossBladeProjectilePresentation>();
            checks["projectileDissolveReference"] = presentation != null && new SerializedObject(presentation).FindProperty("dissolvePrefab").objectReferenceValue == hit;
        }
        else { checks["singleBladeVisual"] = checks["singleShortSoftTrail"] = checks["projectileDissolveReference"] = false; }
        if (hit != null && hit.GetComponentsInChildren<ParticleSystem>(true).Length == 1 && hit.GetComponent<PooledCombatVfx>() != null)
        {
            var ps = hit.GetComponent<ParticleSystem>(); var main = ps.main; var sheet = ps.textureSheetAnimation;
            var pool = new SerializedObject(hit.GetComponent<PooledCombatVfx>());
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            checks["oneLayerEightFrameDissolve"] = main.maxParticles == 1 && !main.loop && !main.playOnAwake && !main.useUnscaledTime &&
                main.duration == recipe.hitSeconds && main.startLifetime.constant == recipe.hitSeconds && main.startSpeed.constant == 0 && main.startSize.constant == recipe.hitSize &&
                sheet.enabled && sheet.numTilesX == 8 && sheet.numTilesY == 4 && sheet.frameOverTime.curve.Evaluate(1) < .25f &&
                sheet.frameOverTime.curve.Evaluate(1) > .249f && renderer.sharedMaterial == bladeMaterial && renderer.sortingOrder == recipe.hitOrder && !ps.useAutoRandomSeed;
            checks["dissolvePoolTiming"] = pool.FindProperty("lifetimeOverride").floatValue == recipe.poolSeconds &&
                pool.FindProperty("referenceRadius").floatValue == recipe.hitRadius && pool.FindProperty("sortingOrder").intValue == recipe.hitOrder &&
                pool.FindProperty("_particles").arraySize == 1 && pool.FindProperty("_particles").GetArrayElementAtIndex(0).objectReferenceValue == ps;
        }
        else { checks["oneLayerEightFrameDissolve"] = checks["dissolvePoolTiming"] = false; }
        foreach (var path in BossPaths)
        {
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var brain = boss != null ? boss.GetComponent<BossBrain>() : null;
            var visual = boss != null ? boss.transform.Find("BossBladeBurstVisual") : null;
            var presentation = visual != null ? visual.GetComponent<BossBladeBurstPresentation>() : null;
            bool correct = brain != null && brain.prefabRegistry != null && brain.prefabRegistry.Count(e => e != null && string.Equals(e.key, RegistryKey, StringComparison.OrdinalIgnoreCase)) == 1 && brain.FindPrefab(RegistryKey) == projectile && presentation != null;
            if (correct)
            {
                var data = new SerializedObject(presentation);
                var filter = visual.GetComponent<MeshFilter>(); var renderer = visual.GetComponent<MeshRenderer>();
                correct = data.FindProperty("bladeSprite").objectReferenceValue == blade && data.FindProperty("solidMaterial").objectReferenceValue == solidMaterial &&
                    data.FindProperty("bladeMaterial").objectReferenceValue == bladeMaterial && data.FindProperty("sortingOrder").intValue == recipe.telegraphOrder &&
                    filter != null && filter.sharedMesh == null && renderer != null && renderer.sortingOrder == recipe.telegraphOrder &&
                    renderer.sharedMaterials.SequenceEqual(new[] { solidMaterial, bladeMaterial });
            }
            checks["bossRegistryAndVisual:" + Path.GetFileNameWithoutExtension(path)] = correct;
        }
        bool passed = checks.Values.All(value => value);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "configuration.json", JsonConvert.SerializeObject(new { passed, checks,
            recipe = JObject.Parse(File.ReadAllText(RecipePath)), checkedUtc = DateTime.UtcNow }, Formatting.Indented));
        File.WriteAllText(Output + "resources.json", JsonConvert.SerializeObject(new {
            sourcePngBytes = pngBytes, width = texture != null ? texture.width : 0, height = texture != null ? texture.height : 0,
            format = texture != null ? texture.format.ToString() : "missing", etc2BlockBytesWithoutMipmaps = 512 * 256,
            editorTextureMemoryBytes = texture != null ? UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) : 0,
            sharedMaterials = 3, addedTrailTextures = 0, renderersPerProjectile = 2, telegraphRenderersPerBoss = 1,
            hitLayers = 1, hitFrames = 8, hitSeconds = recipe.hitSeconds, poolSeconds = recipe.poolSeconds,
            projectilePath = ProjectilePath, bossPaths = BossPaths,
            note = "Source PNG bytes, ETC2 block data and Editor memory are not WeChat upload size/device performance."
        }, Formatting.Indented));
        if (!passed) throw new InvalidOperationException("Boss blade visual configuration drift. See " + Output + "configuration.json");
    }
}

[Serializable]
public sealed class BossBladeBurstVisualRecipe
{
    public int schemaVersion, width, height, pixelsPerUnit, pngBudgetKiB, spriteOrder, trailOrder, hitOrder, telegraphOrder;
    public string id, mode, displayName, status, texturePath, materialPath, solidMaterialPath, trailMaterialPath, hitPrefabPath,
        projectilePath, registryKey, bladeSource, hitSource, briefPath, promptPath, validationReport;
    public float bladeScale, trailSeconds, trailWidth, hitSeconds, poolSeconds, hitRadius, hitSize;
}
#endif
