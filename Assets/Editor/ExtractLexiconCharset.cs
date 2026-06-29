using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FlatBuffers;
using ProtoTable;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从所有数据表 + UI/代码中提取全部不重复汉字 → charset.txt，
/// 用于 TMP Font Asset Creator 裁剪 SDF 字体。
/// </summary>
public static class ExtractLexiconCharset
{
    private const string MenuPath = "Tools/提取词库字符集";

    [MenuItem(MenuPath, false, 200)]
    public static void Run()
    {
        var charset = new HashSet<char>();
        var sources = new StringBuilder();
        int tableTotalRows = 0;

        // ── 1. 表数据 ──
        CollectFromTable("LexiconTable",    "DisplayText",     ref charset, ref tableTotalRows, sources);
        CollectFromTable("Monster",         "name",            ref charset, ref tableTotalRows, sources);
        CollectFromTable("Monster",         "beizhu",          ref charset, ref tableTotalRows, sources);
        CollectFromTable("ChapterLevel",    "mapName",         ref charset, ref tableTotalRows, sources);
        CollectFromTable("ErrorCodeTable",  "errMsg",          ref charset, ref tableTotalRows, sources);
        CollectFromTable("ItemTable",          "ItemName",        ref charset, ref tableTotalRows, sources);
        CollectFromTable("ItemTable",          "Description",     ref charset, ref tableTotalRows, sources);
        CollectFromTable("AchievementConfig",  "Description",     ref charset, ref tableTotalRows, sources);

        // ── 2. 扫描代码和 UI 中的硬编码中文 ──
        int codeHits = CollectFromCodeAndPrefabs(ref charset);

        // ── 3. 标点符号与特殊字符 ──
        // 数字 + 英文 + 英文标点
        AddChars(charset, "0123456789");
        AddChars(charset, "abcdefghijklmnopqrstuvwxyz");
        AddChars(charset, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        AddChars(charset, "!?.,;:+-*/=()[]{}<>@#$%^&_|~ ");
        AddChars(charset, "\\/'\"`");  // 反斜杠 斜杠 单引号 双引号 反引号

        // 数学符号
        AddChars(charset, "©®™°∞≠≤≥≈");
        AddChars(charset, "∆∑√∫∂≪≫");

        // 中文标点（逐个 Unicode 转义，避免 C# 字符串解析问题）
        AddChars(charset, "、。，、；：？！"); // 、。，、；：？！
        AddChars(charset, "…—～·");                         // …—～·
        AddChars(charset, "‘’“”");                         // 中文引号 '' ""
        AddChars(charset, "《》「」『』");             // 《》「」『』
        AddChars(charset, "（）【】");                         // （）【】
        AddChars(charset, "〔〕［］｛｝");             // 〔〕［］｛｝
        AddChars(charset, "×÷＋－±");                  // ×÷＋－±
        AddChars(charset, "≤≥≠≪≫");                   // ≤≥≠≪≫
        AddChars(charset, "∞∝∵∴∧∨");             // ∞∝∵∴∧∨
        AddChars(charset, "！※");                                      // ！※

        // 箭头
   //     AddChars(charset, "←↑→↓↔↕");             // ←↑→↓↔↕
    //    AddChars(charset, "↖↗↘↙");                         // ↖↗↘↙
    //    AddChars(charset, "➤➜➙➚");                         // ➤➜➙➚

        // UI 常用图形
    //    AddChars(charset, "▼▶▲◀");                         // ▼▶▲◀
        AddChars(charset, "◆●○★☆");                   // ◆●○★☆
    //    AddChars(charset, "✓✗✘☐☑☒");             // ✓✗✘☐☑☒
  //      AddChars(charset, "♠♣♥♦♪♫");             // ♠♣♥♦♪♫

        // 序号
        AddChars(charset, "①②③④⑤⑥⑦⑧⑨");  // ①②③④⑤⑥⑦⑧⑨
        AddChars(charset, "ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩ"); // Ⅰ-Ⅹ
        AddChars(charset, "㈠㈡㈢㈣㈤㈥㈦㈧㈨㈩"); // ㈠-㈩

        // 单位
        AddChars(charset, "㎜㎝㎞㎡㏄㏎㏑㏒㏕");  // ㎜㎝㎞㎡㏄㏎㏑㏒㏕

        // ── UI 伤害飘字用字（静态方法中的字符串常量，正则可能漏）──
        AddChars(charset, "暴击");

        // ── 输出 ──
        string outputPath = Path.Combine(Application.dataPath, "..", "charset.txt");
        var sb = new StringBuilder();
        foreach (char c in charset) sb.Append(c);
        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);

        string msg = $"提取完成！\n\n"
                   + $"表数据总行数: {tableTotalRows}\n"
                   + $"代码/UI 命中: {codeHits} 处中文文本\n"
                   + $"不重复字符数: {charset.Count}\n"
                   + $"输出文件: {outputPath}\n\n"
                   + $"── TMP Font Asset Creator 设置建议 ──\n"
                   + $"Font Size: 32~36（不要超过 42）\n"
                   + $"Atlas Resolution: 2048×2048\n"
                   + $"Character Set: Custom Characters\n"
                   + $"把 charset.txt 全部内容粘贴进去\n"
                   + $"预计生成字体: 1.5~3MB";

        EditorUtility.DisplayDialog("提取词库字符集", msg, "确定");
        Debug.Log($"[ExtractLexiconCharset] {msg}\n来源：\n{sources}");
    }

