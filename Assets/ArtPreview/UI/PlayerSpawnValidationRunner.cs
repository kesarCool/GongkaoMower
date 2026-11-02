#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Opening-frame placement in real battle scenes with an isolated profile.</summary>
public sealed class PlayerSpawnValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "PlayerSpawn";
    private bool previousBackground;
    private Vector3 mapOffset;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game") return;
        // Loading also holds a pause before the opening frame is revealed.
        Time.timeScale = 0;
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) spawner.enabled = false;
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) outcome.enabled = false;
        foreach (var unlock in FindObjectsOfType<GameUnlockSkill>()) unlock.enabled = false;
        BattleMapLoader.Instance.transform.position = mapOffset;
        var player = FindObjectOfType<PlayerController>();
        player.transform.position = new Vector3(-80, -90, player.transform.position.z);
    }

    protected override IEnumerator RunChecks()
    {
        TutorialDirector.Progress.skipped = true;
        TutorialDirector.Current?.Cancel();
        foreach (int chapter in new[] { 1, 2, 3 })
        {
            SelectedLevelContext.Set(chapter, chapter * 100 + 1);
            mapOffset = chapter == 2 ? new Vector3(42, -17, 0) : Vector3.zero;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Single);
            yield return null;
            var player = FindObjectOfType<PlayerController>();
            Vector2 center;
            bool hasCenter = BattlePlayerPositioning.TryGetMapCenter(out center);
            Record("Chapter " + chapter + " spawns at the current map center while paused", hasCenter &&
                Vector2.Distance(player.transform.position, center) < .001f,
                "Player=" + player.transform.position + "; map center=" + center);
            Record("Chapter " + chapter + " synchronizes body and clears movement", Vector2.Distance(
                player.GetComponent<Rigidbody2D>().position, center) < .001f &&
                !(bool)typeof(PlayerController).GetField("hasTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player));
            var camera = FindObjectOfType<CameraFollow2D>().GetComponent<Camera>();
            Vector3 viewport = camera.WorldToViewportPoint(player.transform.position);
            Record("Chapter " + chapter + " opens with the player centered on screen", Vector2.Distance(viewport, new Vector2(.5f, .5f)) < .001f);
            Record("Chapter " + chapter + " spawn does not overlap walls", !WallStuckResolver.HasWallOverlap(center));
            if (chapter == 2) Record("Translated map does not use a fixed world coordinate", Vector2.Distance(center, new Vector2(57, -2)) < .001f);
            yield return Capture("chapter-" + chapter + "-center");
        }

        var root = new GameObject("SpawnGroundFixture", typeof(Grid));
        root.transform.position = new Vector3(-100, -100, 0);
        var groundObject = new GameObject("Ground", typeof(Tilemap));
        groundObject.transform.SetParent(root.transform, false);
        var ground = groundObject.GetComponent<Tilemap>();
        var tile = ScriptableObject.CreateInstance<Tile>();
        Vector2 spawn;
        Record("Empty ground has no spawn", !BattlePlayerPositioning.TryGetSpawnPosition(ground, out spawn));
        for (int y = 0; y < 6; y++) for (int x = 0; x < 6; x++) ground.SetTile(new Vector3Int(x, y, 0), tile);
        Vector2 expected = new Vector2(-97, -97);
        Record("Even-sized ground uses its geometric center", BattlePlayerPositioning.TryGetSpawnPosition(ground, out spawn) && Vector2.Distance(spawn, expected) < .001f);
        ground.SetTile(new Vector3Int(3, 3, 0), null);
        Record("Center hole falls back to the nearest safe ground cell", BattlePlayerPositioning.TryGetSpawnPosition(ground, out spawn) &&
            ground.HasTile(ground.WorldToCell(spawn)) && Mathf.Abs(Vector2.Distance(spawn, expected) - Mathf.Sqrt(.5f)) < .001f);
        ground.SetTile(new Vector3Int(3, 3, 0), tile);
        var wall = new GameObject("SpawnWallFixture", typeof(BoxCollider2D));
        wall.transform.position = expected;
        wall.GetComponent<BoxCollider2D>().size = Vector2.one * 2;
        wall.AddComponent<WallMarker>();
        Physics2D.SyncTransforms();
        Record("Obstructed center falls back outside the wall", BattlePlayerPositioning.TryGetSpawnPosition(ground, out spawn) &&
            !WallStuckResolver.HasWallOverlap(spawn) && Vector2.Distance(spawn, expected) > 1);
        wall.SetActive(false);
        root.transform.localScale = Vector3.one * .05f;
        Record("Ground too small for the player does not return an unsafe spawn", !BattlePlayerPositioning.TryGetSpawnPosition(ground, out spawn));
        Destroy(wall); Destroy(root); Destroy(tile);
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.runInBackground = previousBackground;
        Time.timeScale = 1;
        base.OnDestroy();
    }
}
#endif
