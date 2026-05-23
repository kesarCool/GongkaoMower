using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成三章地图所需的极简瓦片纹理（纯色 + 简单噪点）。
/// 不需要 PS，纯代码出图。
/// </summary>
public static class GenerateMapTiles
{
    private const string OutputDir = "Assets/Res/Map/GeneratedTiles";
    private const int Size = 32;

    [MenuItem("Tools/生成地图瓦片纹理", false, 201)]
    public static void Run()
    {
        if (!Directory.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);

        // 第一章：新手村（绿色系）
        GenerateSolid("ch1_grass",    new Color(0.30f, 0.59f, 0.31f));  // 草地
        GenerateSolid("ch1_dirt",     new Color(0.63f, 0.53f, 0.47f));  // 土路
        GenerateSolid("ch1_water",    new Color(0.39f, 0.71f, 0.96f));  // 水域
        GenerateSolid("ch1_wall",     new Color(0.33f, 0.38f, 0.25f));  // 围墙（深绿）

        // 第二章：暗色系
        GenerateSolid("ch2_ground",   new Color(0.22f, 0.28f, 0.31f));  // 暗灰地面
        GenerateSolid("ch2_stone",    new Color(0.33f, 0.43f, 0.48f));  // 石板路
        GenerateSolid("ch2_abyss",    new Color(0.15f, 0.19f, 0.22f));  // 深渊
        GenerateSolid("ch2_wall",     new Color(0.18f, 0.22f, 0.24f));  // 围墙（暗色）

        // 第三章：熔岩系
        GenerateSolid("ch3_ground",   new Color(0.36f, 0.25f, 0.22f));  // 暗红地面
        GenerateSolid("ch3_scorched", new Color(0.24f, 0.15f, 0.14f));  // 焦土
        GenerateSolid("ch3_lava",     new Color(1.00f, 0.34f, 0.13f));  // 岩浆
        GenerateSolid("ch3_wall",     new Color(0.20f, 0.14f, 0.12f));  // 围墙（深棕）

        // 通用（第一章过渡区也可用）
        GenerateSolid("common_stone", new Color(0.50f, 0.50f, 0.50f));

        // 给草地和地面加细微噪点（看起来不是纯色平板）
        AddNoise(Path.Combine(OutputDir, "ch1_grass.png"), 0.04f);

        AssetDatabase.Refresh();
        ApplySpriteSettings();

        EditorUtility.DisplayDialog("生成地图瓦片",
            $"已生成 13 个瓦片纹理到:\n{OutputDir}\n\n" +
            "所有文件已设为 Sprite(2D and UI)，可直接拖入 Tile Palette。\n" +
            "如需换色，改 Tools/生成地图瓦片纹理 中的颜色值重新运行即可。",
            "确定");

        Debug.Log("[GenerateMapTiles] 完成，13 个 tile 已生成。");
    }

    private static void GenerateSolid(string name, Color color)
    {
        string path = Path.Combine(OutputDir, name + ".png");
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    /// <summary>
    /// 对已有 PNG 纹理叠加细微随机噪点，避免纯色平板的廉价感。
    /// </summary>
    private static void AddNoise(string path, float intensity)
    {
        if (!File.Exists(path)) return;
        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        var pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float n = (Random.value - 0.5f) * intensity;
            pixels[i] = new Color(
                Mathf.Clamp01(pixels[i].r + n),
                Mathf.Clamp01(pixels[i].g + n),
                Mathf.Clamp01(pixels[i].b + n),
                pixels[i].a
            );
        }
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ApplySpriteSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { OutputDir });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = Size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
        }
    }
}
