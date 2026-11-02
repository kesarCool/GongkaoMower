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

/// <summary>
/// 从美术交付中的布局和散图生成独立的地图预览Prefab。
/// 只复制实际用到的素材；不改现有关卡、Resources或地图配表。
/// </summary>
public static class CartoonBattleMapGenerator
{
    private const string Version = "v1.2";
    private const string DefaultSource = "美术资源/地图设计/明快卡通_战斗地图_v1";

    [Serializable] public class ArtAsset
    {
        public string name, kind, path;
        public int size, ppu;
    }
    [Serializable] public class ArtManifest { public ArtAsset[] assets; }
    [Serializable] public class GridInfo { public int width, height; }
    [Serializable] public class Prop
    {
        public string sprite;
        public float xCell, yCellFromTop, previewWidthPx;
    }
    [Serializable] public class Layout
    {
        public string name, foregroundFamily, background;
        public GridInfo grid;
        public string[] terrainNamesRowMajor;
        public Prop[] props;
    }
    [Serializable] public class BuildReport
    {
        public string generatorVersion, sourceFolder, outputFolder;
        public int tileResolution, importedTextures;
        public string[] prefabs;
        public string status = "美术预览；未接入战斗、碰撞、刷怪约束和配表。";
    }

    [MenuItem("Tools/地图/明快卡通/一键生成三张地图（32px，推荐）", false, 225)]
    public static void Generate32() { RunFromMenu(32); }

    [MenuItem("Tools/地图/明快卡通/一键生成三张地图（64px）", false, 226)]
    public static void Generate64() { RunFromMenu(64); }

    [MenuItem("Tools/地图/明快卡通/一键生成三张地图（32px，推荐）", true)]
    [MenuItem("Tools/地图/明快卡通/一键生成三张地图（64px）", true)]
    public static bool CanGenerate() { return !EditorApplication.isPlayingOrWillChangePlaymode; }

