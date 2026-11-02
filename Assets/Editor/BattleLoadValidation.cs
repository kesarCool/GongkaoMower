#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Run in an empty Play Mode scene; measures editor CPU work, not device FPS.</summary>
public static class BattleLoadValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;
    private static readonly Collider2D[] LegacyHits = new Collider2D[8];

    [MenuItem("Tools/Performance/Validate Dense Battle")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in an empty Play Mode scene.");
        if (Object.FindObjectOfType<EnemyBase>() != null || WordMonsterShatterRuntime.Instance != null)
            throw new InvalidOperationException("Use an empty scene to keep the user's battle untouched.");
        _checks = 0;
        var host = new GameObject("BattleLoadValidation");
        var savedRandom = UnityEngine.Random.state;
        bool savedTriggers = Physics2D.queriesHitTriggers;
        try
        {
            var box = Child(host, "wall").AddComponent<BoxCollider2D>();
            box.size = new Vector2(.3f, 12f);
            box.gameObject.AddComponent<WallMarker>();
            Physics2D.SyncTransforms();
            Check(WallStuckResolver.HasWallOverlap(new Vector2(.4f, 0)), "near wall");
            Check(!WallStuckResolver.HasWallOverlap(new Vector2(4, 0)), "open ground");
            Check(!WallStuckResolver.HasWallOverlap(Vector2.zero, -1f), "negative radius");

            // Compare exact shape queries with Unity's oracle, including rotated walls.
            box.transform.rotation = Quaternion.Euler(0, 0, 37);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 300; i++)
            {
                var point = new Vector2((i % 20 - 10) * .4f, (i / 20 - 7) * .4f);
                Check(WallStuckResolver.HasWallOverlap(point) == Oracle(point), "rotated wall " + i);
            }
            box.transform.rotation = Quaternion.identity;
            box.isTrigger = true;
            Physics2D.queriesHitTriggers = false;
            Check(!WallStuckResolver.HasWallOverlap(Vector2.zero), "trigger filter off");
            Physics2D.queriesHitTriggers = true;
            Check(WallStuckResolver.HasWallOverlap(Vector2.zero), "trigger filter on");
            box.isTrigger = false;
            box.enabled = false;
            Check(!WallStuckResolver.HasWallOverlap(Vector2.zero), "disabled collider");
            box.enabled = true;
            var body = box.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = false;
            Check(!WallStuckResolver.HasWallOverlap(Vector2.zero), "unsimulated body");
            body.simulated = true;
            Physics2D.SyncTransforms();
            Vector2 resolved = WallStuckResolver.Resolve(new Vector2(.1f, 0));
            Check(!WallStuckResolver.HasWallOverlap(resolved), "stuck entity exits wall");

            for (int i = 0; i < 200; i++)
            {
                var mob = Child(host, "crowd");
                mob.transform.position = new Vector3(3 + i % 20 * .25f, i / 20 * .25f, 0);
                mob.AddComponent<CircleCollider2D>().radius = .35f;
            }
            for (int i = 0; i < 30; i++) Child(host, "near-wall crowd").AddComponent<CircleCollider2D>();
            Physics2D.SyncTransforms();
            Check(WallStuckResolver.HasWallOverlap(Vector2.zero), "wall remains visible among 30 colliders");
            for (int i = 1; i < 4; i++)
            {
                var extra = Child(host, "far wall");
                extra.transform.position = new Vector3(20 + i, 0, 0);
                extra.AddComponent<BoxCollider2D>().size = new Vector2(.3f, 12f);
                extra.AddComponent<WallMarker>();
            }
            Physics2D.SyncTransforms();
            var points = new Vector2[200];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(3 + i % 20 * .25f, i / 20 * .25f);
            var oldTimes = new double[5]; var newTimes = new double[5];
            Measure(points, true); Measure(points, false);
            for (int i = 0; i < 5; i++) { oldTimes[i] = Measure(points, true); newTimes[i] = Measure(points, false); }
            Array.Sort(oldTimes); Array.Sort(newTimes);
            var getAllocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread"));
            long before = getAllocated();
            for (int i = 0; i < 1000; i++) WallStuckResolver.HasWallOverlap(points[i % 200]);
            long queryBytes = getAllocated() - before;
            Check(queryBytes == 0, "wall query zero managed allocations after warmup");

            var font = Resources.Load<TMP_FontAsset>(BattleChineseFontRuntime.DefaultResourcesPath);
            Check(font != null, "battle font present");
            var runtime = Child(host, "shatter").AddComponent<WordMonsterShatterRuntime>();
            var type = typeof(WordMonsterShatterRuntime);
            var pendType = type.GetNestedType("PendFragment", BindingFlags.NonPublic);
            object pending = Activator.CreateInstance(pendType);
            SetPending(pending, "font", font);
            var shared = BattleTextTheme.MaterialFor(font, BattleTextTheme.Role.Word);
            SetPending(pending, "fontSharedMaterial", shared);
            SetPending(pending, "character", '学');
            SetPending(pending, "fontSize", 12f);
            SetPending(pending, "worldRot", Quaternion.identity);
            SetPending(pending, "color", Color.white);
            SetPending(pending, "lifeMul", 1f);
            SetPending(pending, "burstMul", 1f);
            var enqueue = type.GetMethod("Enqueue", Private);
            var update = type.GetMethod("Update", Private);
            var release = type.GetMethod("ReleaseFragmentAt", Private);
            var active = (IList)type.GetField("_activeFragments", Private).GetValue(runtime);
            var ids = new HashSet<int>();
            for (int i = 0; i < 80; i++) enqueue.Invoke(runtime, new[] { pending });
            update.Invoke(runtime, null);
            Check(runtime.ActiveFragmentCount == 16, "creation spread: 16 per frame");
            for (int i = 0; i < 5; i++) update.Invoke(runtime, null);
            Check(runtime.ActiveFragmentCount == 64 && runtime.PendingFragmentCount == 16, "64 live fragment cap");
            for (int i = 0; i < active.Count; i++)
            {
                var go = FragmentObject(active[i]); ids.Add(go.GetInstanceID());
                Check(go.GetComponent<TextMeshPro>().fontSharedMaterial == shared, "fragment shares source material " + i);
            }
            while (active.Count > 0) release.Invoke(runtime, new object[] { active.Count - 1 });
            Check(runtime.PooledFragmentCount == 64, "expired fragments retained within budget");
            update.Invoke(runtime, null);
            while (active.Count > 0) release.Invoke(runtime, new object[] { active.Count - 1 });
            for (int round = 0; round < 10; round++)
            {
                SetPending(pending, "character", round % 2 == 0 ? '考' : '试');
                for (int i = 0; i < 64; i++) enqueue.Invoke(runtime, new[] { pending });
                for (int i = 0; i < 4; i++) update.Invoke(runtime, null);
                for (int i = 0; i < active.Count; i++)
                {
                    var go = FragmentObject(active[i]);
                    Check(ids.Contains(go.GetInstanceID()), "reuse existing fragment");
                    Check(go.GetComponent<TextMeshPro>().fontSharedMaterial == shared, "reuse shared material");
                    Check(go.GetComponent<TextMeshPro>().textInfo.characterInfo[0].character == (round % 2 == 0 ? '考' : '试'), "reused glyph refreshed");
                }
                while (active.Count > 0) release.Invoke(runtime, new object[] { active.Count - 1 });
            }
            Check(runtime.PooledFragmentCount == 64, "pool remains bounded after 640 respawns");
            var result = new Result { checks = _checks, legacyWallMedianMs = oldTimes[2], wallOnlyMedianMs = newTimes[2], queriesPerSample = 10000,
                wallQueryAllocatedBytes = queryBytes, recycledFragmentRespawns = 640, retainedFragments = runtime.PooledFragmentCount };
            string json = JsonUtility.ToJson(result, true);
            Directory.CreateDirectory("Library/BattlePerformanceValidation");
            File.WriteAllText("Library/BattlePerformanceValidation/result.json", json);
            return json;
        }
        finally
        {
            host.SetActive(false);
            Object.Destroy(host);
            Physics2D.queriesHitTriggers = savedTriggers;
            UnityEngine.Random.state = savedRandom;
        }
    }

    private static GameObject Child(GameObject host, string name) { var go = new GameObject(name); go.transform.SetParent(host.transform); return go; }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); _checks++; }
    private static void SetPending(object pending, string name, object value) => pending.GetType().GetField(name).SetValue(pending, value);
    private static GameObject FragmentObject(object fragment) => (GameObject)fragment.GetType().GetField("go").GetValue(fragment);
    private static bool Oracle(Vector2 point)
    {
        foreach (var col in Physics2D.OverlapCircleAll(point, .35f)) if (col.GetComponent<WallMarker>() != null) return true;
        return false;
    }
    private static double Measure(Vector2[] points, bool legacy)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int pass = 0; pass < 50; pass++) for (int i = 0; i < points.Length; i++)
        {
            if (!legacy) { WallStuckResolver.HasWallOverlap(points[i]); continue; }
            int count = Physics2D.OverlapCircleNonAlloc(points[i], .35f, LegacyHits);
            for (int h = 0; h < count; h++) if (LegacyHits[h].TryGetComponent<WallMarker>(out _)) break;
        }
        watch.Stop(); return watch.Elapsed.TotalMilliseconds;
    }
    [Serializable] private sealed class Result
    {
        public string status = "PASS", environment = "Unity Editor Play Mode; not an iOS FPS measurement";
        public int checks, queriesPerSample, recycledFragmentRespawns, retainedFragments;
        public double legacyWallMedianMs, wallOnlyMedianMs;
        public long wallQueryAllocatedBytes;
    }
}
#endif
