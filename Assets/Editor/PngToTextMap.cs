using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PNG → 多色文字地图 .txt。菜单入口：Tools/PNG转文字地图
/// 按像素主色调映射：
///   透明/白/亮灰 → .（地面）
///   暗色(所有通道<80) → #（暗色）
///   偏红 → @（金色）
///   偏蓝 → +（天蓝）
///   偏黄/橙 → *（橙色）
///   偏绿/青 → ~（青绿）
/// </summary>
public static class PngToTextMap
{
    [MenuItem("Tools/PNG转文字地图", false, 219)]
    public static void Run()
    {
        var tex = Selection.activeObject as Texture2D;
        if (tex == null)
        {
            EditorUtility.DisplayDialog("缺少 PNG", "请先在 Project 窗口选中一张 PNG 图片（需开启 Read/Write）。", "确定");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(tex);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("错误", "请选中 Project 窗口中的 PNG 文件。", "确定");
            return;
        }

        // 直接读文件字节，不需要 Read/Write Enabled
        byte[] fileBytes = File.ReadAllBytes(assetPath);
        var tmp = new Texture2D(2, 2);
        tmp.LoadImage(fileBytes);

        int w = tmp.width;
        int h = tmp.height;
        Color32[] pixels = tmp.GetPixels32();
        Object.DestroyImmediate(tmp);

        var sb = new StringBuilder();
        for (int y = h - 1; y >= 0; y--) // 翻转 Y：像素原点在左下，文字地图第一行在顶部
        {
            for (int x = 0; x < w; x++)
            {
                Color32 c = pixels[y * w + x];
                sb.Append(PixelToChar(c));
            }
            sb.AppendLine();
        }

        // 保存到同目录
        string dir = Path.GetDirectoryName(assetPath);
        string pngName = Path.GetFileNameWithoutExtension(assetPath);
        string outPath = Path.Combine(dir, pngName + ".txt");
        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        Debug.Log($"[PNG→TXT] {tex.name}.png ({w}x{h}) → {outPath}");
        EditorUtility.DisplayDialog("完成",
            $"已生成: {pngName}.txt\n\n尺寸: {w} × {h}\n路径: {outPath}\n\n"
            + "颜色映射：\n"
            + "  透明/白/亮灰 → .\n"
            + "  暗色 → #\n"
            + "  偏红 → @（金色）\n"
            + "  偏蓝 → +（天蓝）\n"
            + "  偏黄/橙 → *（橙色）\n"
            + "  偏绿/青 → ~（青绿）\n\n"
            + "可直接用 Tools/文字地图铺图 铺到 Tilemap（拖对应颜色瓦片）。",
            "确定");
    }

    [MenuItem("Tools/PNG转文字地图", true)]
    public static bool Validate() => Selection.activeObject is Texture2D && !EditorApplication.isPlaying;

    /// <summary>
    /// 像素主色调 → 文字地图字符。
    /// 透明/亮色→.  暗色→#  偏红→@  偏蓝→+  偏黄/橙→*  偏绿/青→~
    /// </summary>
    private static char PixelToChar(Color32 c)
    {
        if (c.a < 128) return '.';

        int r = c.r, g = c.g, b = c.b;
        int max = Mathf.Max(r, Mathf.Max(g, b));
        int min = Mathf.Min(r, Mathf.Min(g, b));

        // 暗色：所有通道都低 → #
        if (max < 80) return '#';

        // 接近灰度（饱和度低）→ .
        if (max - min < 30) return '.';

        // 偏红 → @（金色）
        if (r > g + 20 && r > b + 20) return '@';

        // 偏蓝 → +（天蓝）
        if (b > r + 20 && b > g + 20) return '+';

        // 偏黄/橙：红高、绿中、蓝低 → *
        if (r > b + 30 && g > b + 30) return '*';

        // 偏绿/青：绿高或绿蓝都高、红低 → ~
        if (g > r + 20) return '~';

        return '.';
    }
}
