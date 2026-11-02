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

/// <summary>Chapter two seasonal battle maps; preserves the reusable ZlMap bamboo library.</summary>
public static class AutumnAcademyMapGenerator
{
    public const string Art = MapResourcePaths.AutumnAcademy + "/Textures";
    public const string Preview = "ArtOutput/World/AutumnAcademy";
    private const string Output = "Assets/Resources/Map";
    private static readonly string[] NewArt = { "maple_tree", "ginkgo_tree", "autumn_shrub", "academy_gate", "study_table", "autumn_leaves" };
    private static readonly string[] Labels = { "枫庭迎门", "银杏斜阶", "秋影回庭", "丹枫寻章", "落叶书径", "双庭听秋", "书山秋阶", "文昌秋试" };

    [Serializable] public class MapCheck
    {
        public int stage, groundCells, decorationCount;
        public string label, prefab, terrainHash;
        public bool loadable, referencesValid, boundsValid, spawnClear, decorationsInside, mapLayerValid, connected, wallsValid, noBamboo, autumnTerrainOnly;
    }
    [Serializable] public class Report
    {
        public string theme = "autumn_academy", bambooLibrary = MapResourcePaths.RuntimeMaps + "/ZlMap1..8.prefab";
        public bool passed, bambooPreserved, chapterOneUnchanged, sceneUnchanged, uniqueLayouts;
        public long newPngSourceBytes, newTextureStorageBytes;
        public long terrainTextureStorageBytes;
        public int sharedTerrainTextures, newDecorationTextures;
        public MapCheck[] maps;
        public string note = "Actual Unity prefab renders; independent 64px autumn terrain and shared autumn decorations. Source/Editor texture sizes are not a new WeChat package measurement. Reusable bamboo ZlMap1..8 are in Resources and are included in builds.";
    }

    [MenuItem("Tools/Maps/Chapter Two/Generate Autumn Academy (2-1 to 2-8)")]
    private static void Menu() { Debug.Log(Generate()); }
    [MenuItem("Tools/Maps/Chapter Two/Generate Autumn Academy (2-1 to 2-8)", true)]
    private static bool CanGenerate() { return !EditorApplication.isPlayingOrWillChangePlaymode; }

