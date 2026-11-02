using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Edits only food-stall scale and its nearby crate; preserves hand-edited map layouts.</summary>
public static class FoodStallMapOptimizer
{
    const string TexturePath = ChapterOneMapGenerator.Art + "/Textures/food_stall.png";
    const string ReportPath = ChapterOneMapGenerator.Preview + "/food-stall-optimization.json";
    [MenuItem("Tools/Maps/Chapter One/Optimize Food Stall (128px, Compact)")]
    static void Menu() { Debug.Log(Apply()); }

    [Serializable] public class MapChange
    {
        public string path, guidBefore, guidAfter, terrainHashBefore, terrainHashAfter;
        public int stalls, crates;
        public bool valid;
    }
    [Serializable] public class Report
    {
        public bool success, sourcePngPreserved, noNewTexture, scenePreserved;
        public int importedWidthBefore, importedWidthAfter, importedHeightBefore, importedHeightAfter, totalStalls;
        public long textureMemoryBefore, textureMemoryAfter;
        public float targetWorldWidth = ChapterOneMapGenerator.FoodStallWidth;
        public string sourcePngHashBefore, sourcePngHashAfter, backup, note;
        public MapChange[] maps;
    }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (sprite == null || importer == null) throw new InvalidOperationException("Existing food-stall texture is missing.");
        var paths = Enumerable.Range(1, 8).Select(ChapterOneMapGenerator.MapPrefabPath).ToList();
        // Keep the source map samples consistent with the shared prop as well.
        paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { ChapterOneMapGenerator.Art + "/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)
                .GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sprite == sprite)));
        paths = paths.Distinct().ToList();
        foreach (string path in paths)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing map: " + path);
        string backup = "Library/MapBackups/FoodStall-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        foreach (string path in paths.Concat(new[] { TexturePath + ".meta" }))
        {
            string dest = backup + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(path, dest, false);
        }
        var report = new Report {
            backup = backup, importedWidthBefore = texture.width, importedHeightBefore = texture.height,
            textureMemoryBefore = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
            sourcePngHashBefore = FileHash(TexturePath), noNewTexture = true,
            note = "Only prefab prop transforms and food_stall import resolution change. Original PNG and all terrain stay intact. Memory is Editor texture memory, not measured WeChat build bytes."
        };
        var mapResults = new List<MapChange>();
        var settings = importer.GetDefaultPlatformTextureSettings();
        settings.maxTextureSize = 128;
        importer.SetPlatformTextureSettings(settings);
        importer.maxTextureSize = 128;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var platformSettings = importer.GetPlatformTextureSettings(platform);
            if (!platformSettings.overridden) continue;
            platformSettings.maxTextureSize = Math.Min(platformSettings.maxTextureSize, 128);
            importer.SetPlatformTextureSettings(platformSettings);
        }
        importer.SaveAndReimport();
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var change = new MapChange { path = path, guidBefore = AssetDatabase.AssetPathToGUID(path), terrainHashBefore = TerrainHash(root) };
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var stall in renderers.Where(r => r.sprite == sprite))
                {
                    float scale = ChapterOneMapGenerator.FoodStallWidth / (sprite.bounds.size.x * Mathf.Abs(stall.transform.parent.lossyScale.x));
                    stall.transform.localScale = Vector3.one * scale;
                    change.stalls++;
                    var crate = renderers.Where(r => r.sprite != null && r.sprite.name == "crate" && r.transform.parent == stall.transform.parent &&
                        Mathf.Abs(r.transform.localPosition.y - stall.transform.localPosition.y) < .35f &&
                        Mathf.Abs(r.transform.localPosition.x - stall.transform.localPosition.x) < 2.6f)
                        .OrderBy(r => (r.transform.localPosition - stall.transform.localPosition).sqrMagnitude).FirstOrDefault();
                    if (crate == null) continue;
                    var pos = crate.transform.localPosition;
                    pos.x = stall.transform.localPosition.x + (pos.x < stall.transform.localPosition.x ? -1.4f : 1.4f);
                    crate.transform.localPosition = pos;
                    crate.transform.localScale = Vector3.one * .9f / (crate.sprite.bounds.size.x * Mathf.Abs(crate.transform.parent.lossyScale.x));
                    change.crates++;
                }
                change.terrainHashAfter = TerrainHash(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Save failed: " + path);
                change.guidAfter = AssetDatabase.AssetPathToGUID(path);
                change.valid = change.stalls > 0 && change.guidBefore == change.guidAfter && change.terrainHashBefore == change.terrainHashAfter;
                mapResults.Add(change);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        report.importedWidthAfter = texture.width; report.importedHeightAfter = texture.height;
        report.textureMemoryAfter = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        report.sourcePngHashAfter = FileHash(TexturePath);
        report.sourcePngPreserved = report.sourcePngHashAfter == report.sourcePngHashBefore;
        report.scenePreserved = scene == UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene() && scene.isDirty == wasDirty;
        report.maps = mapResults.ToArray(); report.totalStalls = mapResults.Sum(r => r.stalls);
        report.success = report.sourcePngPreserved && report.scenePreserved && texture.width <= 128 && texture.height <= 128 && mapResults.All(r => r.valid);
        ChapterOneMapGenerator.RefreshExistingPreviews();
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        if (!report.success) throw new InvalidOperationException("Food-stall validation failed. See " + ReportPath);
        return JsonUtility.ToJson(report, true);
    }

    public static string TerrainHash(GameObject root)
    {
        var value = new StringBuilder();
        foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
        {
            value.Append(map.name).Append(map.cellBounds).Append(map.transform.localPosition).Append(map.transform.localScale);
            foreach (var p in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(p);
                if (tile != null) value.Append(p).Append(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tile)))
                    .Append(map.GetColor(p)).Append(map.GetTransformMatrix(p));
            }
        }
        return Hash(Encoding.UTF8.GetBytes(value.ToString()));
    }
    static string FileHash(string path) { return Hash(File.ReadAllBytes(path)); }
    static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
