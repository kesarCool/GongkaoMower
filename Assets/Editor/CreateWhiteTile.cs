using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成纯白 32×32 瓦片贴图。菜单入口：Tools/生成白色瓦片贴图
/// </summary>
public static class CreateWhiteTile
{
    [MenuItem("Tools/生成白色瓦片贴图", false, 218)]
    public static void Run()
    {
        string dir = "Assets/Res/Textures";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string path = Path.Combine(dir, "tile_white_32.png");

        // 纯白 32×32 RGBA
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[32 * 32];
        Color32 white = new Color32(255, 255, 255, 255);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = white;
        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] pngBytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, pngBytes);
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        // 自动设置导入参数
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        Debug.Log($"[白色瓦片] 已生成: {path}");
        EditorUtility.DisplayDialog("完成", $"已生成: {path}\n\n下一步：右键 Create → 2D → 瓦片 → 拖入此贴图 → 改 Color 做彩色瓦片。", "确定");
    }
}
