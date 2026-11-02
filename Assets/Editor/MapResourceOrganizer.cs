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

/// <summary>One-time GUID-preserving organization plus repeatable, read-only map validation.</summary>
public static class MapResourceOrganizer
{
    public const string Reports = "ArtOutput/World/MapResourceOrganization";
    [Serializable] public class Move { public string from, to; }
    [Serializable] public class FileRecord
    {
        public string from, to, hash, guid;
        public bool bytesPreserved, guidPreserved;
    }
    [Serializable] public class Receipt
    {
        public string backup;
        public bool passed, sceneUnchanged;
        public Move[] moves;
        public FileRecord[] files;
    }
    [Serializable] public class MapCheck
    {
        public string prefab, resourcesPath;
        public int cells, decorations;
        public bool loadable, referencesValid, gridValid, artCentralized, terrainValid;
    }
    [Serializable] public class Validation
    {
        public bool passed, fallbackLoadable;
        public int uniqueTextures;
        public MapCheck[] maps;
        public string note = "ZlMap1..8 are in Resources and will be included in builds. Texture files are moved, not duplicated; source bytes are not package sizes.";
    }

    static Move[] Moves()
    {
        var moves = new List<Move> {
            new Move { from = "Assets/ArtPreview/CartoonMaps_32_db482fa081", to = MapResourcePaths.SharedCartoon },
            new Move { from = "Assets/Res/Map/AutumnAcademy", to = MapResourcePaths.AutumnAcademy },
            new Move { from = "Assets/Res/Map/GeneratedTiles", to = MapResourcePaths.Common + "/GeneratedTiles" },
            new Move { from = "Assets/Res/Map/Palettes", to = MapResourcePaths.Common + "/Palettes" }
        };
        for (int stage = 1; stage <= 8; stage++) moves.Add(new Move {
            from = "Assets/Res/Map/Library/BambooAcademy/Prefabs/TileMap2_" + stage + ".prefab", to = MapResourcePaths.BambooMap(stage)
        });
        return moves.ToArray();
    }

    [MenuItem("Tools/Maps/Resources/Organize Existing Map Resources")]
    static void Menu() { Debug.Log(Organize()); }
    [MenuItem("Tools/Maps/Resources/Validate Map Resources")]
    static void ValidateMenu() { Debug.Log(Validate()); }

