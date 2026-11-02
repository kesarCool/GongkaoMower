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
using Object = UnityEngine.Object;

/// <summary>Chapter-one variations. Reuses the existing style-one Tile/Sprite assets; writes no new art.</summary>
public static class ChapterOneMapGenerator
{
    public const string Art = MapResourcePaths.SharedCartoon;
    public const string Output = "Assets/Resources/Map";
    public const string Preview = "ArtOutput/World/ChapterOneMaps";
    const string FirstMap = Output + "/TileMap1_1.prefab";
    const int Size = 30;
    public const float FoodStallWidth = 2.2f;
    static readonly Dictionary<Sprite, Rect> VisibleRects = new Dictionary<Sprite, Rect>();

    [Serializable] public class Prop
    {
        public string name, sprite;
        public float x, y, width;
        public bool flip;
    }
    class Plan
    {
        public int stage;
        public string name;
        public readonly bool[,] dirt = new bool[Size, Size];
        public readonly List<Prop> props = new List<Prop>();
        public string[] Rows()
        {
            var rows = new string[Size];
            for (int y = 0; y < Size; y++)
            {
                var row = new char[Size];
                for (int x = 0; x < Size; x++) row[x] = dirt[x, Size - 1 - y] ? '#' : '.';
                rows[y] = new string(row);
            }
            return rows;
        }
        public Plan Rect(int x, int y, int width, int height, bool fill = true)
        {
            for (int j = y; j < y + height; j++) for (int i = x; i < x + width; i++)
                if (i >= 0 && j >= 0 && i < Size && j < Size) dirt[i, j] = fill;
            return this;
        }
        public Plan P(string sprite, float x, float y, float width, bool flip = false)
        {
            props.Add(new Prop { name = sprite + "_" + props.Count.ToString("00"), sprite = sprite, x = x, y = y, width = width, flip = flip });
            return this;
        }
        public Plan Tree(float x, float y, float width = 3.15f) { return P("tree_round", x, y, width); }
        public Plan Stall(float x, float y, bool flip = false)
        {
            P("food_stall", x, y, FoodStallWidth, flip);
            P("crate", x + (flip ? -1.4f : 1.4f), y + .05f, .9f);
            return this;
        }
        public Plan Wheat(float x, float y, int columns = 3, int rows = 2)
        {
            for (int j = rows - 1; j >= 0; j--) for (int i = 0; i < columns; i++)
                P("wheat", x + i * 1.15f + (j % 2) * .15f, y + j * .85f, .95f);
            return this;
        }
    }
    [Serializable] public class MapResult
    {
        public int stage, groundCells, dirtCells, propCount, terrainKinds;
        public string name, prefab, resourcesPath, blueprint, render, phoneRender, terrainHash, prefabHash;
        public long prefabSourceBytes;
        public bool connectedDirt, resourcesLoadable, referencesValid, mapLayersValid, boundsValid, spawnClear;
    }
    [Serializable] public class Report
    {
        public string version = "1.0", sourceArt = Art, retainedFirstMap = FirstMap;
        public string firstMapHashBefore, firstMapHashAfter;
        public bool success, originalUnchanged, noAdditionalTextures, sceneUnchanged;
        public int newPrefabs, additionalTextureCount, sharedTextureCount;
        public long newPrefabSourceBytes;
        public string packagingNote = "Prefab source bytes are not measured WeChat build bytes. All new maps share the first map's existing textures and Tile assets. Level table is unchanged.";
        public string[] additionalTextures;
        public MapResult[] maps;
    }

    [MenuItem("Tools/Maps/Chapter One/Generate Style 1 Maps (1-2 to 1-8)")]
    public static void GenerateMenu() { Debug.Log("[ChapterOneMaps] " + Generate()); }
    [MenuItem("Tools/Maps/Chapter One/Generate Style 1 Maps (1-2 to 1-8)", true)]
    static bool CanGenerate() { return !EditorApplication.isPlayingOrWillChangePlaymode; }

    // Respect maps renamed in the Editor; never recreate a second set under the old names.
    public static string MapPrefabPath(int stage)
    {
        if (stage == 1) return FirstMap;
        string renamed = Output + "/TileMap1_" + stage + ".prefab";
        return File.Exists(renamed) ? renamed : Output + "/TileMap1-" + stage + ".prefab";
    }