    [MenuItem(MenuPath, true)]
    public static bool Validate() => !EditorApplication.isPlaying;

    // ── helpers ──

    private static void AddChars(HashSet<char> set, string chars)
    {
        foreach (char c in chars) set.Add(c);
    }

    private static void CollectFromTable(string typeName, string fieldName,
        ref HashSet<char> charset, ref int total, StringBuilder log)
    {
        string path = Path.Combine(Application.dataPath, "Resources", "Data", "table_fb", typeName + ".bytes");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ExtractLexiconCharset] 跳过: 未找到 {path}");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        var ftable = new Table();
        var buffer = new ByteBuffer(data);
        ftable.bb_pos = 0;
        ftable.bb = buffer;

        int length = ftable.__vector_len(0);
        int vec0 = ftable.__vector(0);
        int rows = 0;
        int chars = 0;

        for (int i = 0; i < length; i++)
        {
            int rowPos = ftable.__indirect(vec0 + i * 4);

            // 用反射取字段值（Editor 环境没 IL2CPP 裁剪，安全）
            string text = GetFieldValue(typeName, fieldName, rowPos, ftable.bb);
            if (string.IsNullOrEmpty(text)) continue;

            rows++;
            foreach (char c in text)
            {
                if (c > 127)
                {
                    if (charset.Add(c)) chars++;
                }
            }
        }

        total += rows;
        log.AppendLine($"  {typeName}.{fieldName}: {rows} 有效行, +{chars} 新字符");
    }

    private static string GetFieldValue(string typeName, string fieldName, int rowPos, ByteBuffer bb)
    {
        // 通过 ProtoTable 类型的反射拿属性值
        var asm = typeof(LexiconTable).Assembly;
        var type = asm.GetType("ProtoTable." + typeName);
        if (type == null) return null;

        var obj = (IFlatbufferObject)System.Activator.CreateInstance(type);
        obj.__init(rowPos, bb);
        var prop = type.GetProperty(fieldName);
        if (prop == null) return null;

        try { return prop.GetValue(obj) as string; }
        catch { return null; }
    }

    private static int CollectFromCodeAndPrefabs(ref HashSet<char> charset)
    {
        int hits = 0;
        var chineseRe = new Regex(@"[一-鿿㐀-䶿豈-﫿]+", RegexOptions.Compiled);
        // .asset / .prefab 中 Unity YAML 把中文转成 \uXXXX，需单独解析
        var unicodeEscapeRe = new Regex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

        // 扫描 .cs、.prefab、.unity、.asset
        string[] dirs = { "Assets/Script", "Assets/Scenes", "Assets/Prefab", "Assets/Res/Prefabs", "Assets/ScriptableObject", "Assets/Resources" };
        foreach (string dir in dirs)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", dir);
            if (!Directory.Exists(fullPath)) continue;

            var files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext != ".cs" && ext != ".prefab" && ext != ".unity" && ext != ".asset") continue;

                // .prefab / .unity 太长，只扫前 500K
                string content;
                try
                {
                    if (ext != ".cs")
                    {
                        var info = new FileInfo(file);
                        if (info.Length > 500_000) continue; // 跳过大文件
                        content = File.ReadAllText(file, Encoding.UTF8);
                    }
                    else
                    {
                        content = File.ReadAllText(file, Encoding.UTF8);
                    }
                }
                catch { continue; }

                // 1. 匹配直接写入的汉字（.cs 文件）
                var matches = chineseRe.Matches(content);
                foreach (Match m in matches)
                {
                    foreach (char c in m.Value)
                    {
                        if (charset.Add(c)) hits++;
                    }
                }

                // 2. 匹配 YAML Unicode 转义序列 \uXXXX（.asset / .prefab）
                if (ext == ".asset" || ext == ".prefab" || ext == ".unity")
                {
                    var escMatches = unicodeEscapeRe.Matches(content);
                    foreach (Match m in escMatches)
                    {
                        if (m.Groups.Count < 2) continue;
                        int code = int.Parse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                        if (code > 127) // 只收集非 ASCII（中文字符都在此范围内）
                        {
                            char c = (char)code;
                            if (charset.Add(c)) hits++;
                        }
                    }
                }
            }
        }

        return hits;
    }
}