    public static string Organize()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var moves = Moves();
        foreach (var move in moves)
        {
            bool source = Exists(move.from), destination = Exists(move.to);
            if (source == destination) throw new IOException("Expected exactly one existing location: " + move.from + " -> " + move.to);
        }
        if (moves.All(m => !Exists(m.from))) return Validate();
        var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty; int count = SceneManager.sceneCount;
        var records = new List<FileRecord>();
        foreach (var move in moves.Where(m => Exists(m.from)))
        {
            var paths = Directory.Exists(move.from) ? Directory.GetFiles(move.from, "*", SearchOption.AllDirectories).Concat(new[] { move.from + ".meta" }) : new[] { move.from, move.from + ".meta" };
            foreach (string raw in paths)
            {
                string path = raw.Replace('\\', '/');
                records.Add(new FileRecord { from = path, to = move.to + path.Substring(move.from.Length), hash = ContentHash(path, path),
                    guid = path.EndsWith(".meta", StringComparison.Ordinal) ? "" : AssetDatabase.AssetPathToGUID(path) });
            }
        }
        // Existing active maps and their metas must stay byte-identical.
        foreach (string path in Directory.GetFiles(MapResourcePaths.RuntimeMaps, "TileMap*.prefab*"))
            records.Add(new FileRecord { from = path.Replace('\\', '/'), to = path.Replace('\\', '/'), hash = ContentHash(path, path),
                guid = path.EndsWith(".meta", StringComparison.Ordinal) ? "" : AssetDatabase.AssetPathToGUID(path) });
        string backup = "Library/MapBackups/MapResourceOrganization-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        foreach (var record in records)
        {
            string destination = backup + "/" + record.from;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(record.from, destination);
        }
        Directory.CreateDirectory(Reports);
        var receipt = new Receipt { backup = backup, moves = moves, files = records.ToArray() };
        File.WriteAllText(Reports + "/move-receipt.json", JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
        foreach (var move in moves.Where(m => Exists(m.from)))
        {
            EnsureFolder(Path.GetDirectoryName(move.to).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(move.from, move.to);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        // AssetDatabase moves GUID references; this importer cache also contains literal path strings.
        string reportPath = MapResourcePaths.SharedCartoon + "/BuildReport.json";
        string report = File.ReadAllText(reportPath, Encoding.UTF8).Replace("Assets/ArtPreview/CartoonMaps_32_db482fa081", MapResourcePaths.SharedCartoon);
        File.WriteAllText(reportPath, report, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(reportPath, ImportAssetOptions.ForceSynchronousImport);
        foreach (var record in records)
        {
            bool isImporterReport = record.to == reportPath;
            record.bytesPreserved = isImporterReport ? report.Replace(MapResourcePaths.SharedCartoon, "Assets/ArtPreview/CartoonMaps_32_db482fa081") == File.ReadAllText(backup + "/" + record.from, Encoding.UTF8) : ContentHash(record.to, record.from) == record.hash;
            record.guidPreserved = string.IsNullOrEmpty(record.guid) || record.guid == AssetDatabase.AssetPathToGUID(record.to);
        }
        // Remove only the verified-empty former library, never assets or backups.
        string empty = "Assets/Res/Map/Library";
        if (Directory.Exists(empty) && !Directory.GetFiles(empty, "*", SearchOption.AllDirectories).Any(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
            if (!AssetDatabase.DeleteAsset(empty)) throw new IOException("Could not remove empty former library.");
        AssetDatabase.SaveAssets();
        receipt.sceneUnchanged = scene == SceneManager.GetActiveScene() && scene.isDirty == dirty && count == SceneManager.sceneCount;
        receipt.passed = receipt.sceneUnchanged && records.All(r => r.bytesPreserved && r.guidPreserved);
        File.WriteAllText(Reports + "/move-receipt.json", JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
        if (!receipt.passed) throw new InvalidOperationException("Resource preservation failed. See " + Reports + "/move-receipt.json");
        return Validate();
    }

    public static string Validate()
    {
        var checks = new List<MapCheck>(); var textures = new HashSet<string>();
        var required = Enumerable.Range(1, 8).Select(i => MapResourcePaths.RuntimeMaps + "/TileMap1_" + i + ".prefab")
            .Concat(Enumerable.Range(1, 8).Select(ChapterTwoMapGenerator.MapPath)).Concat(Enumerable.Range(1, 8).Select(MapResourcePaths.BambooMap));
        var paths = Directory.GetFiles(MapResourcePaths.RuntimeMaps, "*.prefab").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new IOException("Missing map: " + path);
            var ground = prefab.GetComponentInChildren<Tilemap>();
            var deps = AssetDatabase.GetDependencies(path, true);
            foreach (string texture in deps.Where(p => p.EndsWith(".png", StringComparison.Ordinal))) textures.Add(texture);
            string key = "Map/" + Path.GetFileNameWithoutExtension(path);
            var tiles = ground.GetTilesBlock(ground.cellBounds);
            string terrainRoot = path.Contains("/TileMap2_") ? AutumnAcademyTerrainBuilder.Root + "/Tiles/"
                : path.Contains("/TileMap1_") || path.Contains("/ZlMap") ? MapResourcePaths.SharedCartoon + "/Tiles/" : MapResourcePaths.Styles + "/";
            checks.Add(new MapCheck {
                prefab = path, resourcesPath = key, cells = tiles.Count(t => t != null), decorations = prefab.GetComponentsInChildren<SpriteRenderer>(true).Length,
                loadable = Resources.Load<GameObject>(key) == prefab, referencesValid = ChapterOneMapGenerator.ReferencesValid(prefab),
                gridValid = ground.cellBounds == new BoundsInt(0, 0, 0, 30, 30, 1) && prefab.GetComponentsInChildren<Collider2D>(true).Length == 0,
                artCentralized = deps.Where(p => p.EndsWith(".png", StringComparison.Ordinal) || p.EndsWith(".asset", StringComparison.Ordinal)).All(p => p.StartsWith(MapResourcePaths.Styles + "/", StringComparison.Ordinal)),
                terrainValid = tiles.All(t => t != null && AssetDatabase.GetAssetPath(t).StartsWith(terrainRoot, StringComparison.Ordinal))
            });
        }
        var result = new Validation { maps = checks.ToArray(), uniqueTextures = textures.Count, fallbackLoadable = Resources.Load<GameObject>("Map/TileMap1_1") != null };
        result.passed = result.fallbackLoadable && required.All(File.Exists) && checks.All(c => c.cells == 900 && c.loadable && c.referencesValid && c.gridValid && c.artCentralized && c.terrainValid);
        Directory.CreateDirectory(Reports);
        string json = JsonUtility.ToJson(result, true);
        File.WriteAllText(Reports + "/validation.json", json, new UTF8Encoding(false));
        if (!result.passed) throw new InvalidOperationException("Map validation failed. See " + Reports + "/validation.json");
        return json;
    }

    static bool Exists(string path) { return File.Exists(path) || Directory.Exists(path); }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        if (string.IsNullOrEmpty(guid)) throw new IOException("Could not create asset folder: " + path);
    }
    static string ContentHash(string path, string original)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (original.Contains("/Library/BambooAcademy/") && original.EndsWith(".prefab", StringComparison.Ordinal))
        {
            string oldName = Path.GetFileNameWithoutExtension(original), newName = Path.GetFileNameWithoutExtension(path);
            string text = Encoding.UTF8.GetString(bytes).Replace("  m_Name: " + newName + "\r\n", "  m_Name: " + oldName + "\r\n")
                .Replace("  m_Name: " + newName + "\n", "  m_Name: " + oldName + "\n");
            bytes = Encoding.UTF8.GetBytes(text);
        }
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
}
