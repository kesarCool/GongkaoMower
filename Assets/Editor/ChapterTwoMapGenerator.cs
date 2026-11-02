using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Common = ChapterOneMapGenerator;

/// <summary>Eight connected stone-and-bamboo chapter maps, using the existing style-two art set.</summary>
public static class ChapterTwoMapGenerator
{
    public const string Output = "Assets/Resources/Map";
    public const string Preview = "ArtOutput/World/BambooAcademyLibrary";
    const int Size = 30;
    const string Family = "stone_on_grass";
    const string CornerSource = "美术资源/地图设计/明快卡通_战斗地图_v1/01_试铺资源/32像素_兼容/地面散图/stone_on_grass";

    internal class Plan
    {
        public int stage;
        public string name, label;
        public readonly bool[,] stone = new bool[Size, Size];
        public readonly List<Common.Prop> props = new List<Common.Prop>();
        public Plan Rect(int x, int y, int w, int h, bool fill = true)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++)
                if (i >= 0 && j >= 0 && i < Size && j < Size) stone[i, j] = fill;
            return this;
        }
        public string[] Rows()
        {
            return Enumerable.Range(0, Size).Select(y => new string(Enumerable.Range(0, Size)
                .Select(x => stone[x, Size - 1 - y] ? '#' : '.').ToArray())).ToArray();
        }
        public Plan P(string sprite, float x, float y, float width, bool flip = false)
        {
            props.Add(new Common.Prop { name = sprite + "_" + props.Count.ToString("00"), sprite = sprite, x = x, y = y, width = width, flip = flip });
            return this;
        }
        public Plan Bamboo(float x, float y, float width = 2.4f, bool flip = false) { return P("bamboo", x, y, width, flip); }
        public Plan Lamp(float x, float y) { return P("stone_lantern", x, y, 1); }
        public Plan Bench(float x, float y, bool flip = false) { return P("stone_bench", x, y, 1.8f, flip); }
        public Plan Shrub(float x, float y) { return P("shrub", x, y, 1.25f); }
        public Plan Rock(float x, float y) { return P("rocks", x, y, 1.3f); }
    }
    [Serializable] public class MapResult
    {
        public int stage, groundCells, stoneCells, props;
        public string name, label, prefab, resourcesPath, guid, terrainHash, prefabHash;
        public bool roadConnected, referencesValid, mapLayersValid, spawnClear, visiblePropsInsideMap, perimeterValid, resourcesLoadable;
        public long prefabSourceBytes;
    }
    [Serializable] public class Report
    {
        public string version = "1.0", style = "bamboo_academy", sourceArt = Common.Art, outputFolder = Output, previewFolder = Preview;
        public bool success, priorMapsUnchanged, sceneUnchanged, allLayoutsUnique;
        public int mapsCreated, protectedPrefabCount, cornerTexturesAdded, sharedTextures;
        public long cornerPngSourceBytes, prefabSourceBytes;
        public string[] supplementalTextures;
        public MapResult[] maps;
        public string note = "All maps share existing style-two art; four 32px inner corners are imported from the same original art delivery. Source bytes are not measured WeChat build bytes. Chapter tables and gameplay rules are unchanged.";
    }

    [MenuItem("Tools/Maps/Map Library/Generate Bamboo Academy")]
    public static void Menu() { Debug.Log(Generate()); }
    [MenuItem("Tools/Maps/Map Library/Generate Bamboo Academy", true)]
    static bool CanGenerate() { return !EditorApplication.isPlayingOrWillChangePlaymode; }
    public static string MapPath(int stage) { return Output + "/TileMap2_" + stage + ".prefab"; }
    public static string BambooMapPath(int stage) { return MapResourcePaths.BambooMap(stage); }

    public static string Generate()
    {
        if (!CanGenerate()) throw new InvalidOperationException("Exit Play mode before generating maps.");
        var active = SceneManager.GetActiveScene(); bool wasDirty = active.isDirty; int sceneCount = SceneManager.sceneCount;
        var targets = Enumerable.Range(1, 8).Select(BambooMapPath).ToArray();
        var protectedPaths = Directory.GetFiles(Output, "*.prefab").Concat(Directory.GetFiles(Common.Art + "/Prefabs", "*.prefab"))
            .Select(p => p.Replace('\\', '/')).Except(targets).ToDictionary(p => p, FileHash);
        var corners = EnsureInnerCorners();
        var tiles = AssetDatabase.FindAssets("t:Tile", new[] { Common.Art + "/Tiles" }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Tile>).Where(t => t != null).ToDictionary(t => t.name);
        string[] allowedProps = { "bamboo", "stone_lantern", "stone_bench", "shrub", "rocks", "sign" };
        var sprites = allowedProps.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<Sprite>(Common.Art + "/Textures/" + n + ".png"));
        var plans = Plans();
        foreach (var plan in plans)
        {
            if (!Common.Connected(plan.stone)) throw new InvalidOperationException("Disconnected stone path: " + plan.name);
            foreach (var n in CartoonTilePlan.Build(plan.Rows(), Family, "grass").Distinct())
                if (!tiles.ContainsKey(n)) throw new InvalidOperationException("Missing Tile: " + n);
            foreach (var prop in plan.props)
                if (sprites[prop.sprite] == null) throw new InvalidOperationException("Missing Sprite: " + prop.sprite);
        }
        Directory.CreateDirectory(Preview + "/blueprints");
        if (targets.Any(File.Exists))
        {
            string backup = "Library/MapBackups/BambooLibrary-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            foreach (var p in targets.Where(File.Exists))
            {
                File.Copy(p, backup + "/" + Path.GetFileName(p));
                File.Copy(p + ".meta", backup + "/" + Path.GetFileName(p) + ".meta");
            }
        }
        var results = new List<MapResult>();
        try
        {
            foreach (var plan in plans)
            {
                EditorUtility.DisplayProgressBar("Chapter Two Maps", plan.name, (plan.stage - 1) / 8f);
                string path = Common.BuildThemedPrefab(BambooMapPath(plan.stage), plan.Rows(), Family, "grass", plan.props, tiles, sprites);
                var result = Inspect(plan, path);
                results.Add(result);
                File.WriteAllText(Preview + "/blueprints/ZlMap" + plan.stage + ".txt", string.Join("\n", plan.Rows()) + "\n", new UTF8Encoding(false));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Common.Render(prefab, Preview + "/ZlMap" + plan.stage + ".png", false);
                Common.Render(prefab, Preview + "/ZlMap" + plan.stage + "-phone.png", true);
            }
            AssetDatabase.SaveAssets();
            var textures = targets.SelectMany(p => AssetDatabase.GetDependencies(p, true)).Where(p => p.EndsWith(".png")).Distinct().ToArray();
            var report = new Report {
                mapsCreated = results.Count, maps = results.ToArray(), protectedPrefabCount = protectedPaths.Count,
                priorMapsUnchanged = protectedPaths.All(p => p.Value == FileHash(p.Key)),
                allLayoutsUnique = results.Select(r => r.terrainHash).Distinct().Count() == 8,
                sceneUnchanged = active == SceneManager.GetActiveScene() && active.isDirty == wasDirty && sceneCount == SceneManager.sceneCount,
                cornerTexturesAdded = corners.Length, supplementalTextures = corners,
                cornerPngSourceBytes = corners.Sum(p => new FileInfo(p).Length), sharedTextures = textures.Length,
                prefabSourceBytes = results.Sum(r => r.prefabSourceBytes)
            };
            report.success = report.priorMapsUnchanged && report.allLayoutsUnique && report.sceneUnchanged && results.All(Valid);
            File.WriteAllText(Preview + "/build-report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.success) throw new InvalidOperationException("Map validation failed: " + Preview + "/build-report.json");
            return JsonUtility.ToJson(report, true);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static string[] EnsureInnerCorners()
    {
        var paths = new List<string>();
        foreach (int mask in new[] { 127, 223, 247, 253 })
        {
            string name = Family + "_m" + mask.ToString("000"), imagePath = Common.Art + "/Textures/" + name + ".png";
            paths.Add(imagePath);
            if (!File.Exists(imagePath)) File.Copy(CornerSource + "/" + name + ".png", imagePath);
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 32)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.mipmapEnabled = false; importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = 0;
                importer.SetTextureSettings(settings); importer.maxTextureSize = 32; importer.SaveAndReimport();
            }
            string tilePath = Common.Art + "/Tiles/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null) continue;
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.name = name;
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath); tile.color = Color.white; tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        return paths.ToArray();
    }

    internal static Plan[] Plans()
    {
        var gate = new Plan { stage = 1, name = "bamboo_gate", label = "竹径迎门" };
        gate.Rect(11, 0, 8, 30).Rect(7, 6, 16, 18).Rect(9, 3, 12, 24);
        gate.Bamboo(4, 4).Bamboo(26, 4, 2.4f, true).Bamboo(4, 23.5f, 2.7f).Bamboo(26, 23.5f, 2.7f, true)
            .Bamboo(7.2f, 25.8f, 2.1f).Bamboo(22.8f, 25.8f, 2.1f, true)
            .Lamp(10.5f, 24.2f).Lamp(19.5f, 24.2f).Lamp(10.5f, 4.1f).Lamp(19.5f, 4.1f)
            .Bench(5, 14).Bench(25, 14, true).Shrub(4.5f, 10.3f).Shrub(25.5f, 18.6f)
            .Rock(7, 3.2f).Rock(23, 3.2f).P("sign", 8, 21.2f, 1.1f);

        var stairs = new Plan { stage = 2, name = "diagonal_steps", label = "斜阶问学" };
        stairs.Rect(1, 1, 10, 8).Rect(7, 6, 10, 9).Rect(12, 12, 10, 9).Rect(19, 19, 10, 9);
        stairs.Bamboo(3, 10.6f, 2.5f).Bamboo(6, 16.2f, 2.5f, true).Bamboo(10, 22.3f)
            .Bamboo(16, 25.7f, 2.4f, true).Bamboo(26, 3).Bamboo(24.1f, 9.3f, 2.5f, true)
            .Lamp(3.3f, 3).Lamp(9, 4).Lamp(20.4f, 24.3f).Lamp(26.6f, 24.3f)
            .Bench(4.3f, 7).Bench(25.2f, 26.5f, true).Shrub(3, 21.8f).Shrub(27.2f, 15.3f)
            .Rock(8.6f, 25.8f).Rock(20.4f, 3.8f).P("sign", 10.7f, 10.3f, 1.1f).P("sign", 23.8f, 18, 1.1f);

        var courtyard = new Plan { stage = 3, name = "open_courtyard", label = "竹影回庭" };
        courtyard.Rect(3, 3, 24, 7).Rect(3, 8, 7, 19).Rect(20, 8, 7, 19).Rect(10, 3, 10, 10).Rect(12, 0, 6, 4);
        courtyard.Bamboo(11.3f, 20, 2.3f).Bamboo(18.7f, 20, 2.3f, true)
            .Bamboo(11.7f, 24.4f, 2.3f).Bamboo(18.3f, 24.4f, 2.3f, true)
            .Bamboo(2, 11, 2).Bamboo(28, 11, 2, true).Bamboo(2, 23, 2).Bamboo(28, 23, 2, true)
            .Lamp(6.5f, 24.7f).Lamp(23.5f, 24.7f).Lamp(10.5f, 6).Lamp(19.5f, 6)
            .Bench(11.4f, 18.4f).Bench(18.6f, 18.4f, true).Shrub(11, 12).Shrub(19, 12)
            .Rock(5.5f, 1).Rock(24.5f, 1).P("sign", 15, 27.5f, 1.1f);

        var fork = new Plan { stage = 4, name = "forked_paths", label = "分径寻章" };
        fork.Rect(12, 0, 6, 16).Rect(8, 10, 14, 10).Rect(3, 17, 10, 9).Rect(17, 17, 10, 9)
            .Rect(0, 22, 6, 6).Rect(24, 22, 6, 6).Rect(13, 20, 4, 10, false);
        fork.Bamboo(4, 3).Bamboo(26, 3, 2.4f, true).Bamboo(5.5f, 12.4f).Bamboo(24.5f, 12.4f, 2.4f, true)
            .Bamboo(14.6f, 24.8f, 2.1f).Bamboo(15.5f, 20.7f, 2.1f, true)
            .Lamp(8, 23.5f).Lamp(22, 23.5f).Lamp(10.5f, 7).Lamp(19.5f, 7)
            .Bench(6, 19).Bench(24, 19, true).Shrub(4.5f, 7.5f).Shrub(25.5f, 7.5f)
            .Rock(10, 27).Rock(20, 27).P("sign", 15, 9.5f, 1.1f);

        var spiral = new Plan { stage = 5, name = "scroll_walk", label = "九曲书径" };
        spiral.Rect(3, 23, 24, 5).Rect(3, 3, 5, 25).Rect(3, 3, 24, 5)
            .Rect(22, 7, 5, 13).Rect(12, 15, 15, 5).Rect(12, 10, 5, 10);
        spiral.Bamboo(10, 21, 2.1f).Bamboo(17.7f, 10, 2.2f, true).Bamboo(20, 20.5f, 2.1f)
            .Bamboo(2, 12, 2).Bamboo(28, 12, 2, true).Bamboo(5, 1, 2.2f).Bamboo(25, 1, 2.2f, true)
            .Lamp(5.3f, 24.8f).Lamp(24.8f, 24.8f).Lamp(5.3f, 4.4f).Lamp(24.8f, 4.4f)
            .Bench(15, 26).Bench(10.1f, 8.7f, true).Rock(9.6f, 11).Rock(18.5f, 22)
            .Shrub(28.1f, 21.7f).Shrub(1.7f, 22.6f).P("sign", 20, 5, 1.1f);

        var twins = new Plan { stage = 6, name = "paired_gardens", label = "双庭对语" };
        twins.Rect(2, 7, 11, 16).Rect(4, 4, 7, 22).Rect(17, 7, 11, 16).Rect(19, 4, 7, 22).Rect(11, 12, 8, 6);
        twins.Bamboo(7.5f, 26.7f, 2.1f).Bamboo(22.5f, 26.7f, 2.1f, true)
            .Bamboo(7.5f, 1.2f).Bamboo(22.5f, 1.2f, 2.4f, true)
            .Bamboo(13.7f, 20.5f, 2.2f).Bamboo(16.3f, 6.7f, 2.2f, true)
            .Lamp(5.2f, 22).Lamp(24.8f, 22).Lamp(5.2f, 7.5f).Lamp(24.8f, 7.5f)
            .Bench(3.8f, 14).Bench(26.2f, 14, true).Shrub(13.6f, 24.4f).Shrub(16.4f, 3.2f)
            .Rock(1.8f, 3).Rock(28.2f, 24.8f).P("sign", 11.8f, 10, 1.1f);

        var terraces = new Plan { stage = 7, name = "rising_terraces", label = "书山长阶" };
        terraces.Rect(3, 21, 24, 7).Rect(7, 11, 16, 7).Rect(11, 1, 8, 7).Rect(12, 6, 6, 7).Rect(12, 16, 6, 7);
        terraces.Bamboo(3.8f, 18, 2.4f).Bamboo(26.2f, 18, 2.4f, true)
            .Bamboo(7.5f, 7, 2.4f).Bamboo(22.5f, 7, 2.4f, true)
            .Bamboo(5, 25.5f, 2.4f).Bamboo(25, 25.5f, 2.4f, true)
            .Lamp(10.5f, 23.4f).Lamp(19.5f, 23.4f).Lamp(10, 11.8f).Lamp(20, 11.8f)
            .Bench(10, 26).Bench(20, 26, true).Bench(15, 3)
            .Shrub(4, 11.2f).Shrub(26, 11.2f).Rock(9.1f, 1.6f).Rock(20.9f, 1.6f).P("sign", 22.7f, 20, 1.1f);

        var forum = new Plan { stage = 8, name = "scholar_forum", label = "文昌论道" };
        forum.Rect(3, 8, 24, 14).Rect(6, 5, 18, 20).Rect(9, 2, 12, 26).Rect(12, 0, 6, 30)
            .Rect(0, 12, 4, 6).Rect(26, 12, 4, 6);
        forum.Bamboo(3.4f, 3.8f, 2.6f).Bamboo(26.6f, 3.8f, 2.6f, true)
            .Bamboo(3.4f, 23.7f, 2.6f).Bamboo(26.6f, 23.7f, 2.6f, true)
            .Bamboo(7.8f, 26.8f, 2.1f).Bamboo(22.2f, 26.8f, 2.1f, true)
            .Lamp(10.6f, 24.4f).Lamp(19.4f, 24.4f).Lamp(10.6f, 4.2f).Lamp(19.4f, 4.2f)
            .Lamp(5, 9).Lamp(25, 9).Lamp(5, 19.3f).Lamp(25, 19.3f)
            .Bench(7.6f, 13).Bench(22.4f, 13, true).Shrub(7.8f, 1.2f).Shrub(22.2f, 1.2f)
            .Rock(2.3f, 8.3f).Rock(27.7f, 8.3f).P("sign", 15, 26.6f, 1.1f);
        return new[] { gate, stairs, courtyard, fork, spiral, twins, terraces, forum };
    }

    static MapResult Inspect(Plan plan, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); var ground = prefab.GetComponentInChildren<Tilemap>();
        var result = new MapResult {
            stage = plan.stage, name = plan.name, label = plan.label, prefab = path, resourcesPath = "Map/ZlMap" + plan.stage,
            guid = AssetDatabase.AssetPathToGUID(path), stoneCells = plan.stone.Cast<bool>().Count(v => v), props = plan.props.Count,
            groundCells = ground.GetTilesBlock(ground.cellBounds).Count(t => t != null),
            terrainHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", plan.Rows()))), prefabHash = FileHash(path), prefabSourceBytes = new FileInfo(path).Length,
            roadConnected = Common.Connected(plan.stone), referencesValid = Common.ReferencesValid(prefab),
            mapLayersValid = prefab.GetComponentsInChildren<Renderer>(true).All(r => r.gameObject.layer == LayerMask.NameToLayer("Map")),
            spawnClear = true, visiblePropsInsideMap = true, resourcesLoadable = Resources.Load<GameObject>("Map/ZlMap" + plan.stage) == prefab
        };
        var spawn = new Bounds(new Vector3(15, 15, 0), new Vector3(5, 5, 10));
        foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Bounds b = Common.VisibleBounds(sr);
            if (b.Intersects(spawn)) result.spawnClear = false;
            if (b.min.x < 0 || b.min.y < 0 || b.max.x > Size || b.max.y > Size) result.visiblePropsInsideMap = false;
        }
        var terrain = CartoonTilePlan.Build(plan.Rows(), Family, "grass");
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            if (ground.GetTile(new Vector3Int(x, Size - 1 - y, 0)).name != terrain[y * Size + x])
                throw new InvalidOperationException("Serialized terrain mismatch: " + path);
        result.perimeterValid = CheckPerimeter(prefab);
        if (!Valid(result)) throw new InvalidOperationException(JsonUtility.ToJson(result));
        return result;
    }
    static bool CheckPerimeter(GameObject prefab)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var ground = instance.GetComponentInChildren<Tilemap>();
            typeof(BattleMapLoader).GetMethod("BuildPerimeterWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { instance.transform, ground });
            var wall = instance.transform.Find("Wall");
            return ground.cellBounds == new BoundsInt(0, 0, 0, Size, Size, 1) && wall != null && wall.childCount == 4 &&
                wall.GetComponentsInChildren<BoxCollider2D>().Length == 4 && wall.GetComponentsInChildren<WallMarker>().Length == 4;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static bool Valid(MapResult r)
    {
        return r.groundCells == Size * Size && r.roadConnected && r.referencesValid && r.mapLayersValid && r.spawnClear &&
            r.visiblePropsInsideMap && r.perimeterValid && r.resourcesLoadable;
    }
    static string FileHash(string path) { return Hash(File.ReadAllBytes(path)); }
    static string Hash(byte[] value)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
    }
}
