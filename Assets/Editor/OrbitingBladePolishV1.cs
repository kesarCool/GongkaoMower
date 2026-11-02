#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small, repeatable trail-only polish; the two authored blade sprites are preserved.</summary>
public static class OrbitingBladePolishV1
{
    public const string Output = "ArtOutput/VFX/OrbitingBladePolishV1";
    const string Regular = "Assets/Prefab/Combat/OrbitingBlade.prefab";
    const string Advanced = "Assets/Prefab/Combat/OrbitingBladeMax.prefab";
    const string MaterialPath = "Assets/Res/Materials/VFX/mat_orbiting_blade_wind_v1.mat";

    [MenuItem("工具/VFX/回风舞柳/应用微调 V1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("GongkaoMower/TalismanTrailSoft");
            if (shader == null) throw new System.InvalidOperationException("Missing soft trail shader");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        Configure(Regular, false, material);
        Configure(Advanced, true, material);
        AssetDatabase.SaveAssets();
    }

    static void Configure(string path, bool advanced, Material material)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trail = root.GetComponentInChildren<TrailRenderer>(true);
            trail.sharedMaterial = material;
            trail.time = advanced ? .155f : .135f;
            trail.widthMultiplier = advanced ? .19f : .125f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .85f), new Keyframe(.22f, 1), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(advanced ? new Color(.98f,.96f,1) : new Color(1,.97f,.86f), 0),
                new GradientColorKey(advanced ? new Color(.77f,.66f,.94f) : new Color(.90f,.77f,.46f), .55f),
                new GradientColorKey(advanced ? new Color(.60f,.50f,.79f) : new Color(.71f,.67f,.40f), 1)
            }, new[] { new GradientAlphaKey(advanced ? .68f : .58f, 0), new GradientAlphaKey(.30f,.45f), new GradientAlphaKey(0,1) });
            trail.colorGradient = gradient;
            trail.minVertexDistance = .025f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 2;
            trail.sortingLayerID = root.GetComponent<SpriteRenderer>().sortingLayerID;
            trail.sortingOrder = root.GetComponent<SpriteRenderer>().sortingOrder - 1;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

}
#endif
