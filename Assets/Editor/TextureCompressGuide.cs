using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 纹理压缩 + 未使用资源分析一站式工具。
/// 菜单: Tools/纹理优化分析
/// </summary>
public static class TextureCompressGuide
{
    [MenuItem("Tools/纹理优化分析", false, 210)]
    public static void Run()
    {
        // ── Step 1: 扫描哪些纹理被引用 ──
        var referencedPaths = new HashSet<string>();
        CollectReferencedAssets(referencedPaths);

        // ── Step 2: 列出所有纹理 + 分类 ──
        var allTextures = new List<(string path, long size, bool used, string category)>();
        string[] searchDirs = { "Assets/Res", "Assets/Resources" };
        foreach (string dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".tga") continue;
                if (file.Contains(".meta")) continue;

                string normalized = file.Replace("\\", "/");
                bool used = referencedPaths.Contains(normalized);
                string category = Classify(normalized);
                long size = new FileInfo(file).Length;
                allTextures.Add((normalized, size, used, category));
            }
        }

        // ── Step 3: 输出报告 ──
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("══════ 纹理优化分析报告 ══════");
        sb.AppendLine();

        var unused = allTextures.Where(t => !t.used).OrderByDescending(t => t.size).ToList();
        var usedLarge = allTextures.Where(t => t.used && t.size > 50_000).OrderByDescending(t => t.size).ToList();
        var usedSmall = allTextures.Where(t => t.used && t.size <= 50_000).ToList();

        long unusedTotal = unused.Sum(t => t.size);
        long usedTotalLarge = usedLarge.Sum(t => t.size);
        long usedTotalSmall = usedSmall.Sum(t => t.size);

        // ── 未使用纹理 ──
        sb.AppendLine($"── 未使用纹理 ({unused.Count} 个, {FormatSize(unusedTotal)}) —— 建议删除 ──");
        if (unused.Count == 0)
        {
            sb.AppendLine("  （无）");
        }
        else
        {
            foreach (var t in unused)
                sb.AppendLine($"  {FormatSize(t.size),8} [{t.category}] {Path.GetFileName(t.path)}");
        }
        sb.AppendLine();

        // ── 使用中、需压缩的大纹理 ──
        sb.AppendLine($"── 使用中但需压缩 ({usedLarge.Count} 个, {FormatSize(usedTotalLarge)}) ──");
        if (usedLarge.Count == 0)
        {
            sb.AppendLine("  （无）");
        }
        else
        {
            foreach (var t in usedLarge)
            {
                string note = t.size > 500_000 ? " ⚠ 超大，优先处理" : "";
                int targetKb = EstimateCompressedKb(t.size);
                sb.AppendLine($"  {FormatSize(t.size),8} → ~{targetKb}KB  [{t.category}] {Path.GetFileName(t.path)}{note}");
            }
        }
        sb.AppendLine();

        // ── 使用中的小纹理 ──
        sb.AppendLine($"── 使用中的小纹理 ({usedSmall.Count} 个, {FormatSize(usedTotalSmall)}) ──");
        sb.AppendLine($"  （无需压缩，已在合理范围内）");
        sb.AppendLine();

        // ── 汇总 ──
        long total = unusedTotal + usedTotalLarge + usedTotalSmall;
        long savings = unusedTotal; // 删除可省
        // 压缩预估：大纹理可压缩到 25%
        long compressSavings = (long)(usedTotalLarge * 0.75);
        sb.AppendLine($"── 汇总 ──");
        sb.AppendLine($"  纹理总大小:      {FormatSize(total)}");
        sb.AppendLine($"  可删除(未使用):  {FormatSize(unusedTotal)} → 直接清理");
        sb.AppendLine($"  需压缩(大纹理):  {FormatSize(usedTotalLarge)} → 预计可省 ~{FormatSize(compressSavings)}");
        sb.AppendLine($"  已OK(小纹理):    {FormatSize(usedTotalSmall)}");
        sb.AppendLine();
        sb.AppendLine($"── 操作指南 ──");
        sb.AppendLine($"  1. 去 Unity 删除上面-未使用纹理-的文件");
        sb.AppendLine($"  2. 选中-需压缩-的纹理 → Inspector 中 Texture Importer 设置：");
        sb.AppendLine($"     - Max Size: 原图较大设 1024，特效设 2048");
        sb.AppendLine($"     - Compression: RGBA Crunched ETC2");
        sb.AppendLine($"     - Crunch Quality: 50");
        sb.AppendLine($"     - Generate Mip Maps: 关");
        sb.AppendLine($"     - Filter Mode: Bilinear");
        sb.AppendLine($"     - 点击右下角 Apply");

        string reportPath = Path.Combine(Application.dataPath, "..", "texture_report.txt");
        File.WriteAllText(reportPath, sb.ToString());
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("纹理优化分析", $"报告已生成:\n{reportPath}\n\n"
            + $"未使用纹理: {unused.Count} 个 ({FormatSize(unusedTotal)})\n"
            + $"需压缩: {usedLarge.Count} 个 ({FormatSize(usedTotalLarge)})\n\n"
            + $"操作指南已写入报告中。",
            "确定");
    }

    private static void CollectReferencedAssets(HashSet<string> referenced)
    {
        // 从所有 Prefab / Scene / ScriptableObject / Material 收集 GUID 引用
        string[] dirs = { "Assets/Prefab", "Assets/Res/Prefabs", "Assets/Scenes", "Assets/Resources" };
        var guids = new HashSet<string>();

        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLower();
                bool isAsset = ext == ".prefab" || ext == ".unity" || ext == ".asset" || ext == ".mat";
                if (!isAsset) continue;
                if (file.Contains(".meta")) continue;

                var info = new FileInfo(file);
                if (info.Length > 1_000_000) continue; // 跳过超大文件

                try
                {
                    string content = File.ReadAllText(file);
                    // 提取所有 guid 引用: guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        content, @"guid:\s*([a-f0-9]{32})",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    foreach (System.Text.RegularExpressions.Match m in matches)
                        guids.Add(m.Groups[1].Value.ToLower());
                }
                catch { }
            }
        }

        // 把 guid 转成路径
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                referenced.Add(path);
        }
    }

    private static string Classify(string path)
    {
        if (path.Contains("/GeneratedTiles/")) return "地图瓦片";
        if (path.Contains("/image/")) return "UI";
        if (path.Contains("/Effect/") || path.Contains("/Textures/")) return "特效";
        if (path.Contains("/Fonts/")) return "字体图标";
        if (path.Contains("/Map/")) return "地图";
        return "其他";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000) return $"{bytes / 1_000_000.0:F1}MB";
        if (bytes >= 1_000) return $"{bytes / 1_000.0:F0}KB";
        return $"{bytes}B";
    }

    private static int EstimateCompressedKb(long bytes)
    {
        // Crunched ETC2 压缩率大约 75-85%
        return (int)(bytes * 0.25 / 1000);
    }
}