    private static void RunFromMenu(int resolution)
    {
        string source = Path.GetFullPath(DefaultSource);
        if (!File.Exists(Path.Combine(source, "03_使用说明/素材清单.json")))
        {
            source = EditorUtility.OpenFolderPanel("选择明快卡通_战斗地图_v1交付目录", "", "");
            if (string.IsNullOrEmpty(source)) return;
        }
        try
        {
            BuildReport report = Generate(source, resolution);
            Object first = AssetDatabase.LoadMainAssetAtPath(report.prefabs[0]);
            Selection.activeObject = first;
            EditorGUIUtility.PingObject(first);
            Debug.Log("[卡通地图] " + JsonUtility.ToJson(report, true));
            EditorUtility.DisplayDialog("地图已生成", "三张地图Prefab：\n" + report.outputFolder +
                "\n\n已定位第一张，双击Prefab即可查看。重复生成相同素材会复用已有结果。" +
                "\n\n本轮为美术预览，不改变当前战斗地图。滨水图水域和所有装饰尚未设置阻挡。", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("地图生成未完成", ex.Message + "\n\n现有关卡没有被替换。", "确定");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    public static BuildReport Generate(string sourceFolder, int resolution)
    {
        if (resolution != 32 && resolution != 64) throw new ArgumentOutOfRangeException(nameof(resolution));
        sourceFolder = Path.GetFullPath(sourceFolder);
        string manifestText = File.ReadAllText(Path.Combine(sourceFolder, "03_使用说明/素材清单.json"), Encoding.UTF8);
        var manifest = JsonUtility.FromJson<ArtManifest>(manifestText);
        if (manifest == null || manifest.assets == null) throw new InvalidDataException("素材清单无效。");
        string[] layoutPaths = Directory.GetFiles(Path.Combine(sourceFolder, "03_使用说明"), "*_实拼布局.json");
        Array.Sort(layoutPaths, StringComparer.Ordinal);
        if (layoutPaths.Length != 3) throw new InvalidDataException("交付目录必须包含三张实拼布局JSON。");
        string[] texts = layoutPaths.Select(p => File.ReadAllText(p, Encoding.UTF8)).ToArray();
        Layout[] layouts = texts.Select(JsonUtility.FromJson<Layout>).ToArray();
        var blueprintTexts = new List<string>();
        var art = manifest.assets.Where(a => a.kind == "decoration" || a.size == resolution)
            .ToDictionary(a => a.name, StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < layouts.Length; index++)
        {
            Layout layout = layouts[index];
            string blueprintName = Path.GetFileName(layoutPaths[index]).Replace("_实拼布局.json", "_基础地形.txt");
            string blueprint = File.ReadAllText(Path.Combine(sourceFolder, "06_现有工具蓝图", blueprintName), Encoding.UTF8).TrimEnd('\r', '\n');
            blueprintTexts.Add(blueprint);
            string[] rows = blueprint.Split('\n').Select(r => r.TrimEnd('\r')).ToArray();
            if (layout == null || layout.grid == null || rows.Length != layout.grid.height || rows.Any(r => r.Length != layout.grid.width))
                throw new InvalidDataException("蓝图行列数与布局不一致：" + blueprintName);
            layout.terrainNamesRowMajor = CartoonTilePlan.Build(rows, layout.foregroundFamily, layout.background);
            if (layout == null || layout.grid == null || layout.grid.width < 1 || layout.grid.height < 1 ||
                layout.grid.width > 128 || layout.grid.height > 128 || layout.terrainNamesRowMajor == null ||
                layout.terrainNamesRowMajor.Length != layout.grid.width * layout.grid.height)
                throw new InvalidDataException("布局尺寸或瓦片数量无效。");
            foreach (string name in layout.terrainNamesRowMajor) required.Add(name);
            foreach (Prop prop in layout.props ?? Array.Empty<Prop>()) required.Add(prop.sprite);
        }
        string[] names = required.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var sourceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (string.IsNullOrEmpty(name) || name.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') || !art.ContainsKey(name))
                throw new InvalidDataException("缺少素材或素材名称无效：" + name);
            string file = ResolveSourceFile(sourceFolder, art[name].path);
            if (!File.Exists(file)) throw new FileNotFoundException("素材文件不存在", file);
            sourceFiles[name] = file;
        }
        string hash;
        using (var bytes = new MemoryStream())
        using (var sha = SHA256.Create())
        {
            byte[] header = Encoding.UTF8.GetBytes(Version + ":" + resolution + "\n" + manifestText + "\n" + string.Join("\n", texts) + "\n" + string.Join("\n", blueprintTexts));
            bytes.Write(header, 0, header.Length);
            foreach (string name in names)
            {
                byte[] data = File.ReadAllBytes(sourceFiles[name]);
                byte[] label = Encoding.UTF8.GetBytes(name + ":" + data.Length + "\n");
                bytes.Write(label, 0, label.Length); bytes.Write(data, 0, data.Length);
            }
            hash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").Substring(0, 10).ToLowerInvariant();
        }
        string output = resolution == 32 && hash == "db482fa081" ? MapResourcePaths.SharedCartoon
            : MapResourcePaths.Styles + "/Imported/CartoonMaps_" + resolution + "_" + hash;
        string reportPath = output + "/BuildReport.json";
        if (File.Exists(reportPath))
        {
            var old = JsonUtility.FromJson<BuildReport>(File.ReadAllText(reportPath, Encoding.UTF8));
            if (old != null && old.prefabs != null && old.prefabs.Length == 3 &&
                old.prefabs.All(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null) &&
                names.All(n => AssetDatabase.LoadAssetAtPath<Sprite>(output + "/Textures/" + n + ".png") != null &&
                    (art[n].kind != "terrain" || AssetDatabase.LoadAssetAtPath<Tile>(output + "/Tiles/" + n + ".asset") != null))) return old;
        }
        string initialOutput = output;
        int suffix = 1;
        while (Directory.Exists(output)) output = initialOutput + "_" + suffix++;
        Directory.CreateDirectory(output + "/Textures");
        Directory.CreateDirectory(output + "/Tiles");
        Directory.CreateDirectory(output + "/Prefabs");
        foreach (string name in names) File.Copy(sourceFiles[name], output + "/Textures/" + name + ".png", false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        var tiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i], assetPath = output + "/Textures/" + name + ".png";
                EditorUtility.DisplayProgressBar("明快卡通地图", "导入实际使用的素材：" + name, i / (float)names.Length * 0.8f);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("无法导入：" + assetPath);
                ArtAsset record = art[name];
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = record.ppu;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = record.kind == "decoration";
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = 9; // Custom pivot.
                settings.spritePivot = record.kind == "decoration" ? new Vector2(0.5f, 0.05f) : new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null) throw new InvalidOperationException("Sprite为空：" + assetPath);
                sprites.Add(name, sprite);
                if (record.kind != "terrain") continue;
                Tile tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = name; tile.sprite = sprite; tile.color = Color.white; tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, output + "/Tiles/" + name + ".asset");
                tiles.Add(name, tile);
            }
            var prefabPaths = new List<string>();
            for (int i = 0; i < layouts.Length; i++)
            {
                EditorUtility.DisplayProgressBar("明快卡通地图", "拼接：" + layouts[i].name, 0.8f + i / 15f);
                prefabPaths.Add(BuildPrefab(layouts[i], i, output, tiles, sprites));
            }
            var report = new BuildReport { generatorVersion = Version, sourceFolder = sourceFolder, outputFolder = output,
                tileResolution = resolution, importedTextures = names.Length, prefabs = prefabPaths.ToArray() };
            File.WriteAllText(output + "/BuildReport.json", JsonUtility.ToJson(report, true), Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static string ResolveSourceFile(string root, string relative)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative ?? ""));
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !result.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("素材路径必须位于交付目录内，且为PNG。");
        return result;
    }

    private static string BuildPrefab(Layout layout, int index, string output, Dictionary<string, Tile> tiles, Dictionary<string, Sprite> sprites)
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = new GameObject("CartoonMap_" + (index + 1).ToString("00") + "_" + layout.name, typeof(Grid));
            SceneManager.MoveGameObjectToScene(root, previewScene);
            root.GetComponent<Grid>().cellSize = new Vector3(1, 1, 0);
            var groundObject = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            int mapLayer=LayerMask.NameToLayer("Map");
            if(mapLayer<0)throw new InvalidOperationException("Missing Map layer; cannot generate terrain visible to MiniMapUI.");
            groundObject.layer=mapLayer;
            groundObject.transform.SetParent(root.transform, false);
            Tilemap ground = groundObject.GetComponent<Tilemap>();
            ground.tileAnchor = new Vector3(0.5f, 0.5f, 0);
            var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            var groundRenderer = groundObject.GetComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = -100;
            groundRenderer.sharedMaterial = material;
            int w = layout.grid.width, h = layout.grid.height;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                ground.SetTile(new Vector3Int(x, h - 1 - y, 0), tiles[layout.terrainNamesRowMajor[y * w + x]]);
            ground.CompressBounds();
            var decor = new GameObject("Decoration");
            decor.layer=mapLayer;
            decor.transform.SetParent(root.transform, false);
            foreach (Prop prop in layout.props ?? Array.Empty<Prop>())
            {
                var child = new GameObject(prop.sprite, typeof(SpriteRenderer));
                child.layer=mapLayer;
                child.transform.SetParent(decor.transform, false);
                child.transform.localPosition = new Vector3(prop.xCell, h - prop.yCellFromTop, 0);
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = sprites[prop.sprite];
                renderer.sharedMaterial = material;
                renderer.sortingOrder = -50;
                float width = renderer.sprite.bounds.size.x;
                child.transform.localScale = Vector3.one * (prop.previewWidthPx / 64f / width);
            }
            string file = output + "/Prefabs/" + root.name + ".prefab";
            if (PrefabUtility.SaveAsPrefabAsset(root, file) == null) throw new InvalidOperationException("Prefab保存失败：" + file);
            return file;
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
}