    public static void RunBatch()
    {
        try { Debug.Log(Generate()); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static string Generate()
    {
        if (!CanGenerate()) throw new InvalidOperationException("Exit Play mode before generating autumn maps.");
        var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty; int sceneCount = SceneManager.sceneCount;
        var protectedMaps = Directory.GetFiles(Output, "TileMap1_*.prefab").ToDictionary(p => p, HashFile);
        string backup = "Library/MapBackups/AutumnAcademy-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        foreach (string file in Directory.GetFiles(Output, "TileMap2_*.prefab*")) File.Copy(file, backup + "/" + Path.GetFileName(file));
        var archived = PreserveBambooMaps();
        PrepareArt();
        var tiles = AutumnAcademyTerrainBuilder.Ensure(ChapterTwoMapGenerator.Plans().SelectMany(p => CartoonTilePlan.Build(p.Rows(), AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background)));
        var sprites = LoadSprites();
        Directory.CreateDirectory(Preview + "/blueprints");
        var maps = new List<MapCheck>();
        try
        {
            foreach (var plan in ChapterTwoMapGenerator.Plans())
            {
                EditorUtility.DisplayProgressBar("Autumn Academy", plan.stage + "/8 " + Labels[plan.stage - 1], (plan.stage - 1) / 8f);
                var props = MakeAutumnProps(plan);
                string path = ChapterTwoMapGenerator.MapPath(plan.stage);
                Common.BuildThemedPrefab(path, plan.Rows(), AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background, props, tiles, sprites);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var sprite in contents.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sprite.sprite.name == "autumn_leaves") sprite.sortingOrder = -90;
                    contents.transform.Find("Decoration").gameObject.SetActive(true);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                var check = Inspect(plan, path);
                maps.Add(check);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Common.Render(prefab, Preview + "/TileMap2_" + plan.stage + ".png", false, new Color(.74f, .51f, .37f));
                Common.Render(prefab, Preview + "/TileMap2_" + plan.stage + "-phone.png", true, new Color(.74f, .51f, .37f));
                File.WriteAllText(Preview + "/blueprints/TileMap2_" + plan.stage + ".txt", string.Join("\n", plan.Rows()) + "\n", new UTF8Encoding(false));
            }
            AssetDatabase.SaveAssets();
            var dependencies = maps.SelectMany(m => AssetDatabase.GetDependencies(m.prefab, true)).Where(p => p.EndsWith(".png")).Distinct().ToArray();
            var report = new Report {
                maps = maps.ToArray(), bambooPreserved = archived.All(p => HashFile(p.Key) == p.Value),
                chapterOneUnchanged = protectedMaps.All(p => HashFile(p.Key) == p.Value),
                sceneUnchanged = scene == SceneManager.GetActiveScene() && scene.isDirty == dirty && SceneManager.sceneCount == sceneCount,
                uniqueLayouts = maps.Select(m => m.terrainHash).Distinct().Count() == 8,
                newPngSourceBytes = NewArt.Sum(n => new FileInfo(Art + "/" + n + ".png").Length),
                newTextureStorageBytes = NewArt.Sum(TextureStorage), newDecorationTextures = NewArt.Length,
                sharedTerrainTextures = dependencies.Count(p => !p.StartsWith(Art + "/", StringComparison.Ordinal) && !p.StartsWith(AutumnAcademyTerrainBuilder.Root + "/", StringComparison.Ordinal)),
                terrainTextureStorageBytes = dependencies.Where(p => p.StartsWith(AutumnAcademyTerrainBuilder.Root + "/Textures/", StringComparison.Ordinal)).Sum(AutumnAcademyTerrainBuilder.TextureStorage)
            };
            report.passed = maps.Count == 8 && report.bambooPreserved && report.chapterOneUnchanged && report.sceneUnchanged && report.uniqueLayouts && maps.All(Valid);
            File.WriteAllText(Preview + "/build-report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.passed) throw new InvalidOperationException("Autumn map checks failed: " + Preview + "/build-report.json");
            return JsonUtility.ToJson(report, true);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static Dictionary<string, string> PreserveBambooMaps()
    {
        var hashes = new Dictionary<string, string>();
        for (int stage = 1; stage <= 8; stage++)
        {
            string path = MapResourcePaths.BambooMap(stage);
            if (!File.Exists(path)) throw new InvalidOperationException("Missing bamboo library map: " + path + ". Run the Map Library/Generate Bamboo Academy menu first.");
            hashes[path] = HashFile(path);
            hashes[path + ".meta"] = HashFile(path + ".meta");
        }
        return hashes;
    }
    public static string RegenerateBambooLibrary()
    {
        return ChapterTwoMapGenerator.Generate();
    }
    private static Dictionary<string, Sprite> LoadSprites()
    {
        var names = new[] { "bamboo", "stone_lantern", "stone_bench", "shrub", "rocks", "sign" };
        var sprites = names.ToDictionary(n => n, n => AssetDatabase.LoadAssetAtPath<Sprite>(Common.Art + "/Textures/" + n + ".png"));
        foreach (string name in NewArt) sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/" + name + ".png");
        return sprites;
    }

    private static void PrepareArt()
    {
        foreach (string name in NewArt)
        {
            string path = Art + "/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            int size = name == "maple_tree" || name == "ginkgo_tree" || name == "academy_gate" ? 256 : 128;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128; importer.spriteBorder = Vector4.zero;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = name == "autumn_leaves" ? new Vector2(.5f, .5f) : new Vector2(.5f, .05f);
            importer.SetTextureSettings(settings); importer.maxTextureSize = size;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.crunchedCompression = true; importer.compressionQuality = 80;
            var webgl = importer.GetPlatformTextureSettings("WebGL"); webgl.name = "WebGL"; webgl.overridden = true;
            webgl.maxTextureSize = size; webgl.format = TextureImporterFormat.DXT5Crunched; webgl.compressionQuality = 80;
            importer.SetPlatformTextureSettings(webgl); importer.SaveAndReimport();
        }
    }

    private static List<Common.Prop> MakeAutumnProps(ChapterTwoMapGenerator.Plan plan)
    {
        var result = new List<Common.Prop>(); int tree = plan.stage;
        foreach (var old in plan.props)
        {
            string name = old.sprite; float width = old.width;
            if (name == "bamboo") { name = tree++ % 3 == 0 ? "ginkgo_tree" : "maple_tree"; width = Mathf.Min(2.6f, width); }
            else if (name == "shrub") name = "autumn_shrub";
            else if (name == "stone_bench" && result.Count % 2 == 0) { name = "study_table"; width = 1.8f; }
            Add(result, name, old.x, old.y, width, old.flip);
            if (name == "maple_tree" || name == "ginkgo_tree") Add(result, "autumn_leaves", old.x, old.y + .2f, width * .8f, old.flip);
        }
        // Near the mobile view's sides, outside the central 5x5 spawning area.
        AddTreeIfClear(result, plan.stage % 2 == 0 ? "ginkgo_tree" : "maple_tree", 10.8f, 20.2f, 2.3f);
        AddTreeIfClear(result, plan.stage % 2 == 0 ? "maple_tree" : "ginkgo_tree", 19.2f, 7.5f, 2.3f);
        if (plan.stage == 1 || plan.stage == 8) Add(result, "academy_gate", 15, 22.3f, 3.1f);
        if (plan.stage == 2 || plan.stage == 4 || plan.stage == 7) Add(result, "study_table", 9.7f, 18.2f, 1.6f);
        foreach (var p in new[] { new Vector2(11, 8.5f), new Vector2(20.1f, 20.2f), new Vector2(9.8f, 11), new Vector2(19, 24), new Vector2(10.5f, 24.5f) })
            Add(result, "autumn_leaves", p.x, p.y, .85f + plan.stage % 3 * .15f, plan.stage % 2 == 0);
        return result;
    }
    private static void AddTreeIfClear(List<Common.Prop> props, string name, float x, float y, float width)
    {
        if (props.Any(p => p.sprite != "autumn_leaves" && Vector2.Distance(new Vector2(p.x, p.y), new Vector2(x, y)) < 2.7f)) return;
        Add(props, name, x, y, width); Add(props, "autumn_leaves", x, y + .2f, 1.9f);
    }
    private static void Add(List<Common.Prop> props, string sprite, float x, float y, float width, bool flip = false)
    { props.Add(new Common.Prop { name = sprite + "_" + props.Count.ToString("00"), sprite = sprite, x = x, y = y, width = width, flip = flip }); }

    private static MapCheck Inspect(ChapterTwoMapGenerator.Plan plan, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); var ground = prefab.GetComponentInChildren<Tilemap>();
        var decoration = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        var terrain = CartoonTilePlan.Build(plan.Rows(), AutumnAcademyTerrainBuilder.Family, AutumnAcademyTerrainBuilder.Background);
        var check = new MapCheck {
            stage = plan.stage, label = Labels[plan.stage - 1], prefab = path, groundCells = ground.GetTilesBlock(ground.cellBounds).Count(t => t != null),
            decorationCount = decoration.Length, terrainHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", plan.Rows()))),
            loadable = Resources.Load<GameObject>("Map/TileMap2_" + plan.stage) == prefab, referencesValid = Common.ReferencesValid(prefab),
            boundsValid = ground.cellBounds == new BoundsInt(0, 0, 0, 30, 30, 1) && prefab.GetComponentsInChildren<Collider2D>(true).Length == 0,
            spawnClear = true, decorationsInside = true, connected = Common.Connected(plan.stone),
            mapLayerValid = prefab.GetComponentsInChildren<Renderer>(true).All(r => r.gameObject.layer == LayerMask.NameToLayer("Map")),
            noBamboo = decoration.All(r => r.sprite != null && r.sprite.name != "bamboo"),
            autumnTerrainOnly = ground.GetTilesBlock(ground.cellBounds).All(t => t != null && AssetDatabase.GetAssetPath(t).StartsWith(AutumnAcademyTerrainBuilder.Root + "/Tiles/", StringComparison.Ordinal))
        };
        var spawn = new Bounds(new Vector3(15, 15, 0), new Vector3(5, 5, 10));
        foreach (var sprite in decoration)
        {
            Bounds b = Common.VisibleBounds(sprite);
            if (b.Intersects(spawn)) check.spawnClear = false;
            if (b.min.x < -.01f || b.min.y < -.01f || b.max.x > 30.01f || b.max.y > 30.01f) check.decorationsInside = false;
        }
        for (int y = 0; y < 30; y++) for (int x = 0; x < 30; x++)
            if (ground.GetTile(new Vector3Int(x, 29 - y, 0)).name != terrain[y * 30 + x]) throw new InvalidOperationException("Ground changed: " + path);
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            typeof(BattleMapLoader).GetMethod("BuildPerimeterWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { instance.transform, instance.GetComponentInChildren<Tilemap>() });
            var wall = instance.transform.Find("Wall");
            check.wallsValid = wall != null && wall.childCount == 4 && wall.GetComponentsInChildren<WallMarker>().Length == 4;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return check;
    }
    private static bool Valid(MapCheck check)
    { return check.groundCells == 900 && check.loadable && check.referencesValid && check.boundsValid && check.spawnClear && check.decorationsInside && check.connected && check.mapLayerValid && check.wallsValid && check.noBamboo && check.autumnTerrainOnly; }
    private static long TextureStorage(string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + name + ".png");
        var type = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
        var method = type.GetMethod("GetStorageMemorySizeLong", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        return Convert.ToInt64(method.Invoke(null, new object[] { texture }));
    }
    private static string HashFile(string file) { return Hash(File.ReadAllBytes(file)); }
    private static string Hash(byte[] bytes)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
}
