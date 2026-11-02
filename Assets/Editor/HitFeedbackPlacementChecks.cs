#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Presentation geometry checks in a disposable preview scene; never saves game scenes or prefabs.</summary>
public static class HitFeedbackPlacementChecks
{
    public const string ReportPath = "ArtOutput/VFX/HitFeedbackAlignmentV1/anchor-checks.json";
    static readonly string[] Prefabs = {
        "Assets/Prefab/Enemy/Normal/enemy001.prefab",
        "Assets/Prefab/Enemy/Normal/enemy003.prefab"
    };

    sealed class SceneSnapshot
    {
        public int handle;
        public string path;
        public bool dirty;
    }

    sealed class PlacementCase
    {
        public string name, word;
        public float rootRotation, textRotation;
        public Vector3 rootScale, textScale;
    }

    [MenuItem("工具/VFX/检查命中反馈字形锚点")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running editor anchor checks.");

        var checks = new List<object>();
        var cases = new List<object>();
        Action<string, bool> check = (name, passed) => {
            checks.Add(new { name, passed });
            if (!passed) throw new InvalidOperationException(name);
        };
        var originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var scene = SceneManager.GetSceneAt(i);
            return new SceneSnapshot { handle = scene.handle, path = scene.path, dirty = scene.isDirty };
        }).ToArray();
        int originalActive = SceneManager.GetActiveScene().handle;
        int originalPreviewCount = EditorSceneManager.previewSceneCount;
        var originalRandom = UnityEngine.Random.state;
        bool originalLogging = EnemyWordLabel.LogVerbose;
        var temporary = new List<GameObject>();
        var assetBytes = Prefabs.ToDictionary(path => path, File.ReadAllBytes);
        Scene preview = default(Scene);
        string error = "";

        try
        {
            EnemyWordLabel.LogVerbose = false;
            preview = EditorSceneManager.NewPreviewScene();
            var scenarios = new[] {
                new PlacementCase { name="ordinary", word="天师符令", rootScale=Vector3.one, textScale=Vector3.one },
                new PlacementCase { name="rotated-scaled", word="刀光剑影", rootRotation=31, textRotation=-27, rootScale=new Vector3(1.15f,.78f,1), textScale=new Vector3(.87f,1.22f,1) },
                new PlacementCase { name="long-word", word="雷霆万钧穿云破雾连环爆破", rootRotation=-19, textRotation=24, rootScale=new Vector3(.76f,1.31f,1), textScale=new Vector3(1.18f,.92f,1) },
                new PlacementCase { name="character-gap", word="天    师", rootRotation=12, textRotation=-35, rootScale=new Vector3(1.24f,.82f,1), textScale=new Vector3(.91f,1.16f,1) }
            };

            foreach (string prefabPath in Prefabs)
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                check("Real enemy fixture exists: " + prefabPath, template != null);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(template, preview);
                temporary.Add(go);
                go.name = "HitAnchorFixture_" + Path.GetFileNameWithoutExtension(prefabPath);
                var label = go.GetComponentInChildren<EnemyWordLabel>(true);
                var collider = go.GetComponent<Collider2D>() ?? go.GetComponentInChildren<Collider2D>(true);
                check("Real enemy has word label and collider: " + prefabPath, label != null && collider != null);
                var serialized = new SerializedObject(label);
                serialized.FindProperty("randomizeZRotation").boolValue = false;
                serialized.FindProperty("randomizeFontSize").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                foreach (var scenario in scenarios)
                {
                    label.SetWord(scenario.word);
                    TextMeshPro text;
                    check(prefabPath + " / " + scenario.name + ": world TMP resolves", label.TryGetWorldTextForShatter(out text));
                    go.transform.position = new Vector3(17,-13,10);
                    go.transform.rotation = Quaternion.Euler(0,0,scenario.rootRotation);
                    go.transform.localScale = scenario.rootScale;
                    text.transform.localRotation = Quaternion.Euler(0,0,scenario.textRotation);
                    text.transform.localScale = scenario.textScale;
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.ForceMeshUpdate(true);
                    Physics2D.SyncTransforms();

                    var visible = VisibleCharacters(text);
                    check(prefabPath + " / " + scenario.name + ": rendered visible characters", visible.Length >= 2);
                    Bounds bounds = WorldGlyphBounds(text, visible);
                    float reach = Mathf.Max(bounds.extents.x,bounds.extents.y) + 2;
                    var sources = new List<Vector2>();
                    for (int direction=0; direction<8; direction++)
                    {
                        float angle = direction * Mathf.PI / 4;
                        sources.Add((Vector2)bounds.center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*reach);
                    }
                    sources.Add(bounds.center);
                    sources.Add(text.transform.TransformPoint((visible[0].bottomLeft + visible[visible.Length-1].topRight)*.5f));

                    bool pointsOnGlyph = true, consistentApi = true, hasAnchor = true;
                    var beforeResolve = UnityEngine.Random.state;
                    foreach (Vector2 source in sources)
                    {
                        Transform anchor; Vector3 local;
                        bool found = label.TryGetHitAnchor(source,out anchor,out local);
                        hasAnchor &= found && anchor != null;
                        if (!found || anchor == null) { pointsOnGlyph=false; continue; }
                        Vector2 directPoint = anchor.TransformPoint(local);
                        pointsOnGlyph &= InVisibleGlyph(text,visible,directPoint);
                        EnemyWordLabel resolvedLabel; Transform resolvedAnchor; Vector3 resolvedLocal;
                        Vector2 resolved = CombatHitPlacement.Resolve(collider,source,out resolvedLabel,out resolvedAnchor,out resolvedLocal);
                        consistentApi &= resolvedLabel == label && resolvedAnchor == anchor &&
                            Vector2.Distance(directPoint,resolved)<.0002f &&
                            Vector2.Distance(resolvedAnchor.TransformPoint(resolvedLocal),resolved)<.0002f;
                        pointsOnGlyph &= InVisibleGlyph(text,visible,resolved);
                    }
                    bool randomUntouched = JsonUtility.ToJson(beforeResolve) == JsonUtility.ToJson(UnityEngine.Random.state);
                    check(prefabPath + " / " + scenario.name + ": all directions and gaps resolve an anchor",hasAnchor);
                    check(prefabPath + " / " + scenario.name + ": all contacts lie on a visible character quad",pointsOnGlyph);
                    check(prefabPath + " / " + scenario.name + ": shared placement agrees with label API",consistentApi);
                    check(prefabPath + " / " + scenario.name + ": contact resolution consumes no combat RNG",randomUntouched);

                    var randomBeforeCenter = UnityEngine.Random.state;
                    Vector2 visualCenter;
                    bool centerFound = label.TryGetVisualCenter(out visualCenter);
                    Vector2 sharedCenter = CombatHitPlacement.Center(go.transform);
                    check(prefabPath + " / " + scenario.name + ": visual center lies within glyph bounds",
                        centerFound && InsideBounds2D(bounds,visualCenter) && Vector2.Distance(sharedCenter,visualCenter)<.0002f);
                    check(prefabPath + " / " + scenario.name + ": center resolution consumes no combat RNG",
                        JsonUtility.ToJson(randomBeforeCenter)==JsonUtility.ToJson(UnityEngine.Random.state));
                    cases.Add(new { prefab=prefabPath, scenario=scenario.name, visibleCharacters=visible.Length,
                        sourceDirections=sources.Count, glyphCenter=new { x=visualCenter.x,y=visualCenter.y },
                        rootToGlyphCenter=Vector2.Distance(go.transform.position,visualCenter) });
                }

                foreach (string blank in new[] { "", "   \n  " })
                {
                    label.SetWord(blank);
                    var text = go.GetComponentInChildren<TextMeshPro>(true);
                    text.ForceMeshUpdate(true);
                    Physics2D.SyncTransforms();
                    Vector2 source = (Vector2)go.transform.position + new Vector2(3,-2);
                    Transform anchor; Vector3 local;
                    var beforeResolve = UnityEngine.Random.state;
                    bool found = label.TryGetHitAnchor(source,out anchor,out local);
                    Vector2 center;
                    bool centerFound = label.TryGetVisualCenter(out center);
                    EnemyWordLabel resolvedLabel; Transform resolvedAnchor; Vector3 resolvedLocal;
                    Vector2 resolved = CombatHitPlacement.Resolve(collider,source,out resolvedLabel,out resolvedAnchor,out resolvedLocal);
                    check(prefabPath + (blank.Length==0?": empty text":": whitespace text") + " has no glyph anchor",
                        !found && !centerFound && resolvedAnchor == null);
                    check(prefabPath + (blank.Length==0?": empty text":": whitespace text") + " uses collider fallback",
                        Vector2.Distance(resolved,collider.ClosestPoint(source))<.0002f);
                    check(prefabPath + (blank.Length==0?": empty text":": whitespace text") + " center falls back to target root",
                        Vector2.Distance(CombatHitPlacement.Center(go.transform),go.transform.position)<.0002f);
                    check(prefabPath + (blank.Length==0?": empty text":": whitespace text") + " fallback consumes no combat RNG",
                        JsonUtility.ToJson(beforeResolve)==JsonUtility.ToJson(UnityEngine.Random.state));
                }

                // Ordinary MonoBehaviour edit-mode activation does not guarantee runtime callbacks.
                // Invoke the actual callbacks explicitly; pooled runtime following has a separate play-mode fixture.
                int originalVersion = label.HitAnchorVersion;
                InvokeLifecycle(label,"OnDisable"); int disabledVersion = label.HitAnchorVersion;
                InvokeLifecycle(label,"OnEnable"); int enabledVersion = label.HitAnchorVersion;
                check(prefabPath + ": disable and enable callbacks invalidate anchor version",
                    originalVersion != disabledVersion && disabledVersion != enabledVersion);
            }

            var fallback = new GameObject("HitAnchor_NoWordLabel");
            temporary.Add(fallback); SceneManager.MoveGameObjectToScene(fallback,preview);
            fallback.transform.position = new Vector3(31,-17,10);
            fallback.transform.rotation = Quaternion.Euler(0,0,22);
            fallback.transform.localScale = new Vector3(1.6f,.65f,1);
            var circle = fallback.AddComponent<CircleCollider2D>(); circle.radius=.7f; circle.offset=new Vector2(.2f,-.15f);
            Physics2D.SyncTransforms();
            var randomBeforeFallback = UnityEngine.Random.state;
            EnemyWordLabel unusedLabel; Transform unusedAnchor; Vector3 unusedLocal;
            Vector2 fallbackSource = (Vector2)fallback.transform.position + new Vector2(4,3);
            Vector2 fallbackPoint = CombatHitPlacement.Resolve(circle,fallbackSource,out unusedLabel,out unusedAnchor,out unusedLocal);
            check("Non-word target uses exact collider closest point",unusedLabel==null && unusedAnchor==null && Vector2.Distance(fallbackPoint,circle.ClosestPoint(fallbackSource))<.0002f);
            check("Non-word center falls back to target root",Vector2.Distance(CombatHitPlacement.Center(fallback.transform),fallback.transform.position)<.0002f);
            Vector2 nullTargetPoint=CombatHitPlacement.Resolve(null,fallbackSource,out unusedLabel,out unusedAnchor,out unusedLocal);
            check("Missing target safely retains source and returns no anchor",unusedLabel==null && unusedAnchor==null && nullTargetPoint==fallbackSource && CombatHitPlacement.Center(null)==Vector2.zero);
            check("Non-word target resolution consumes no combat RNG",JsonUtility.ToJson(randomBeforeFallback)==JsonUtility.ToJson(UnityEngine.Random.state));
        }
        catch (Exception exception) { error=exception.ToString(); }
        finally
        {
            foreach (var go in temporary) if (go != null) Object.DestroyImmediate(go);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            EnemyWordLabel.LogVerbose = originalLogging;
            UnityEngine.Random.state = originalRandom;
        }

        bool objectsRemoved = temporary.All(go => go == null);
        bool sceneStatePreserved = SceneManager.sceneCount == originalScenes.Length &&
            SceneManager.GetActiveScene().handle == originalActive &&
            originalScenes.All(before => {
                var after = Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(scene => scene.handle==before.handle);
                return after.IsValid() && after.path==before.path && after.isDirty==before.dirty;
            }) && EditorSceneManager.previewSceneCount==originalPreviewCount;
        bool assetsPreserved = assetBytes.All(pair => pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)));
        checks.Add(new { name="All temporary fixture objects and components are removed",passed=objectsRemoved });
        checks.Add(new { name="Loaded scenes, active scene, dirty flags and preview count are preserved",passed=sceneStatePreserved });
        checks.Add(new { name="Real enemy prefab bytes are unchanged",passed=assetsPreserved });
        bool passedAll = error.Length==0 && objectsRemoved && sceneStatePreserved && assetsPreserved;
        string report = JsonConvert.SerializeObject(new { passed=passedAll,checks,cases,error,
            note="Editor geometry validation on real enemy prefabs. Version lifecycle callbacks invoked explicitly; movement/following and pool reuse require the separate play-mode fixture." },Formatting.Indented);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath,report);
        return report;
    }

    static TMP_CharacterInfo[] VisibleCharacters(TextMeshPro text)
    {
        return text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible).ToArray();
    }

    static Bounds WorldGlyphBounds(TextMeshPro text,TMP_CharacterInfo[] characters)
    {
        var bounds=new Bounds(text.transform.TransformPoint(characters[0].bottomLeft),Vector3.zero);
        foreach (var c in characters)
        {
            bounds.Encapsulate(text.transform.TransformPoint(c.bottomLeft));
            bounds.Encapsulate(text.transform.TransformPoint(c.bottomRight));
            bounds.Encapsulate(text.transform.TransformPoint(c.topLeft));
            bounds.Encapsulate(text.transform.TransformPoint(c.topRight));
        }
        return bounds;
    }

    static bool InVisibleGlyph(TextMeshPro text,TMP_CharacterInfo[] characters,Vector2 point)
    {
        Vector3 local=text.transform.InverseTransformPoint(new Vector3(point.x,point.y,text.transform.position.z));
        const float inset=.000001f;
        return characters.Any(c => local.x>c.bottomLeft.x+inset && local.x<c.topRight.x-inset &&
            local.y>c.bottomLeft.y+inset && local.y<c.topRight.y-inset);
    }

    static bool InsideBounds2D(Bounds bounds,Vector2 point)
    {
        const float epsilon=.0005f;
        return point.x>=bounds.min.x-epsilon && point.x<=bounds.max.x+epsilon && point.y>=bounds.min.y-epsilon && point.y<=bounds.max.y+epsilon;
    }

    static void InvokeLifecycle(EnemyWordLabel label,string method)
    {
        var callback=typeof(EnemyWordLabel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        if (callback==null) throw new MissingMethodException(typeof(EnemyWordLabel).FullName,method);
        callback.Invoke(label,null);
    }
}
#endif
