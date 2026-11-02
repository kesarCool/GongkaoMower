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

public static class BossDashV1Builder
{
    public const string RecipePath = "Tools/VFX/Library/Boss/boss_dash_v1.json";
    public const string MaterialPath = "Assets/Res/Materials/VFX/mat_boss_blade_solid_v1.mat";
    public const string Output = "ArtOutput/VFX/BossDashKnifeV1/Audit/";
    public static readonly string[] BossPaths = {
        "Assets/Prefab/Enemy/Boss/boss_common.prefab", "Assets/Prefab/Enemy/Boss/boss_30001.prefab",
        "Assets/Prefab/Enemy/Boss/boss_30002.prefab", "Assets/Prefab/Enemy/Boss/boss_30003.prefab"
    };

    public static BossDashVisualRecipe ReadRecipe()
    {
        var recipe = JsonConvert.DeserializeObject<BossDashVisualRecipe>(File.ReadAllText(RecipePath),
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (recipe == null || recipe.schemaVersion != 1 || recipe.id != "boss_dash_v1" ||
            recipe.mode != "boss-dash" || recipe.materialPath != MaterialPath || recipe.childName != "BossDashVisual" ||
            recipe.warningRadiusPolicy != "max-skill-and-world-body-radius" || recipe.chargeSeconds != .3f ||
            recipe.recoverySeconds != .2f || recipe.dashDistance != 8f || recipe.damageRadius != 1.2f ||
            recipe.damage != 30f || recipe.intervalSeconds != 6f || recipe.firstDelayMultiplier != .3f ||
            recipe.wallContactCapacity != 4 || recipe.dashSpeedVariants == null ||
            !recipe.dashSpeedVariants.SequenceEqual(new[] { 15f, 20f }))
            throw new InvalidOperationException("Invalid Boss dash recipe identity or preserved gameplay constants.");
        if (recipe.borderWidth <= 0 || recipe.borderWidth > .08f || recipe.chargeFillAlpha < .03f ||
            recipe.chargeFillAlpha > .15f || recipe.sortingOrder != 4 || recipe.addedPngBytes != 0 ||
            recipe.addedMaterials != 0 || recipe.renderersPerBoss != 1 || recipe.submeshesPerBoss != 1)
            throw new InvalidOperationException("Boss dash recipe exceeds presentation budget.");
        return recipe;
    }

    [MenuItem("工具/VFX/Build Boss Dash V1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var recipe = ReadRecipe();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null || material.shader.name != "Sprites/Default" || material.mainTexture != null)
            throw new InvalidOperationException("Build Boss blade resources first; the shared solid material must exist.");
        foreach (var path in BossPaths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<BossBrain>() == null || root.GetComponent<Rigidbody2D>() == null)
                    throw new InvalidOperationException("Missing Boss gameplay components: " + path);
                var child = root.transform.Find(recipe.childName);
                if (child == null)
                {
                    child = new GameObject(recipe.childName).transform;
                    child.SetParent(root.transform, false);
                }
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                var presentation = child.GetComponent<BossDashPresentation>();
                if (presentation == null) presentation = child.gameObject.AddComponent<BossDashPresentation>();
                var data = new SerializedObject(presentation);
                data.FindProperty("solidMaterial").objectReferenceValue = material;
                data.FindProperty("sortingOrder").intValue = recipe.sortingOrder;
                data.FindProperty("skillDamageRadius").floatValue = recipe.damageRadius;
                data.FindProperty("borderWidth").floatValue = recipe.borderWidth;
                data.FindProperty("chargeFillAlpha").floatValue = recipe.chargeFillAlpha;
                data.ApplyModifiedPropertiesWithoutUndo();
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = recipe.sortingOrder;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                if (child.GetComponent<MeshFilter>().sharedMesh != null)
                    throw new InvalidOperationException("Dash mesh must be generated only at runtime: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Audit();
    }

    [MenuItem("工具/VFX/检查Boss冲刺配置")]
    public static void Audit()
    {
        var recipe = ReadRecipe();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var checks = new Dictionary<string, bool>();
        checks["sharedSolidMaterialWithoutTexture"] = material != null && material.shader.name == "Sprites/Default" && material.mainTexture == null;
        var radii = new Dictionary<string, float>();
        foreach (var path in BossPaths)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var child = root != null ? root.transform.Find(recipe.childName) : null;
            var presentation = child != null ? child.GetComponent<BossDashPresentation>() : null;
            var brain = root != null ? root.GetComponent<BossBrain>() : null;
            bool correct = presentation != null && brain != null && root.GetComponent<Rigidbody2D>() != null;
            if (correct)
            {
                var data = new SerializedObject(presentation);
                var filter = child.GetComponent<MeshFilter>();
                var renderer = child.GetComponent<MeshRenderer>();
                correct = data.FindProperty("solidMaterial").objectReferenceValue == material &&
                    data.FindProperty("sortingOrder").intValue == recipe.sortingOrder &&
                    data.FindProperty("skillDamageRadius").floatValue == recipe.damageRadius &&
                    data.FindProperty("borderWidth").floatValue == recipe.borderWidth &&
                    data.FindProperty("chargeFillAlpha").floatValue == recipe.chargeFillAlpha &&
                    filter != null && filter.sharedMesh == null && renderer != null && !renderer.enabled &&
                    renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == material && renderer.sortingOrder == recipe.sortingOrder &&
                    child.localPosition == Vector3.zero && child.localRotation == Quaternion.identity && child.localScale == Vector3.one;
                var circle = root.GetComponent<CircleCollider2D>();
                Vector3 scale = root.transform.lossyScale;
                radii[Path.GetFileNameWithoutExtension(path)] = circle != null
                    ? Mathf.Max(recipe.damageRadius, circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))) : recipe.damageRadius;
            }
            checks["bossDashVisual:" + Path.GetFileNameWithoutExtension(path)] = correct;
            checks["bladeBurstRegistryPreserved:" + Path.GetFileNameWithoutExtension(path)] = brain != null &&
                brain.FindPrefab("BladeBurstBullet") == AssetDatabase.LoadAssetAtPath<GameObject>(BossBladeBurstV1Builder.ProjectilePath) &&
                brain.FindPrefab("HomingBullet") == AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Boss/bossBullet/HomingBullet.prefab") &&
                root.GetComponentInChildren<BossBladeBurstPresentation>(true) != null;
        }
        bool passed = checks.Values.All(value => value);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "dash-configuration.json", JsonConvert.SerializeObject(new {
            passed, checks, recipe = JObject.Parse(File.ReadAllText(RecipePath)), checkedUtc = DateTime.UtcNow
        }, Formatting.Indented));
        File.WriteAllText(Output + "dash-resources.json", JsonConvert.SerializeObject(new {
            addedPngBytes = 0, addedTextures = 0, addedMaterials = 0, sharedMaterial = MaterialPath,
            renderersPerBoss = 1, submeshesPerBoss = 1, runtimeMeshesPerBoss = 1, dashSpeedLines = 3,
            warningRadii = radii, skillDamageRadius = recipe.damageRadius, chargeSeconds = recipe.chargeSeconds,
            recoverySeconds = recipe.recoverySeconds, wallContactCapacity = recipe.wallContactCapacity,
            prediction = "Current four-contact Rigidbody2D.Cast-visible wall. Dense skipped contacts may hide a farther wall; movement keeps the original step query.",
            note = "No runtime bitmap/font-material allocation. These resource counts do not measure WeChat FPS or final upload size."
        }, Formatting.Indented));
        if (!passed) throw new InvalidOperationException("Boss dash configuration drift. See " + Output + "dash-configuration.json");
    }
}

[Serializable]
public sealed class BossDashVisualRecipe
{
    public int schemaVersion, sortingOrder, wallContactCapacity, addedPngBytes, addedMaterials, renderersPerBoss, submeshesPerBoss;
    public string id, mode, displayName, status, materialPath, childName, warningRadiusPolicy, briefPath, promptPath, validationReport;
    public float damageRadius, borderWidth, chargeFillAlpha, chargeSeconds, recoverySeconds, dashDistance, damage,
        intervalSeconds, firstDelayMultiplier;
    public float[] dashSpeedVariants;
}
#endif
