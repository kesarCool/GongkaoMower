using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Updates only Ground tiles in the eight existing prefabs, preserving approved decorations.</summary>
public static class AutumnAcademyTerrainMigration
{
    public const string Preview = "ArtOutput/World/AutumnAcademyTerrain";
    [Serializable] public class MapCheck
    {
        public int stage, groundCells;
        public string prefab, beforeShapeHash, afterShapeHash;
        public bool guidPreserved, decorationPreserved, geometryPreserved, autumnTerrainOnly, referencesValid, loadable, noSourceDependency, neighborEdgesMatch;
    }
    [Serializable] public class Report
    {
        public bool passed, sceneUnchanged, sharedArtUnchanged, bambooMapsUnchanged, chapterOneUnchanged;
        public int tileTextures, tilePixels = 64;
        public long tilePngSourceBytes, tileTextureStorageBytes;
        public MapCheck[] maps;
        public string note = "Only Ground Tile references changed. Individual 64px sprites cover one world unit. Imported texture storage is not a fresh WeChat package measurement.";
    }

    [MenuItem("Tools/Maps/Chapter Two/Apply Autumn Terrain Only")]
    private static void Menu() { Debug.Log(Apply()); }
    [MenuItem("Tools/Maps/Chapter Two/Apply Autumn Terrain Only", true)]
    private static bool CanApply() { return !EditorApplication.isPlayingOrWillChangePlaymode; }

    public static string Prepare()
    {
        if (!CanApply()) throw new InvalidOperationException("Exit Play mode first.");
        var names = Enumerable.Range(1, 8).SelectMany(stage => CartoonTilePlan.Build(Rows(AssetDatabase.LoadAssetAtPath<GameObject>(ChapterTwoMapGenerator.MapPath(stage))), AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background));
        var tiles = AutumnAcademyTerrainBuilder.Ensure(names);
        return "Prepared " + tiles.Count + " autumn terrain tiles without touching map prefabs.";
    }

