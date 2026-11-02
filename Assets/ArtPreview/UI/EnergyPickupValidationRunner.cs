#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Uses the real Energy prefab, pool, player and energy ledger, with deterministic frame sizes.</summary>
public sealed class EnergyPickupValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "EnergyPickup";
    protected override string ValidationNote => "Real Game scene, PlayerEnergy and pooled Energy prefab. Deterministic 15/30/60 fps path checks plus actual Physics2D contact/movement. In-memory profile; no device timing measurement.";
    private static readonly MethodInfo Step = typeof(EnergyPickup).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo Contact = typeof(EnergyPickup).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
    private bool previousBackground;
    private float previousTimeScale;
    private EnergyPickup prefab;
    private PlayerEnergy energy;
    private Transform player;
    private Rigidbody2D body;
    private EnergyLevelConfig testConfig;
    private readonly Vector3 origin = new Vector3(15, 15, .45f);

    private void Awake()
    {
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        previousTimeScale = Time.timeScale;
    }

    protected override IEnumerator RunChecks()
    {
        TutorialDirector.Progress.skipped = true; TutorialDirector.Current?.Cancel();
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null; yield return null;
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var system in FindObjectsOfType<CardSelectionSystem>()) { system.StopAllCoroutines(); system.enabled = false; }
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) { outcome.StopAllCoroutines(); outcome.enabled = false; }
        foreach (var joystick in FindObjectsOfType<DynamicJoystick>()) joystick.enabled = false;
        foreach (var skills in FindObjectsOfType<PlayerSkills>()) { skills.ClearAllSkills(); skills.enabled = false; }
        foreach (var enemy in FindObjectsOfType<EnemyBase>()) Destroy(enemy.gameObject);
        foreach (var old in FindObjectsOfType<EnergyPickup>())
        { SpawnLimiter.Instance?.Unregister("EnergyPickup", old.gameObject); GameObjectPool.Release(old.gameObject); }
        UIManager.Instance.CloseAllStack();
        energy = FindObjectOfType<PlayerEnergy>(); player = energy.transform; body = player.GetComponent<Rigidbody2D>();
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<EnergyCollector>().enabled = false;
        player.GetComponent<PlayerHealth>().SetInvulnerable(true);
        testConfig = ScriptableObject.CreateInstance<EnergyLevelConfig>(); testConfig.levelThresholds = new[] { 100000 };
        energy.levelConfig = testConfig; Time.timeScale = 0;
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Energy.prefab").GetComponent<EnergyPickup>();
        Record("Existing orb art, amount and attraction range retained", prefab.amount == 1 && Mathf.Approximately(prefab.suctionRadius, 1) && prefab.GetComponent<SpriteRenderer>().sprite != null);
        Record("Prefab collider is a trigger before spawning", prefab.GetComponent<Collider2D>().isTrigger);

        foreach (int fps in new[] { 15, 30, 60 })
        {
            CheckPath("Stationary", fps, false, _ => Vector3.zero);
            CheckPath("Running 7 units/s", fps, false, t => new Vector3(7 * t, 0, 0));
            CheckPath("Upgraded 24 units/s", fps, false, t => new Vector3(24 * t, 0, 0));
            CheckPath("Rapid reversal", fps, false, t => new Vector3(.5f * Mathf.Sin(t * 40), .5f * Mathf.Cos(t * 40) - .5f, 0));
            CheckPath("Automatic collector at 40 units/s", fps, true, t => new Vector3(40 * t, 0, 0));
        }

        ResetPlayer(); int before = energy.energy;
        var pickup = Spawn(new Vector3(0, 0, -2)); Simulate(pickup, 1f / 60);
        Record("2D collection ignores sprite sorting Z", !pickup.gameObject.activeSelf && energy.energy == before + 1);

        ResetPlayer(); before = energy.energy;
        pickup = Spawn(new Vector3(4.8f, 0, -origin.z)); pickup.ForceCollectBy(player);
        Vector3 paused = pickup.transform.position; Simulate(pickup, 0);
        Record("Paused attraction does not move or credit energy", pickup.gameObject.activeSelf && pickup.transform.position == paused && energy.energy == before);
        Simulate(pickup, .5f);
        Record("Large frame step collects immediately without overshoot", !pickup.gameObject.activeSelf && energy.energy == before + 1);

        ResetPlayer(); before = energy.energy;
        pickup = Spawn(new Vector3(.9f, 0, -origin.z)); pickup.lifeTime = .03f;
        for (int i = 0; i < 30 && pickup.gameObject.activeSelf; i++) Simulate(pickup, 1f / 60);
        Record("Latched pickup finishes even when its ground lifetime expires", !pickup.gameObject.activeSelf && energy.energy == before + 1);
        pickup = Spawn(new Vector3(10, 0, -origin.z)); pickup.lifeTime = .03f; Simulate(pickup, .05f);
        Record("Unattracted expired pickup still despawns without credit", !pickup.gameObject.activeSelf && energy.energy == before + 1);

        ResetPlayer(); before = energy.energy;
        pickup = Spawn(new Vector3(4.8f, 0, -origin.z)); pickup.ForceCollectBy(player);
        SetPlayer(origin + Vector3.left * 8);
        pickup.ForceCollectBy(player); pickup.ForceCollectBy(player); Simulate(pickup, .2f);
        Record("Repeated auto-collect requests keep travel compensation and credit once", !pickup.gameObject.activeSelf && energy.energy == before + 1);

        ResetPlayer(); before = energy.energy;
        var child = new GameObject("PickupTestChildCollider"); child.transform.SetParent(player, false);
        var childCollider = child.AddComponent<CircleCollider2D>(); childCollider.enabled = false;
        pickup = Spawn(new Vector3(1, 0, -origin.z)); int id = pickup.GetInstanceID();
        Contact.Invoke(pickup, new object[] { childCollider }); Contact.Invoke(pickup, new object[] { childCollider });
        pickup.ForceCollectBy(player); Simulate(pickup, .2f);
        Record("Untagged child collider collects only once even with duplicate callbacks", !pickup.gameObject.activeSelf && energy.energy == before + 1);
        Destroy(child);
        pickup = Spawn(new Vector3(10, 0, -origin.z));
        Simulate(pickup, .01f);
        Record("Pool reuse resets old target and collected state", pickup.GetInstanceID() == id && pickup.gameObject.activeSelf && energy.energy == before + 1);
        GameObjectPool.Release(pickup.gameObject);

        // Actual trigger contact, rather than a manually invoked collision callback.
        ResetPlayer(); before = energy.energy;
        pickup = Spawn(new Vector3(.3f, 0, -origin.z)); pickup.GetComponent<Collider2D>().enabled = true;
        Time.timeScale = 1; Physics2D.SyncTransforms();
        yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
        Record("Real physics contact collects without bouncing back out", !pickup.gameObject.activeSelf && energy.energy == before + 1);

        ResetPlayer(); before = energy.energy;
        pickup = Spawn(new Vector3(-.9f, 0, -origin.z)); pickup.GetComponent<Collider2D>().enabled = true;
        // Latch before fleeing, then let real LateUpdate/physics run at normal time.
        Simulate(pickup, .001f); body.velocity = new Vector2(18, 0);
        float started = Time.realtimeSinceStartup;
        while (pickup.gameObject.activeSelf && Time.realtimeSinceStartup - started < 1.5f) yield return null;
        body.velocity = Vector2.zero;
        Record("Actual moving Rigidbody collects at 18 units/s", !pickup.gameObject.activeSelf && energy.energy == before + 1,
            "Real elapsed seconds=" + (Time.realtimeSinceStartup - started));
        if (pickup.gameObject.activeSelf) GameObjectPool.Release(pickup.gameObject);
        Time.timeScale = 0;
        SpawnLimiter.Instance.CanSpawn("EnergyPickup", out var config);
        Record("All tested pickups unregistered from the spawn limiter", config == null || config.currentAlive == 0);
        yield return null;
    }

    private void CheckPath(string label, int fps, bool forced, Func<float, Vector3> path)
    {
        ResetPlayer(); int before = energy.energy;
        var pickup = Spawn(new Vector3(forced ? -4.8f : -.9f, 0, -origin.z));
        if (forced) pickup.ForceCollectBy(player);
        float dt = 1f / fps; Simulate(pickup, dt);
        float previousDistance = Vector2.Distance(player.position, pickup.transform.position);
        float elapsed = dt; bool monotonic = true;
        for (int n = 1; n <= fps && pickup.gameObject.activeSelf; n++)
        {
            SetPlayer(origin + path(n * dt)); Simulate(pickup, dt); elapsed += dt;
            if (!pickup.gameObject.activeSelf) break;
            float distance = Vector2.Distance(player.position, pickup.transform.position);
            monotonic &= distance <= previousDistance + .0001f;
            previousDistance = distance;
        }
        Record(label + " " + fps + "fps closes without orbiting and credits exactly once",
            monotonic && !pickup.gameObject.activeSelf && energy.energy == before + 1 && elapsed < .6f,
            "Collection seconds=" + elapsed + "; gap always closed=" + monotonic);
        if (pickup.gameObject.activeSelf) GameObjectPool.Release(pickup.gameObject);
    }
    private EnergyPickup Spawn(Vector3 offset)
    {
        var pickup = GameObjectPool.Get(prefab, origin + offset, Quaternion.identity);
        pickup.amount = 1; pickup.lifeTime = prefab.lifeTime; pickup.groundRestTime = 0;
        pickup.GetComponent<Collider2D>().enabled = false;
        SpawnLimiter.Instance?.RegisterSpawned("EnergyPickup", pickup.gameObject);
        return pickup;
    }
    private void ResetPlayer() { SetPlayer(origin); body.velocity = Vector2.zero; }
    private void SetPlayer(Vector3 position) { player.position = position; body.position = position; }
    private static void Simulate(EnergyPickup pickup, float dt) => Step.Invoke(pickup, new object[] { dt });
    protected override void OnDestroy()
    {
        Time.timeScale = previousTimeScale; Application.runInBackground = previousBackground;
        if (testConfig != null) Destroy(testConfig);
        base.OnDestroy();
    }
}
#endif
