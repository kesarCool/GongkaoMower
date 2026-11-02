#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ten-second frame summaries for comparing the same battle on mobile devices.</summary>
public sealed class BattlePerformanceSampler : MonoBehaviour
{
    private readonly float[] _frames = new float[1200];
    private int _count, _over50, _over100;
    private float _elapsed, _maxMs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach(scene);

    private static void Attach(Scene scene)
    {
        if (scene.name != GameObjectPool.BattleSceneNameForPoolClear) return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<BattlePerformanceSampler>() != null) return;
        var host = new GameObject("BattlePerformanceSampler");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<BattlePerformanceSampler>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) { ResetWindow(); return; }
        float seconds = Time.unscaledDeltaTime;
        float ms = seconds * 1000f;
        if (_count < _frames.Length) _frames[_count] = ms;
        _count++;
        _elapsed += seconds;
        _maxMs = Mathf.Max(_maxMs, ms);
        if (ms > 50f) _over50++;
        if (ms > 100f) _over100++;
        if (_elapsed < 10f) return;

        int samples = Mathf.Min(_count, _frames.Length);
        Array.Sort(_frames, 0, samples);
        var shards = WordMonsterShatterRuntime.Instance;
        var report = new Summary
        {
            revision = AppBootstrap.PerformanceRevision,
            battleSeconds = Time.timeSinceLevelLoad,
            windowSeconds = _elapsed,
            frameCount = _count,
            fps = _count / _elapsed,
            frameP95Ms = _frames[Mathf.Clamp(Mathf.CeilToInt(samples * .95f) - 1, 0, samples - 1)],
            frameP99Ms = _frames[Mathf.Clamp(Mathf.CeilToInt(samples * .99f) - 1, 0, samples - 1)],
            frameMaxMs = _maxMs,
            framesOver50Ms = _over50,
            framesOver100Ms = _over100,
            enemies = CombatTargetRegistry.CountActive("monster"),
            fragments = shards != null ? shards.ActiveFragmentCount : 0,
            pendingFragments = shards != null ? shards.PendingFragmentCount : 0,
            unityAllocatedMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
            renderWidth = Screen.width,
            renderHeight = Screen.height,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            targetFps = Application.targetFrameRate,
            vsync = QualitySettings.vSyncCount,
            fixedDeltaTime = Time.fixedDeltaTime,
            maximumDeltaTime = Time.maximumDeltaTime
        };
        Debug.Log("[BattlePerf] " + JsonUtility.ToJson(report));
        ResetWindow();
    }

    private void OnApplicationPause(bool paused) => ResetWindow();

    private void ResetWindow()
    {
        _count = _over50 = _over100 = 0;
        _elapsed = _maxMs = 0f;
    }

    [Serializable]
    private sealed class Summary
    {
        public float battleSeconds, windowSeconds, fps, frameP95Ms, frameP99Ms, frameMaxMs, unityAllocatedMB;
        public float fixedDeltaTime, maximumDeltaTime;
        public int framesOver50Ms, framesOver100Ms, enemies, fragments, pendingFragments, renderWidth, renderHeight;
        public int frameCount, targetFps, vsync;
        public string revision, quality;
    }
}
#endif
