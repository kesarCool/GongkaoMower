#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises actual wave preparation, table spawning, arena physics, and revival relocation.</summary>
public sealed class BossArenaEntryValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "BossArenaEntry/Transition";
    protected override string ValidationNote => "Actual Game scene and SpawnerWaves wave/spawn methods; isolated in-memory profile. Portal disappearance/arrival, pan/zoom continuity, pause/resume, input/damage protection, delayed spawn, multiple Bosses, physics, interruption cleanup and shared revival relocation. No WeChat device frame-time validation.";
    private UnityEngine.Random.State savedRandom;
    private float savedScale;
    private bool captured;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    protected override IEnumerator RunChecks()
    {
        savedRandom = UnityEngine.Random.state; savedScale = Time.timeScale; captured = true;
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return new WaitForSecondsRealtime(.35f);
        var spawner = FindObjectOfType<SpawnerWaves>();
        foreach (var item in FindObjectsOfType<SpawnerWaves>()) { item.StopAllCoroutines(); item.enabled = false; }
        foreach (var item in FindObjectsOfType<BattleOutcomeCoordinator>()) { item.StopAllCoroutines(); item.enabled = false; }
        foreach (var item in FindObjectsOfType<BossWheelController>()) item.enabled = false;
        foreach (var item in FindObjectsOfType<DynamicJoystick>()) item.enabled = false;
        UIManager.Instance.CloseAllStack(); Time.timeScale = 1;
        foreach (var item in FindObjectsOfType<EnemyBase>()) Destroy(item.gameObject);
        yield return null; CombatTargetRegistry.ClearAll(); BattleVictoryBossTracker.Reset();
        var skills = FindObjectOfType<PlayerSkills>(); skills.ClearAllSkills(); skills.enabled = false;
        var player = skills.transform; var controller = player.GetComponent<PlayerController>(); controller.enabled = false;
        var body = player.GetComponent<Rigidbody2D>(); body.gravityScale = 0;
        var health = player.GetComponent<PlayerHealth>(); health.SetInvulnerable(true); float hp = health.Hp;
        var arena = FindObjectOfType<BossArenaLock>();
        var follow = FindObjectOfType<CameraFollow2D>();
        var camera = Camera.main;
        bool centerKnown = BattlePlayerPositioning.TryGetMapCenter(out Vector2 center);
        Record("Actual map center and configured 16x20 arena exist", centerKnown && center == new Vector2(15, 15) && arena != null && arena.arenaHalfWidth == 8 && arena.arenaHalfHeight == 10);

        var normal = CreateNormal(new Vector2(9, 9));
        normal.TakeDamage(1);
        Record("Normal enemies do not open a Boss arena", !arena.IsLocked);
        player.position = new Vector3(4, 25, 0); body.position = player.position; body.velocity = new Vector2(3, -4);
        camera.transform.position = new Vector3(7, 23, -10);
        Vector3 departureCamera = camera.transform.position;
        float departureZoom = camera.orthographicSize;
        var bodySprite = player.Find("Body").GetComponent<SpriteRenderer>();
        Color originalColor = bodySprite.color;
        health.SetInvulnerable(false);
        Set(controller, "hasTarget", true); Set(controller, "targetPos", new Vector3(2, 2, 0));
        // Advance outer wave scheduling only: skip ordinary spawn enumerators to
        // reach the real Boss-wave branch without waiting through the whole level.
        spawner.useLevelWaveTable = true;
        var wave = (IEnumerator)typeof(SpawnerWaves).GetMethod("WaveRoutine", Hidden).Invoke(spawner, null);
        bool prepared = false;
        for (int step = 0; step < 100 && wave.MoveNext(); step++)
            if (arena.IsEntering) { prepared = true; break; }
        var entry = player.GetComponent<BossArenaEntryTransition>();
        Record("Real Boss-wave starts a portal without snapping the visible player or camera", prepared &&
            player.position == new Vector3(4, 25, 0) && camera.transform.position == departureCamera &&
            !arena.IsLocked && FindObjectsOfType<LastWaveBossMarker>().Length == 0);
        arena.PrepareBossWave();
        Record("Repeated preparation reuses one portal and one sequence", player.GetComponents<BossArenaEntryTransition>().Length == 1 && entry.IsPlaying && GameObject.Find("BossEntryPortals") != null);
        Record("Input and damage are protected throughout entry", controller.IsArenaEntryLocked && health.ArenaEntryProtected && body.velocity == Vector2.zero);
        health.TakeDamage(999999);
        Record("Hidden entry does not lose HP or trigger revival", health.IsAlive && health.Hp == hp);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, "01-departure.png"));
        float timeout = Time.realtimeSinceStartup + 3;
        while (!entry.IsHidden && Time.realtimeSinceStartup < timeout) yield return null;
        Record("Relocation occurs only after all player renderers are hidden", entry.IsHidden &&
            player.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff) && Vector2.Distance(player.position, center) < .001f);
        var joystick = FindObjectOfType<DynamicJoystick>(true);
        joystick.debugLogs = false; joystick.useExternalTouchEvents = true; joystick.enabled = true;
        joystick.OnTouchDown(new Vector2(200, 400)); joystick.OnTouchMove(new Vector2(280, 400));
        yield return new WaitForSeconds(.07f);
        Record("A held joystick cannot move the hidden player or restore momentum", controller.IsArenaEntryLocked &&
            body.velocity == Vector2.zero && Vector2.Distance(player.position, center) < .001f);
        joystick.OnTouchUp(); joystick.enabled = false;
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output, "02-hidden-travel.png"));
        Vector3 pausedCamera = camera.transform.position;
        float pausedZoom = camera.orthographicSize;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.15f);
        Record("Pause freezes both camera travel and portal timing", entry.IsHidden && entry.IsPlaying &&
            camera.transform.position == pausedCamera && camera.orthographicSize == pausedZoom,
            "position drift=" + Vector3.Distance(camera.transform.position, pausedCamera).ToString("F5") + ", zoom drift=" + Mathf.Abs(camera.orthographicSize - pausedZoom).ToString("F5"));
        Time.timeScale = 1;
        float maxPositionStep = 0f, maxZoomStep = 0f;
        Vector3 lastCamera = camera.transform.position;
        float lastZoom = camera.orthographicSize;
        bool monotonicZoom = true;
        while (entry.IsPlaying && Time.realtimeSinceStartup < timeout + 3)
        {
            yield return null;
            maxPositionStep = Mathf.Max(maxPositionStep, Vector3.Distance(camera.transform.position, lastCamera));
            maxZoomStep = Mathf.Max(maxZoomStep, Mathf.Abs(camera.orthographicSize - lastZoom));
            monotonicZoom &= camera.orthographicSize >= lastZoom - .001f;
            lastCamera = camera.transform.position; lastZoom = camera.orthographicSize;
        }
        Record("Camera completes one bounded monotonic zoom with no one-frame jump", !entry.IsPlaying && !follow.IsArenaTransitioning &&
            monotonicZoom && maxPositionStep < Vector3.Distance(departureCamera, new Vector3(center.x, center.y, -10)) * .5f &&
            maxZoomStep < departureZoom * .15f,
            "max frame pan=" + maxPositionStep.ToString("F3") + ", zoom=" + maxZoomStep.ToString("F3"));
        Record("Player arrives at map center before warnings and wall creation", !arena.IsLocked && Vector2.Distance(player.position, center) < .001f);
        Record("Teleport clears old click movement and physical momentum", body.position == center && body.velocity == Vector2.zero && !Get<bool>(controller, "hasTarget"));
        yield return null;
        Record("Arrival restores player visibility, color, control and damage protection", !bodySprite.forceRenderingOff && bodySprite.color == originalColor &&
            !controller.IsArenaEntryLocked && !health.ArenaEntryProtected && GameObject.Find("BossEntryPortals") == null);
        Record("Boss entry does not heal the player", health.Hp == hp);
        health.SetInvulnerable(true);
        yield return CaptureMotion(entry, follow, center);

        // Movement during the warning must not strand the player outside the wall.
        player.position = new Vector3(1.5f, 28, 0); body.position = player.position;
        var spawn = SpawnBoss(spawner, false);
        bool tick = spawn.MoveNext();
        var boss = FindObjectsOfType<LastWaveBossMarker>().Single().GetComponent<EnemyBase>();
        Freeze(boss);
        Record("Table-spawned Boss closes walls at full health before its first hit", tick && arena.IsLocked && boss.Hp == boss.MaxHp);
        Record("Leaving during a long warning uses a portal instead of a second visible snap", arena.IsEntering && player.position == new Vector3(1.5f, 28, 0));
        while (arena.IsEntering) yield return null;
        Record("Player moved during the warning arrives safely inside the walls", Vector2.Distance(player.position, center) < .001f && body.position == center);
        var walls = GameObject.Find("BossArenaWalls");
        Record("Four physical walls enclose both player and Boss", walls != null && walls.transform.childCount == 4 &&
            walls.GetComponentsInChildren<BoxCollider2D>().Length == 4 && walls.GetComponentsInChildren<WallMarker>().Length == 4 &&
            Inside(arena, player.position) && Inside(arena, boss.transform.position));
        Physics2D.SyncTransforms();
        bool blocked = walls.GetComponentsInChildren<BoxCollider2D>().All(c => {
            Vector2 inward = (center - (Vector2)c.transform.position).normalized;
            return Physics2D.RaycastAll((Vector2)c.transform.position + inward, -inward, 2).Any(h => h.collider == c);
        });
        Record("All four arena walls block physics queries", blocked);
        Record("Player and Boss are placed clear of the wall colliders", !WallStuckResolver.HasWallOverlap(player.position) && !WallStuckResolver.HasWallOverlap(boss.transform.position));
        while (spawn.MoveNext()) { Freeze(boss); yield return spawn.Current; }

        camera.transform.position = new Vector3(center.x, center.y, -10);
        yield return new WaitForSecondsRealtime(.4f);
        yield return Capture("boss-entry-1080x1920");
        player.position = new Vector3(13, 14, 0); body.position = player.position;
        boss.TakeDamage(1);
        Record("Boss damage does not teleport the player a second time or duplicate walls", player.position == new Vector3(13, 14, 0) && GameObject.Find("BossArenaWalls") == walls && walls.transform.childCount == 4);

        spawn = SpawnBoss(spawner, false);
        tick = spawn.MoveNext();
        var second = FindObjectsOfType<LastWaveBossMarker>().Select(m => m.GetComponent<EnemyBase>()).Single(e => e != boss);
        Freeze(second);
        Record("Additional Boss joins existing arena without recentering the player", tick && !arena.IsEntering && GameObject.Find("BossArenaWalls") == walls && player.position == new Vector3(13, 14, 0) && Inside(arena, second.transform.position));
        while (spawn.MoveNext()) { Freeze(second); yield return spawn.Current; }
        normal.TakeDamage(1);
        Record("Normal enemy damage leaves the current arena intact", arena.IsLocked && GameObject.Find("BossArenaWalls") == walls);
        boss.TakeDamage(1000000);
        Record("Defeating one of two Bosses retains the walls", arena.IsLocked);
        second.TakeDamage(1000000); yield return null;
        Record("Defeating the last Boss removes the walls and physical blockers", !arena.IsLocked && GameObject.Find("BossArenaWalls") == null);

        player.position = new Vector3(28, 2, 0); body.position = player.position; body.velocity = Vector2.one;
        typeof(BattleOutcomeCoordinator).GetMethod("TeleportToMapCenter", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
        Record("Revival still uses the identical map center and resets momentum", Vector2.Distance(player.position, center) < .001f && body.position == center && body.velocity == Vector2.zero);
        controller.enabled = true; yield return null; controller.enabled = false;
        Record("An old click destination does not drag the relocated player away", Vector2.Distance(player.position, center) < .001f);

        spawn = SpawnBoss(spawner, true); tick = spawn.MoveNext();
        var finalBoss = FindObjectsOfType<LastWaveBossMarker>().Single().GetComponent<EnemyBase>(); Freeze(finalBoss);
        Record("Final Boss also locks immediately without waiting for damage", tick && arena.IsLocked && Inside(arena, finalBoss.transform.position));
        while (!entry.IsHidden) yield return null;
        arena.enabled = false; yield return null;
        Record("Disabling during hidden travel restores visibility/input and cleans portals/walls", !arena.IsLocked && !entry.IsPlaying && !entry.IsHidden &&
            !bodySprite.forceRenderingOff && !controller.IsArenaEntryLocked && !health.ArenaEntryProtected &&
            GameObject.Find("BossArenaWalls") == null && GameObject.Find("BossEntryPortals") == null && !follow.IsArenaTransitioning);
        Destroy(normal.gameObject); Time.timeScale = savedScale; UnityEngine.Random.state = savedRandom;
    }

    private static IEnumerator SpawnBoss(SpawnerWaves spawner, bool final)
    {
        // Use a real configured Boss row, limiting only the test's spawn count/delay.
        var configured = (IEnumerable)typeof(SpawnerWaves).GetMethod("BuildTableWavesForLevel", Hidden).Invoke(spawner, new object[] { 101 });
        var type = typeof(SpawnerWaves).GetNestedType("TableWaveRuntime", BindingFlags.NonPublic);
        object row = configured.Cast<object>().First(r => (bool)type.GetField("isBoss").GetValue(r));
        type.GetField("totalMonster").SetValue(row, 1); type.GetField("quantityBoss").SetValue(row, 1);
        type.GetField("timeStart").SetValue(row, 0); type.GetField("waveTimeContinue").SetValue(row, 0);
        type.GetField("maxHp").SetValue(row, 10000); type.GetField("attack").SetValue(row, 0); type.GetField("moveSpeed").SetValue(row, 0f);
        var group = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type)); group.Add(row);
        return (IEnumerator)typeof(SpawnerWaves).GetMethod("TableWaveGroupRoutine", Hidden).Invoke(spawner, new object[] { group, 1, 1, final });
    }
    private IEnumerator CaptureMotion(BossArenaEntryTransition entry, CameraFollow2D follow, Vector2 center)
    {
        changeResolution?.Invoke(540, 960);
        yield return new WaitForSecondsRealtime(.2f);
        var player = entry.transform;
        BattlePlayerPositioning.Teleport(player, new Vector2(9, 21));
        follow.SetBossZoom(false);
        Camera.main.orthographicSize = Get<float>(follow, "_defaultOrthoSize");
        follow.transform.position = new Vector3(9, 21, -10);
        yield return null;
        string folder = Path.Combine(output, "portal-motion");
        Directory.CreateDirectory(Path.Combine(folder, "frames"));
        var times = new List<float>();
        float start = Time.realtimeSinceStartup;
        bool started = false;
        while (Time.realtimeSinceStartup - start < 1.9f)
        {
            if (!started && Time.realtimeSinceStartup - start >= .2f) { entry.Play(center, follow); started = true; }
            yield return new WaitForEndOfFrame();
            times.Add(Time.realtimeSinceStartup - start);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frames", "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            yield return new WaitForSecondsRealtime(.045f);
        }
        File.WriteAllText(Path.Combine(folder, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        File.WriteAllText(Path.Combine(folder, "motion.json"), "{\"timeScale\":1,\"retimed\":false,\"frames\":" + times.Count + "}");
        Record("Actual portal footage records departure, camera travel and completed arrival", started && !entry.IsPlaying && times.Count > 15 && Vector2.Distance(player.position, center) < .001f);
        changeResolution?.Invoke(1080, 1920);
        yield return null;
    }
    private static EnemyBase CreateNormal(Vector2 position)
    {
        var enemy = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab"), position, Quaternion.identity).GetComponent<EnemyBase>();
        enemy.ApplyTableStats(1, 10000, 0, 0); Freeze(enemy); return enemy;
    }
    private static void Freeze(EnemyBase enemy)
    {
        foreach (var behavior in enemy.GetComponentsInChildren<MonoBehaviour>())
            if (!(behavior is EnemyBase) && !(behavior is EnemyWordLabel) && !(behavior is TMP_Text)) behavior.enabled = false;
        var body = enemy.GetComponent<Rigidbody2D>(); if (body != null) { body.bodyType = RigidbodyType2D.Kinematic; body.velocity = Vector2.zero; }
    }
    private static bool Inside(BossArenaLock arena, Vector2 position) => Vector2.Distance(position, arena.ClampInside(position)) < .001f;
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Hidden).SetValue(instance, value);
    private static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Hidden).GetValue(instance);
    protected override void OnDestroy()
    {
        if (captured) { Time.timeScale = savedScale; UnityEngine.Random.state = savedRandom; }
        base.OnDestroy();
    }
}
#endif