    public static void RunBatch()
    {
        try { Debug.Log(Apply()); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static string Apply()
    {
        if (!CanApply()) throw new InvalidOperationException("Exit Play mode first.");
        var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty; int count = SceneManager.sceneCount;
        var shared = FilesUnder(ChapterOneMapGenerator.Art);
        var bamboo = MapResourcePaths.BambooFiles().ToDictionary(p => p, HashFile);
        var first = Directory.GetFiles("Assets/Resources/Map", "TileMap1_*.prefab*").ToDictionary(p => p, HashFile);
        string backup = "Library/MapBackups/AutumnTerrain-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(Preview);
        var rows = Enumerable.Range(1, 8).ToDictionary(stage => stage, stage => Rows(AssetDatabase.LoadAssetAtPath<GameObject>(ChapterTwoMapGenerator.MapPath(stage))));
        var tiles = AutumnAcademyTerrainBuilder.Ensure(rows.Values.SelectMany(r => CartoonTilePlan.Build(r, AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background)));
        var checks = new List<MapCheck>();
        foreach (int stage in Enumerable.Range(1, 8))
        {
            string path = ChapterTwoMapGenerator.MapPath(stage);
            File.Copy(path, backup + "/" + Path.GetFileName(path)); File.Copy(path + ".meta", backup + "/" + Path.GetFileName(path) + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(path);
            var contents = PrefabUtility.LoadPrefabContents(path);
            string decoration;
            try
            {
                var ground = contents.GetComponentInChildren<Tilemap>();
                decoration = DecorationHash(contents);
                var names = CartoonTilePlan.Build(rows[stage], AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background);
                for (int y = 0; y < 30; y++) for (int x = 0; x < 30; x++) ground.SetTile(new Vector3Int(x, 29 - y, 0), tiles[names[y * 30 + x]]);
                if (decoration != DecorationHash(contents)) throw new InvalidOperationException("Unexpected decoration edit: " + path);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); var tilemap = prefab.GetComponentInChildren<Tilemap>();
            var all = tilemap.GetTilesBlock(tilemap.cellBounds);
            // A fresh instance verifies saved decoration serialization, not the in-memory rendering state.
            var saved = PrefabUtility.LoadPrefabContents(path);
            bool sameDecoration;
            try { sameDecoration = decoration == DecorationHash(saved); }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
            var check = new MapCheck {
                stage = stage, prefab = path, guidPreserved = guid == AssetDatabase.AssetPathToGUID(path),
                beforeShapeHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", rows[stage]))), afterShapeHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", Rows(prefab)))),
                decorationPreserved = sameDecoration, groundCells = all.Count(t => t != null),
                geometryPreserved = tilemap.cellBounds == new BoundsInt(0, 0, 0, 30, 30, 1) && prefab.GetComponentsInChildren<Collider2D>(true).Length == 0,
                autumnTerrainOnly = all.All(t => t != null && AssetDatabase.GetAssetPath(t).StartsWith(AutumnAcademyTerrainBuilder.Root + "/Tiles/", StringComparison.Ordinal) && Mathf.Approximately(((Tile)t).sprite.bounds.size.x, 1)),
                referencesValid = ChapterOneMapGenerator.ReferencesValid(prefab), loadable = Resources.Load<GameObject>("Map/TileMap2_" + stage) == prefab,
                neighborEdgesMatch = AutumnAcademyTerrainBuilder.ValidateNeighborEdges(rows[stage]),
                noSourceDependency = !AssetDatabase.GetDependencies(path, true).Any(p => p.StartsWith(AutumnAcademyTerrainBuilder.Root + "/Source/", StringComparison.Ordinal))
            };
            checks.Add(check);
            var background = new Color(.74f, .51f, .37f);
            ChapterOneMapGenerator.Render(prefab, Preview + "/TileMap2_" + stage + ".png", false, background);
            ChapterOneMapGenerator.Render(prefab, Preview + "/TileMap2_" + stage + "-phone.png", true, background);
        }
        var texturePaths = tiles.Values.Select(t => AssetDatabase.GetAssetPath(t.sprite)).Distinct().ToArray();
        var report = new Report {
            maps = checks.ToArray(), sceneUnchanged = scene == SceneManager.GetActiveScene() && scene.isDirty == dirty && count == SceneManager.sceneCount,
            sharedArtUnchanged = Unchanged(shared), bambooMapsUnchanged = Unchanged(bamboo), chapterOneUnchanged = Unchanged(first),
            tileTextures = texturePaths.Length, tilePngSourceBytes = texturePaths.Sum(p => new FileInfo(p).Length),
            tileTextureStorageBytes = texturePaths.Sum(AutumnAcademyTerrainBuilder.TextureStorage)
        };
        report.passed = report.sceneUnchanged && report.sharedArtUnchanged && report.bambooMapsUnchanged && report.chapterOneUnchanged && checks.All(c =>
            c.guidPreserved && c.decorationPreserved && c.beforeShapeHash == c.afterShapeHash && c.groundCells == 900 && c.geometryPreserved && c.autumnTerrainOnly && c.referencesValid && c.loadable && c.noSourceDependency && c.neighborEdgesMatch);
        File.WriteAllText(Preview + "/build-report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        if (!report.passed) throw new InvalidOperationException("Autumn terrain verification failed: " + Preview + "/build-report.json");
        return JsonUtility.ToJson(report, true);
    }

    private static string[] Rows(GameObject prefab)
    {
        if (prefab == null) throw new InvalidOperationException("Missing chapter-two map prefab.");
        var ground = prefab.GetComponentInChildren<Tilemap>();
        if (ground == null || ground.cellBounds != new BoundsInt(0, 0, 0, 30, 30, 1)) throw new InvalidOperationException("Expected complete 30x30 Ground: " + prefab.name);
        var rows = new string[30];
        for (int y = 0; y < 30; y++)
        {
            var row = new char[30];
            for (int x = 0; x < 30; x++)
            {
                var tile = ground.GetTile(new Vector3Int(x, 29 - y, 0));
                if (tile == null) throw new InvalidOperationException("Missing ground tile: " + prefab.name);
                if (tile.name.StartsWith("stone_", StringComparison.Ordinal) || tile.name.StartsWith("autumn_stone_", StringComparison.Ordinal)) row[x] = '#';
                else if (tile.name.StartsWith("grass_", StringComparison.Ordinal) || tile.name.StartsWith("autumn_earth_", StringComparison.Ordinal)) row[x] = '.';
                else throw new InvalidOperationException("Unknown terrain: " + tile.name);
            }
            rows[y] = new string(row);
        }
        return rows;
    }

    private static string DecorationHash(GameObject root)
    {
        var decoration = root.transform.Find("Decoration");
        if (decoration == null) throw new InvalidOperationException("Missing Decoration node.");
        var text = new StringBuilder();
        foreach (var transform in decoration.GetComponentsInChildren<Transform>(true))
        {
            text.Append(transform.name).Append('|').Append(transform.GetSiblingIndex()).Append('|').Append(transform.gameObject.activeSelf).Append('|').Append(transform.gameObject.layer);
            text.Append(JsonUtility.ToJson(transform.localPosition)).Append(JsonUtility.ToJson(transform.localRotation)).Append(JsonUtility.ToJson(transform.localScale));
            var renderer = transform.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                text.Append(AssetDatabase.GetAssetPath(renderer.sprite)).Append('|').Append(renderer.enabled).Append('|').Append(renderer.flipX).Append('|').Append(renderer.flipY);
                text.Append('|').Append(renderer.sortingLayerID).Append('|').Append(renderer.sortingOrder).Append(JsonUtility.ToJson(renderer.color));
                text.Append(AssetDatabase.GetAssetPath(renderer.sharedMaterial));
            }
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }
    private static Dictionary<string, string> FilesUnder(string directory) { return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToDictionary(p => p, HashFile); }
    private static bool Unchanged(Dictionary<string, string> files) { return files.All(p => File.Exists(p.Key) && HashFile(p.Key) == p.Value); }
    private static string HashFile(string path) { return Hash(File.ReadAllBytes(path)); }
    private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
}
