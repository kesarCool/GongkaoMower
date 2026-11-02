using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Builds a separate autumn palette from the AI material macros, with phase-correct blob edges.</summary>
public static class AutumnAcademyTerrainBuilder
{
    public const string Root = MapResourcePaths.AutumnAcademy + "/Terrain";
    public const string Family = "autumn_stone_on_earth";
    public const string Background = "autumn_earth";
    public const int TilePixels = 64;
    private const int MacroPixels = 128;

    public static Dictionary<string, Tile> Ensure(IEnumerable<string> requiredNames)
    {
        string[] names = requiredNames.Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Directory.CreateDirectory(Root + "/Textures"); Directory.CreateDirectory(Root + "/Tiles");
        var stone = LoadMacro("stone"); var earth = LoadMacro("earth");
        try
        {
            var stonePixels = stone.GetPixels32(); var earthPixels = earth.GetPixels32();
            foreach (string name in names)
            {
                byte[] png = BuildPng(name, stonePixels, earthPixels);
                string file = Root + "/Textures/" + name + ".png";
                if (!File.Exists(file) || !File.ReadAllBytes(file).SequenceEqual(png)) File.WriteAllBytes(file, png);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(stone); UnityEngine.Object.DestroyImmediate(earth); }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var result = new Dictionary<string, Tile>();
        foreach (string name in names)
        {
            string texturePath = Root + "/Textures/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TilePixels; importer.maxTextureSize = TilePixels;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.alphaIsTransparency = false;
            importer.alphaSource = TextureImporterAlphaSource.None; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true; importer.compressionQuality = 85;
            var webgl = importer.GetPlatformTextureSettings("WebGL"); webgl.name = "WebGL"; webgl.overridden = true;
            webgl.maxTextureSize = TilePixels; webgl.format = TextureImporterFormat.DXT1Crunched; webgl.compressionQuality = 85;
            importer.SetPlatformTextureSettings(webgl); importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            string tilePath = Root + "/Tiles/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); tile.name = name; AssetDatabase.CreateAsset(tile, tilePath); }
            tile.sprite = sprite; tile.color = Color.white; tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile); result[name] = tile;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static Texture2D LoadMacro(string material)
    {
        string path = Root + "/Source/autumn_" + material + "_macro.png";
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!File.Exists(path) || !texture.LoadImage(File.ReadAllBytes(path)) || texture.width != MacroPixels || texture.height != MacroPixels)
        { UnityEngine.Object.DestroyImmediate(texture); throw new InvalidOperationException("Missing 128px autumn source macro: " + path); }
        return texture;
    }

    private static byte[] BuildPng(string name, Color32[] stone, Color32[] earth)
    {
        bool background = name.StartsWith(Background + "_", StringComparison.Ordinal);
        bool edge = name.StartsWith(Family + "_m", StringComparison.Ordinal);
        int variant = int.Parse(name.Substring(name.Length - 1));
        if (variant < 1 || variant > 4 || (!background && !edge && !name.StartsWith("autumn_stone_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Invalid autumn tile name: " + name);
        int mask = edge ? int.Parse(name.Substring((Family + "_m").Length, 3)) : 255;
        int originX = ((variant - 1) % 2) * TilePixels, originY = ((variant - 1) / 2) * TilePixels;
        var pixels = new Color32[TilePixels * TilePixels];
        for (int y = 0; y < TilePixels; y++) for (int x = 0; x < TilePixels; x++)
        {
            int source = (MacroPixels - 1 - originY - y) * MacroPixels + originX + x;
            var a = earth[source]; var b = stone[source];
            float amount = background ? 0 : edge ? Coverage(mask, x, y) : 1;
            pixels[(TilePixels - 1 - y) * TilePixels + x] = new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, amount)), (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, amount)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, amount)), 255);
        }
        var output = new Texture2D(TilePixels, TilePixels, TextureFormat.RGBA32, false);
        try { output.SetPixels32(pixels); output.Apply(); return output.EncodeToPNG(); }
        finally { UnityEngine.Object.DestroyImmediate(output); }
    }

    // Shared mathematical silhouette: matching neighbors have identical edge coverage.
    private static float Coverage(int mask, int x, int y)
    {
        int inside = 0;
        for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
            if (Inside(mask, x + (sx + .5f) / 4, y + (sy + .5f) / 4)) inside++;
        return inside / 16f;
    }
    private static bool Inside(int mask, float x, float y)
    {
        const float inset = 5, radius = 9;
        bool n = (mask & 1) != 0, e = (mask & 4) != 0, s = (mask & 16) != 0, w = (mask & 64) != 0;
        float left = w ? 0 : inset, right = e ? TilePixels : TilePixels - inset;
        float top = n ? 0 : inset, bottom = s ? TilePixels : TilePixels - inset;
        if (x < left || x > right || y < top || y > bottom) return false;
        if (!n && !w && x < left + radius && y < top + radius && Sqr(x - left - radius, y - top - radius) > radius * radius) return false;
        if (!n && !e && x > right - radius && y < top + radius && Sqr(x - right + radius, y - top - radius) > radius * radius) return false;
        if (!s && !w && x < left + radius && y > bottom - radius && Sqr(x - left - radius, y - bottom + radius) > radius * radius) return false;
        if (!s && !e && x > right - radius && y > bottom - radius && Sqr(x - right + radius, y - bottom + radius) > radius * radius) return false;
        if (n && w && (mask & 128) == 0 && Sqr(x, y) < inset * inset) return false;
        if (n && e && (mask & 2) == 0 && Sqr(x - TilePixels, y) < inset * inset) return false;
        if (s && w && (mask & 32) == 0 && Sqr(x, y - TilePixels) < inset * inset) return false;
        if (s && e && (mask & 8) == 0 && Sqr(x - TilePixels, y - TilePixels) < inset * inset) return false;
        return true;
    }
    private static float Sqr(float x, float y) { return x * x + y * y; }

    public static bool ValidateNeighborEdges(string[] rows)
    {
        var names = CartoonTilePlan.Build(rows, Family, Background);
        int width = rows[0].Length, height = rows.Length;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            for (int pixel = 0; pixel < TilePixels; pixel++)
            {
                string tile = names[y * width + x];
                if (x + 1 < width && FilledAt(tile, TilePixels, pixel + .5f) != FilledAt(names[y * width + x + 1], 0, pixel + .5f)) return false;
                if (y + 1 < height && FilledAt(tile, pixel + .5f, TilePixels) != FilledAt(names[(y + 1) * width + x], pixel + .5f, 0)) return false;
            }
        return true;
    }
    private static bool FilledAt(string name, float x, float y)
    {
        if (name.StartsWith(Background + "_", StringComparison.Ordinal)) return false;
        if (!name.StartsWith(Family + "_m", StringComparison.Ordinal)) return true;
        return Inside(int.Parse(name.Substring((Family + "_m").Length, 3)), x, y);
    }

    public static long TextureStorage(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var type = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
        var method = type.GetMethod("GetStorageMemorySizeLong", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        return Convert.ToInt64(method.Invoke(null, new object[] { texture }));
    }
}
