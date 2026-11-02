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

/// <summary>Reuses the Boss blade atlas for a compact knife; never changes the original HomingBullet asset.</summary>
public static class BossKnifeV1Builder
{
    public const string RecipePath = "Tools/VFX/Library/Boss/boss_knife_v1.json";
    public const string ProjectilePath = "Assets/Prefab/Enemy/Boss/bossBullet/KnifeBulletV1.prefab";
    public const string OriginalProjectilePath = "Assets/Prefab/Enemy/Boss/bossBullet/HomingBullet.prefab";
    public const string Output = "ArtOutput/VFX/BossDashKnifeV1/Audit/";
    public const string RegistryKey = "KnifeBullet";

    public static BossKnifeVisualRecipe ReadRecipe()
    {
        var recipe = JsonConvert.DeserializeObject<BossKnifeVisualRecipe>(File.ReadAllText(RecipePath),
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (recipe == null || recipe.schemaVersion != 1 || recipe.id != "boss_knife_v1" || recipe.mode != "boss-knife" ||
            recipe.projectilePath != ProjectilePath || recipe.originalProjectilePath != OriginalProjectilePath || recipe.registryKey != RegistryKey ||
            recipe.texturePath != BossBladeBurstV1Builder.AtlasPath || recipe.materialPath != BossBladeBurstV1Builder.MaterialPath ||
            recipe.solidMaterialPath != BossBladeBurstV1Builder.SolidMaterialPath || recipe.trailMaterialPath != BossBladeBurstV1Builder.TrailMaterialPath ||
            recipe.hitPrefabPath != BossBladeBurstV1Builder.HitPath || recipe.spriteName != "boss_blade_burst_v1")
            throw new InvalidOperationException("Invalid Boss knife recipe identity/shared references.");
        if (recipe.bladeScaleX <= .1f || recipe.bladeScaleX > .7f || recipe.bladeScaleY <= .1f || recipe.bladeScaleY > .9f ||
            recipe.trailSeconds <= 0f || recipe.trailSeconds > .1f || recipe.trailWidth <= 0f || recipe.trailWidth > .1f ||
            recipe.flashSeconds < .08f || recipe.flashSeconds > .18f || recipe.trailOrder >= recipe.spriteOrder || recipe.flashOrder >= recipe.spriteOrder)
            throw new InvalidOperationException("Boss knife recipe exceeds compact presentation budget.");
        return recipe;
    }

    [MenuItem("工具/VFX/Build Boss Knife V1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var recipe = ReadRecipe();
        var blade = AssetDatabase.LoadAllAssetsAtPath(recipe.texturePath).OfType<Sprite>().Single(s => s.name == recipe.spriteName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(recipe.materialPath);
        var solid = AssetDatabase.LoadAssetAtPath<Material>(recipe.solidMaterialPath);
        var trail = AssetDatabase.LoadAssetAtPath<Material>(recipe.trailMaterialPath);
        var hit = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.hitPrefabPath);
        if (material == null || solid == null || trail == null || hit == null) throw new InvalidOperationException("Build Boss blade burst shared resources first.");
        Directory.CreateDirectory(Path.GetDirectoryName(ProjectilePath));
        var projectile = BuildProjectile(recipe, blade, material, trail, hit);
        foreach (var path in BossBladeBurstV1Builder.BossPaths) ConfigureBoss(path, projectile, solid, recipe);
        Audit();
    }

    static Transform FindOrCreate(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;
        child = new GameObject(name).transform; child.SetParent(parent, false); return child;
    }

    static GameObject BuildProjectile(BossKnifeVisualRecipe recipe, Sprite blade, Material material, Material trailMaterial, GameObject hit)
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null;
        var root = PrefabUtility.LoadPrefabContents(existing ? ProjectilePath : OriginalProjectilePath);
        try
        {
            root.name = "KnifeBulletV1";
            var visual = FindOrCreate(root.transform, "KnifeVisual");
            visual.localPosition = new Vector3(0, 0, -.15f); visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(recipe.bladeScaleX, recipe.bladeScaleY, 1);
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            foreach (var previous in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (previous != renderer) UnityEngine.Object.DestroyImmediate(previous);
            renderer.enabled = true; renderer.sprite = blade; renderer.sharedMaterial = material; renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false; renderer.sortingOrder = recipe.spriteOrder;
            var wake = FindOrCreate(root.transform, "KnifeWake");
            wake.localPosition = new Vector3(-.15f, 0, -.15f); wake.localRotation = Quaternion.identity; wake.localScale = Vector3.one;
            var trail = wake.GetComponent<TrailRenderer>();
            if (trail == null) trail = wake.gameObject.AddComponent<TrailRenderer>();
            foreach (var previous in root.GetComponentsInChildren<TrailRenderer>(true))
                if (previous != trail) UnityEngine.Object.DestroyImmediate(previous);
            trail.sharedMaterial = trailMaterial; trail.time = recipe.trailSeconds; trail.widthMultiplier = recipe.trailWidth;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.45f, .65f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .88f, .61f), 0),
                    new GradientColorKey(new Color(.95f, .19f, .025f), .35f), new GradientColorKey(new Color(.28f, .015f, .025f), 1) },
                new[] { new GradientAlphaKey(.86f, 0), new GradientAlphaKey(.48f, .5f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient; trail.minVertexDistance = .055f; trail.numCornerVertices = 1; trail.numCapVertices = 2;
            trail.alignment = LineAlignment.View; trail.textureMode = LineTextureMode.Stretch; trail.autodestruct = false;
            trail.emitting = true; trail.sortingOrder = recipe.trailOrder; trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false; trail.generateLightingData = false;
            var presentation = root.GetComponent<BossBladeProjectilePresentation>();
            if (presentation == null) presentation = root.AddComponent<BossBladeProjectilePresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("dissolvePrefab").objectReferenceValue = hit; data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureBoss(string path, GameObject projectile, Material solid, BossKnifeVisualRecipe recipe)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var brain = root.GetComponent<BossBrain>();
            if (brain == null) throw new InvalidOperationException("Missing BossBrain: " + path);
            var registry = brain.prefabRegistry == null ? new List<BossPrefabEntry>() : brain.prefabRegistry.ToList();
            var matches = registry.Where(e => e != null && string.Equals(e.key, RegistryKey, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Duplicate Boss knife registry key: " + path);
            if (matches.Length == 0) registry.Add(new BossPrefabEntry { key = RegistryKey, prefab = projectile }); else matches[0].prefab = projectile;
            brain.prefabRegistry = registry.ToArray();
            var visual = FindOrCreate(root.transform, "BossKnifeVisual");
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
            var presentation = visual.GetComponent<BossKnifePresentation>();
            if (presentation == null) presentation = visual.gameObject.AddComponent<BossKnifePresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("solidMaterial").objectReferenceValue = solid;
            data.FindProperty("sortingOrder").intValue = recipe.flashOrder;
            data.FindProperty("flashSeconds").floatValue = recipe.flashSeconds;
            data.ApplyModifiedPropertiesWithoutUndo();
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = solid; renderer.sortingOrder = recipe.flashOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (visual.GetComponent<MeshFilter>().sharedMesh != null) throw new InvalidOperationException("Knife flash mesh must be generated at runtime: " + path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool SameGameplayData(Component original, Component variant)
    {
        if (original == null || variant == null || original.GetType() != variant.GetType()) return false;
        var before = JObject.Parse(EditorJsonUtility.ToJson(original)); var after = JObject.Parse(EditorJsonUtility.ToJson(variant));
        foreach (var key in new[] { "m_GameObject", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" }) { before.Remove(key); after.Remove(key); }
        return JToken.DeepEquals(before, after);
    }

    [MenuItem("工具/VFX/检查Boss小刀配置")]
    public static void Audit()
    {
        var recipe = ReadRecipe(); var checks = new Dictionary<string, bool>();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(recipe.texturePath);
        var blade = AssetDatabase.LoadAllAssetsAtPath(recipe.texturePath).OfType<Sprite>().SingleOrDefault(s => s.name == recipe.spriteName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(recipe.materialPath);
        var solid = AssetDatabase.LoadAssetAtPath<Material>(recipe.solidMaterialPath);
        var trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(recipe.trailMaterialPath);
        var hit = AssetDatabase.LoadAssetAtPath<GameObject>(recipe.hitPrefabPath);
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalProjectilePath);
        checks["sharedAtlasNoAdditionalTexture"] = texture != null && texture.width == 512 && texture.height == 256 && blade != null &&
            material != null && material.mainTexture == texture && solid != null && solid.mainTexture == null && trailMaterial != null &&
            trailMaterial.shader.name == "GongkaoMower/TalismanTrailSoft" && !trailMaterial.HasProperty("_MainTex");
        checks["originalPhysicsAndHomingFields"] = projectile != null && original != null && projectile.transform.localScale == original.transform.localScale &&
            SameGameplayData(original.GetComponent<CircleCollider2D>(), projectile.GetComponent<CircleCollider2D>()) &&
            SameGameplayData(original.GetComponent<Rigidbody2D>(), projectile.GetComponent<Rigidbody2D>()) &&
            SameGameplayData(original.GetComponent<HomingBullet>(), projectile.GetComponent<HomingBullet>());
        checks["configuredStraightFlightPreserved"] = projectile != null && projectile.GetComponent<HomingBullet>() != null && projectile.GetComponent<HomingBullet>().turnRate == 0f;
        if (projectile != null)
        {
            var renderers = projectile.GetComponentsInChildren<SpriteRenderer>(true); var trails = projectile.GetComponentsInChildren<TrailRenderer>(true);
            checks["singleCompactBlade"] = renderers.Length == 1 && renderers[0].sprite == blade && renderers[0].sharedMaterial == material &&
                renderers[0].sortingOrder == recipe.spriteOrder && renderers[0].enabled && !renderers[0].flipX && !renderers[0].flipY &&
                renderers[0].transform.localScale == new Vector3(recipe.bladeScaleX, recipe.bladeScaleY, 1) && renderers[0].transform.localRotation == Quaternion.identity;
            checks["singleShortSoftTrail"] = trails.Length == 1 && trails[0].sharedMaterial == trailMaterial && trails[0].time == recipe.trailSeconds &&
                trails[0].widthMultiplier == recipe.trailWidth && trails[0].sortingOrder == recipe.trailOrder && trails[0].colorGradient.alphaKeys.Last().alpha == 0f;
            var presentation = projectile.GetComponent<BossBladeProjectilePresentation>();
            checks["sharedPooledDissolve"] = presentation != null && hit != null && new SerializedObject(presentation).FindProperty("dissolvePrefab").objectReferenceValue == hit;
        }
        else { checks["singleCompactBlade"] = checks["singleShortSoftTrail"] = checks["sharedPooledDissolve"] = false; }
        foreach (var path in BossBladeBurstV1Builder.BossPaths)
        {
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(path); var brain = boss != null ? boss.GetComponent<BossBrain>() : null;
            var visual = boss != null ? boss.transform.Find("BossKnifeVisual") : null;
            var presentation = visual != null ? visual.GetComponent<BossKnifePresentation>() : null;
            bool correct = brain != null && brain.prefabRegistry != null && brain.prefabRegistry.Count(e => e != null && string.Equals(e.key, RegistryKey, StringComparison.OrdinalIgnoreCase)) == 1 &&
                brain.FindPrefab(RegistryKey) == projectile && presentation != null;
            if (correct)
            {
                var data = new SerializedObject(presentation); var filter = visual.GetComponent<MeshFilter>(); var renderer = visual.GetComponent<MeshRenderer>();
                correct = data.FindProperty("solidMaterial").objectReferenceValue == solid && data.FindProperty("sortingOrder").intValue == recipe.flashOrder &&
                    data.FindProperty("flashSeconds").floatValue == recipe.flashSeconds && filter != null && filter.sharedMesh == null && renderer != null &&
                    renderer.sharedMaterial == solid && renderer.sortingOrder == recipe.flashOrder && boss.transform.Find("BossBladeBurstVisual") != null && brain.FindPrefab("BladeBurstBullet") != null;
            }
            checks["bossRegistryAndReleaseFlash:" + Path.GetFileNameWithoutExtension(path)] = correct;
        }
        bool passed = checks.Values.All(value => value); Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "knife-configuration.json", JsonConvert.SerializeObject(new { passed, checks,
            recipe = JObject.Parse(File.ReadAllText(RecipePath)), checkedUtc = DateTime.UtcNow }, Formatting.Indented));
        File.WriteAllText(Output + "knife-resources.json", JsonConvert.SerializeObject(new {
            addedTextureBytes = 0, addedMaterials = 0, sharedAtlasPngBytes = new FileInfo(recipe.texturePath).Length,
            sharedTexturePath = recipe.texturePath, renderersPerProjectile = 2, releaseFlashRenderersPerBoss = 1,
            flashSeconds = recipe.flashSeconds, trailSeconds = recipe.trailSeconds, visualBladeWidth = .890625f * recipe.bladeScaleX,
            colliderRadiusUnchanged = projectile != null ? projectile.GetComponent<CircleCollider2D>().radius : 0f,
            projectilePath = ProjectilePath, bossPaths = BossBladeBurstV1Builder.BossPaths,
            note = "Reuses existing Boss atlas/materials/dissolve. Source bytes are not upload package size or device FPS."
        }, Formatting.Indented));
        if (!passed) throw new InvalidOperationException("Boss knife configuration drift. See " + Output + "knife-configuration.json");
    }
}

[Serializable]
public sealed class BossKnifeVisualRecipe
{
    public int schemaVersion, spriteOrder, trailOrder, flashOrder;
    public string id, mode, displayName, status, projectilePath, originalProjectilePath, registryKey, texturePath, spriteName,
        materialPath, solidMaterialPath, trailMaterialPath, hitPrefabPath, briefPath, promptPath, validationReport;
    public float bladeScaleX, bladeScaleY, trailSeconds, trailWidth, flashSeconds;
}
#endif