    public static void RefreshExistingPreviews()
    {
        if (!CanGenerate()) throw new InvalidOperationException("Exit Play mode before rendering maps.");
        Directory.CreateDirectory(Preview);
        for (int stage = 1; stage <= 8; stage++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath(stage));
            if (prefab == null) throw new InvalidOperationException("Missing map: " + MapPrefabPath(stage));
            Render(prefab, Preview + "/TileMap1-" + stage + ".png", false);
            Render(prefab, Preview + "/TileMap1-" + stage + "-phone.png", true);
        }
    }

    public static string Generate()
    {
        if (!CanGenerate()) throw new InvalidOperationException("Exit Play mode before generating maps.");
        var scene = SceneManager.GetActiveScene();
        VisibleRects.Clear();
        string originalHash = HashFile(FirstMap);
        var first = AssetDatabase.LoadAssetAtPath<GameObject>(FirstMap);
        if (first == null) throw new InvalidOperationException("Existing first map is missing.");
        var baselineTextures = new HashSet<string>(Textures(FirstMap), StringComparer.Ordinal);
        var tiles = AssetDatabase.FindAssets("t:Tile", new[] { Art + "/Tiles" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Tile>)
            .Where(t => t != null).ToDictionary(t => t.name, StringComparer.Ordinal);
        var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (string path in baselineTextures)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites.Add(Path.GetFileNameWithoutExtension(path), sprite);
        }
        var plans = Plans();
        // Preflight the entire batch before writing the first prefab. No fallback to mismatching edge art.
        foreach (var plan in plans)
        {
            foreach (string name in CartoonTilePlan.Build(plan.Rows(), "dirt_on_grass", "grass").Distinct())
                if (!tiles.ContainsKey(name)) throw new InvalidOperationException("Missing existing Tile: " + name + " in stage " + plan.stage);
            foreach (var prop in plan.props)
                if (!sprites.ContainsKey(prop.sprite)) throw new InvalidOperationException("Missing existing Sprite: " + prop.sprite);
            if (!Connected(plan.dirt)) throw new InvalidOperationException("Disconnected road plan: " + plan.name);
        }
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Preview + "/blueprints");
        var results = new List<MapResult>();
        var allTextures = new HashSet<string>(baselineTextures, StringComparer.Ordinal);
        try
        {
            foreach (var plan in plans)
            {
                EditorUtility.DisplayProgressBar("Chapter One Maps", plan.name, (plan.stage - 2) / 7f);
                string path = Build(plan, tiles, sprites);
                string blueprint = Preview + "/blueprints/TileMap1-" + plan.stage + ".txt";
                File.WriteAllText(blueprint, string.Join("\n", plan.Rows()) + "\n", new UTF8Encoding(false));
                var result = Inspect(plan, path);
                result.blueprint = blueprint;
                results.Add(result);
                foreach (string texture in Textures(path)) allTextures.Add(texture);
            }
            AssetDatabase.SaveAssets();
            var terrainHashes = new HashSet<string>(results.Select(r => r.terrainHash));
            if (terrainHashes.Count != 7) throw new InvalidOperationException("Duplicate terrain layout.");
            Render(first, Preview + "/TileMap1-1.png", false);
            Render(first, Preview + "/TileMap1-1-phone.png", true);
            foreach (var result in results)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.prefab);
                result.render = Preview + "/TileMap1-" + result.stage + ".png";
                result.phoneRender = Preview + "/TileMap1-" + result.stage + "-phone.png";
                Render(prefab, result.render, false);
                Render(prefab, result.phoneRender, true);
            }
            string[] extra = allTextures.Except(baselineTextures).OrderBy(p => p).ToArray();
            var report = new Report {
                firstMapHashBefore = originalHash, firstMapHashAfter = HashFile(FirstMap),
                originalUnchanged = originalHash == HashFile(FirstMap),
                noAdditionalTextures = extra.Length == 0, additionalTextureCount = extra.Length,
                additionalTextures = extra, sharedTextureCount = allTextures.Count,
                newPrefabs = results.Count, newPrefabSourceBytes = results.Sum(r => r.prefabSourceBytes),
                maps = results.ToArray(), sceneUnchanged = SceneManager.GetActiveScene() == scene && !scene.isDirty
            };
            report.success = report.originalUnchanged && report.noAdditionalTextures && results.All(Valid);
            File.WriteAllText(Preview + "/build-report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.success) throw new InvalidOperationException("Chapter One map validation failed; see " + Preview + "/build-report.json");
            return JsonUtility.ToJson(report, true);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    static Plan[] Plans()
    {
        var cross = new Plan { stage = 2, name = "market_cross" };
        cross.Rect(12, 0, 6, 30).Rect(0, 12, 30, 6).Rect(9, 9, 12, 12);
        cross.Stall(5.5f, 23).Stall(24.5f, 4.2f, true)
            .Tree(3.1f, 3.1f).Tree(26.4f, 22.5f).Tree(3.2f, 25.7f, 2.8f).Tree(26.5f, 8.5f, 2.7f)
            .Wheat(21.5f, 24.8f).Wheat(4, 5.6f)
            .P("fence", 6, 2.2f, 3.2f).P("fence", 23, 28, 3.2f)
            .P("barrel", 3.1f, 22.8f, 1.25f).P("steamer", 26.8f, 4.5f, 1)
            .P("sign", 10.5f, 20.5f, 1.35f).P("sign", 19.5f, 6.8f, 1.35f)
            .P("shrub", 7.6f, 10.2f, 1.6f).P("rocks", 23.5f, 19.4f, 1.5f);

        var twin = new Plan { stage = 3, name = "twin_courts" };
        twin.Rect(3, 18, 15, 9).Rect(6, 16, 10, 13).Rect(12, 3, 15, 9)
            .Rect(15, 1, 9, 13).Rect(12, 10, 6, 12).Rect(6, 27, 6, 3).Rect(20, 0, 6, 5);
        twin.Stall(6, 25.2f).Stall(23.6f, 4, true)
            .Tree(3.2f, 5).Tree(6.3f, 9.8f).Tree(26.3f, 22.5f).Tree(23.1f, 26.7f, 2.8f)
            .Tree(3.1f, 14, 2.8f).Tree(26.8f, 15, 2.8f)
            .Wheat(20, 23.2f, 3, 3).Wheat(4.5f, 2.5f, 3, 2)
            .P("fence", 22, 27.8f, 3.4f).P("fence", 6, 1.2f, 3.4f)
            .P("sign", 9, 15, 1.4f).P("sign", 21, 13.6f, 1.4f)
            .P("barrel", 3.7f, 25.5f, 1.2f).P("steamer", 26, 4, 1)
            .P("shrub", 9.5f, 7.2f, 1.7f).P("rocks", 20.5f, 18, 1.5f);

        var loop = new Plan { stage = 4, name = "orchard_loop" };
        loop.Rect(3, 3, 24, 24).Rect(10, 10, 10, 10, false)
            .Rect(3, 3, 2, 2, false).Rect(25, 3, 2, 2, false)
            .Rect(3, 25, 2, 2, false).Rect(25, 25, 2, 2, false)
            .Rect(0, 12, 4, 6).Rect(26, 12, 4, 6).Rect(12, 0, 6, 4).Rect(12, 26, 6, 4);
        loop.Tree(11, 10.2f, 2.7f).Tree(19, 17.9f, 2.7f)
            .P("shrub", 11.2f, 18, 1.5f).P("shrub", 18.8f, 10.4f, 1.5f)
            .P("rocks", 11.4f, 12, 1.1f).P("rocks", 19.2f, 18.8f, 1.1f)
            .Stall(5.8f, 25.2f).Stall(24.5f, 3.1f, true)
            .Tree(2.2f, 4.3f, 2.8f).Tree(27.5f, 24.8f, 2.8f)
            .Tree(5.9f, 1.5f, 2.7f).Tree(23.4f, 27.6f, 2.3f)
            .Wheat(20.5f, 1.2f, 3, 1).Wheat(5, 28, 3, 1)
            .P("barrel", 3.5f, 25.4f, 1.2f).P("steamer", 27, 3, 1)
            .P("sign", 9, 21, 1.3f).P("sign", 21, 8, 1.3f);

        var bend = new Plan { stage = 5, name = "bending_lane" };
        bend.Rect(3, 3, 27, 6).Rect(21, 7, 6, 11).Rect(3, 12, 24, 6)
            .Rect(3, 15, 6, 10).Rect(0, 21, 26, 6);
        bend.Stall(14, 23.5f).Stall(14, 3.8f, true)
            .Tree(4.4f, 1).Tree(26.5f, 24.4f).Tree(12.2f, 18.5f, 2.7f)
            .Tree(17.2f, 8.9f, 2.7f).Tree(2, 9.3f, 2.5f).Tree(27.4f, 17.7f, 2.5f)
            .Wheat(10.5f, 27.6f, 4, 1).Wheat(12.2f, 10.2f, 4, 1)
            .P("fence", 18.5f, 20, 3.3f).P("fence", 8.5f, 10.5f, 3)
            .P("rocks", 21.8f, 27.7f, 1.4f).P("shrub", 9.8f, 19.5f, 1.45f)
            .P("sign", 25.5f, 10.1f, 1.35f).P("sign", 4.4f, 18.5f, 1.35f)
            .P("barrel", 17, 3.8f, 1.2f).P("steamer", 11, 23.6f, 1);

        var fields = new Plan { stage = 6, name = "harvest_avenues" };
        fields.Rect(4, 0, 7, 30).Rect(19, 0, 7, 30).Rect(9, 11, 12, 8);
        fields.Stall(5.8f, 24.8f).Stall(23.6f, 3.1f, true)
            .Wheat(12.2f, 3, 5, 3).Wheat(12.2f, 22.2f, 5, 3)
            .P("fence", 13.4f, 1.2f, 2.8f).P("fence", 16.2f, 1.2f, 2.8f)
            .P("fence", 13.4f, 26.5f, 2.8f).P("fence", 16.2f, 26.5f, 2.8f)
            .Tree(2, 5.5f, 2.6f).Tree(2, 21, 2.6f).Tree(28, 23.8f, 2.6f).Tree(28, 9.3f, 2.6f)
            .P("barrel", 3.6f, 25.2f, 1.2f).P("steamer", 26, 3.1f, 1)
            .P("sign", 11.8f, 20, 1.3f).P("sign", 18.2f, 8, 1.3f)
            .P("shrub", 2, 13.8f, 1.4f).P("rocks", 28.2f, 16.7f, 1.5f);

        var doubleLoop = new Plan { stage = 7, name = "double_loop" };
        doubleLoop.Rect(5, 1, 20, 14).Rect(3, 15, 24, 14)
            .Rect(9, 6, 12, 5, false).Rect(8, 20, 14, 5, false)
            .Rect(0, 12, 7, 6).Rect(25, 24, 5, 4);
        doubleLoop.Tree(10.5f, 6.2f, 2.7f).Tree(18.7f, 6.2f, 2.7f)
            .Wheat(10, 20.5f, 3, 2).Wheat(17, 20.5f, 3, 2)
            .P("fence", 15, 24.3f, 3.2f)
            .Stall(5.8f, 25.7f).Stall(23.8f, 2.4f, true)
            .Tree(2.3f, 2.4f, 2.8f).Tree(2.3f, 20.1f, 2.6f).Tree(27.5f, 10.3f, 2.8f)
            .P("shrub", 14.9f, 7, 1.5f).P("shrub", 28, 18.2f, 1.5f)
            .P("barrel", 3.4f, 25.8f, 1.2f).P("steamer", 26.2f, 2.5f, 1)
            .P("sign", 9.2f, 13, 1.35f).P("sign", 25, 17.3f, 1.35f)
            .P("rocks", 2.4f, 9, 1.6f).P("rocks", 27.3f, 28, 1.4f);

        var feast = new Plan { stage = 8, name = "feast_square" };
        feast.Rect(1, 9, 28, 12).Rect(4, 5, 22, 20).Rect(8, 2, 14, 26).Rect(12, 0, 6, 30)
            .Rect(7, 8, 4, 4, false).Rect(19, 8, 4, 4, false)
            .Rect(7, 19, 4, 4, false).Rect(19, 19, 4, 4, false);
        feast.Tree(9, 8.3f, 2.6f).Tree(21, 8.3f, 2.6f)
            .P("shrub", 8, 19.4f, 1.7f).P("shrub", 22, 19.4f, 1.7f)
            .Wheat(8, 21, 2, 1).Wheat(20, 21, 2, 1)
            .Stall(6.1f, 24.5f).Stall(23.9f, 24.5f, true)
            .Stall(6.1f, 2.5f).Stall(23.9f, 2.5f, true)
            .Tree(2.2f, 5.5f, 2.8f).Tree(27.8f, 5.5f, 2.8f)
            .Tree(2.2f, 23.4f, 2.8f).Tree(27.8f, 23.4f, 2.8f)
            .P("barrel", 3.5f, 24.5f, 1.2f).P("barrel", 26.5f, 24.5f, 1.2f)
            .P("steamer", 3.5f, 2.5f, 1.1f).P("steamer", 26.5f, 2.5f, 1.1f)
            .P("sign", 12, 26, 1.3f).P("sign", 18, 3, 1.3f)
            .P("rocks", 9.4f, 11, 1.2f).P("rocks", 20.6f, 11, 1.2f);
        return new[] { cross, twin, loop, bend, fields, doubleLoop, feast };
    }

    static string Build(Plan plan, Dictionary<string, Tile> tiles, Dictionary<string, Sprite> sprites)
    {
        return BuildThemedPrefab(MapPrefabPath(plan.stage), plan.Rows(), "dirt_on_grass", "grass", plan.props, tiles, sprites);
    }

    internal static string BuildThemedPrefab(string path, string[] rows, string family, string background,
        IList<Prop> props, IDictionary<string, Tile> tiles, IDictionary<string, Sprite> sprites)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(Grid));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.GetComponent<Grid>().cellSize = new Vector3(1, 1, 0);
            int layer = LayerMask.NameToLayer("Map");
            if (layer < 0) throw new InvalidOperationException("Map layer is missing.");
            root.layer = layer;
            var groundGo = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            groundGo.layer = layer;
            groundGo.transform.SetParent(root.transform, false);
            var ground = groundGo.GetComponent<Tilemap>();
            ground.tileAnchor = new Vector3(.5f, .5f, 0);
            var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var renderer = groundGo.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -100;
            var terrain = CartoonTilePlan.Build(rows, family, background);
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                ground.SetTile(new Vector3Int(x, Size - 1 - y, 0), tiles[terrain[y * Size + x]]);
            ground.CompressBounds();
            var decor = new GameObject("Decoration");
            decor.layer = layer;
            decor.transform.SetParent(root.transform, false);
            foreach (var prop in props)
            {
                var go = new GameObject(prop.name, typeof(SpriteRenderer));
                go.layer = layer;
                go.transform.SetParent(decor.transform, false);
                go.transform.localPosition = new Vector3(prop.x, prop.y, 0);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprites[prop.sprite];
                sr.sharedMaterial = material;
                sr.sortingOrder = -50;
                sr.flipX = prop.flip;
                go.transform.localScale = Vector3.one * prop.width / sr.sprite.bounds.size.x;
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Could not save " + path);
            return path;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static MapResult Inspect(Plan plan, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var ground = prefab.GetComponentInChildren<Tilemap>();
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        var terrain = CartoonTilePlan.Build(plan.Rows(), "dirt_on_grass", "grass");
        var result = new MapResult {
            stage = plan.stage, name = plan.name, prefab = path,
            resourcesPath = "Map/" + Path.GetFileNameWithoutExtension(path), dirtCells = plan.dirt.Cast<bool>().Count(v => v),
            groundCells = ground.GetUsedTilesCount() > 0 ? ground.GetTilesBlock(ground.cellBounds).Count(t => t != null) : 0,
            propCount = plan.props.Count, terrainKinds = terrain.Distinct().Count(),
            terrainHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", plan.Rows()))),
            prefabHash = HashFile(path), prefabSourceBytes = new FileInfo(path).Length,
            connectedDirt = Connected(plan.dirt), resourcesLoadable = Resources.Load<GameObject>("Map/" + Path.GetFileNameWithoutExtension(path)) == prefab,
            referencesValid = ReferencesValid(prefab),
            mapLayersValid = renderers.All(r => r.gameObject.layer == LayerMask.NameToLayer("Map")),
            boundsValid = ground.cellBounds == new BoundsInt(0, 0, 0, Size, Size, 1) &&
                prefab.GetComponentsInChildren<Collider2D>(true).Length == 0,
            spawnClear = true
        };
        var spawn = new Bounds(new Vector3(15, 15, 0), new Vector3(5, 5, 10));
        foreach (var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // FullRect sprites contain transparent margins, especially the fences. Check visible pixels.
            Bounds b = VisibleBounds(sr);
            if (b.min.x < -.01f || b.min.y < -.01f || b.max.x > Size + .01f || b.max.y > Size + .01f)
                throw new InvalidOperationException("Prop is outside map: " + path + "/" + sr.name + " " + b);
            if (b.Intersects(spawn)) result.spawnClear = false;
        }
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            if (ground.GetTile(new Vector3Int(x, Size - 1 - y, 0)).name != terrain[y * Size + x])
                throw new InvalidOperationException("Serialized terrain mismatch in " + path);
        if (!Valid(result)) throw new InvalidOperationException("Map validation failed: " + JsonUtility.ToJson(result));
        return result;
    }

    static bool Valid(MapResult r)
    {
        return r.groundCells == Size * Size && r.connectedDirt && r.resourcesLoadable && r.referencesValid &&
            r.mapLayersValid && r.boundsValid && r.spawnClear;
    }
    internal static Bounds VisibleBounds(SpriteRenderer renderer)
    {
        Sprite sprite = renderer.sprite;
        Rect rect;
        if (!VisibleRects.TryGetValue(sprite, out rect))
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var pixels = image.GetPixels32();
                int minX = image.width, minY = image.height, maxX = 0, maxY = 0;
                for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++)
                    if (pixels[y * image.width + x].a > 20)
                    {
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x + 1); maxY = Math.Max(maxY, y + 1);
                    }
                float px = sprite.rect.width / image.width, py = sprite.rect.height / image.height;
                rect = Rect.MinMaxRect((minX * px - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (minY * py - sprite.pivot.y) / sprite.pixelsPerUnit,
                    (maxX * px - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (maxY * py - sprite.pivot.y) / sprite.pixelsPerUnit);
                VisibleRects.Add(sprite, rect);
            }
            finally { Object.DestroyImmediate(image); }
        }
        float left = renderer.flipX ? -rect.xMax : rect.xMin, right = renderer.flipX ? -rect.xMin : rect.xMax;
        var bounds = new Bounds();
        bounds.SetMinMax(renderer.transform.TransformPoint(new Vector3(left, rect.yMin, -.1f)),
            renderer.transform.TransformPoint(new Vector3(right, rect.yMax, .1f)));
        return bounds;
    }
    internal static bool ReferencesValid(GameObject root)
    {
        foreach (var go in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject) > 0) return false;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) return false;
                var serialized = new SerializedObject(c);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                        property.objectReferenceInstanceIDValue != 0) return false;
            }
        }
        return true;
    }
    internal static bool Connected(bool[,] grid)
    {
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        int expected = 0;
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            if (grid[x, y]) { expected++; if (queue.Count == 0) queue.Enqueue(new Vector2Int(x, y)); }
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (!visited.Add(p)) continue;
            for (int i = 0; i < 4; i++)
            {
                int x = p.x + dx[i], y = p.y + dy[i];
                if (x >= 0 && x < Size && y >= 0 && y < Size && grid[x, y] && !visited.Contains(new Vector2Int(x, y)))
                    queue.Enqueue(new Vector2Int(x, y));
            }
        }
        return expected > 0 && visited.Count == expected;
    }
    static string[] Textures(string prefab)
    {
        return AssetDatabase.GetDependencies(prefab, true).Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    static string HashFile(string path) { return Hash(File.ReadAllBytes(path)); }
    static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    internal static void Render(GameObject prefab, string path, bool phone, Color? background = null)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        Camera camera = null;
        var previous = RenderTexture.active;
        try
        {
            PrefabUtility.InstantiatePrefab(prefab, scene);
            var cameraGo = new GameObject("MapPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            camera = cameraGo.GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = phone ? 10 : 15.3f;
            camera.transform.position = new Vector3(15, 15, -20);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background ?? new Color(.39f, .58f, .29f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("Map");
            var light = new GameObject("MapPreviewLight", typeof(Light));
            SceneManager.MoveGameObjectToScene(light, scene);
            light.GetComponent<Light>().type = LightType.Directional;
            int width = phone ? 540 : 1024, height = phone ? 960 : 1024;
            target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (camera != null) camera.targetTexture = null;
            if (image != null) Object.DestroyImmediate(image);
            if (target != null) Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
