using UnityEngine;
using UnityEditor;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 编辑器工具：输入礼包码明文，输出 SHA256 哈希 C# 数组。
/// 菜单：Tools → 生成礼包码哈希
/// </summary>
public class GenerateGiftCodeHashes : EditorWindow
{
    private string _inputCodes = "";

    [MenuItem("Tools/生成礼包码哈希")]
    public static void ShowWindow()
    {
        GetWindow<GenerateGiftCodeHashes>("礼包码哈希生成器");
    }

    private void OnGUI()
    {
        GUILayout.Label("输入礼包码（每行一个，英文/数字，大小写不敏感）", EditorStyles.boldLabel);
        _inputCodes = EditorGUILayout.TextArea(_inputCodes, GUILayout.Height(200));

        if (GUILayout.Button("生成哈希数组", GUILayout.Height(30)))
        {
            Generate();
        }
    }

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(_inputCodes))
        {
            EditorUtility.DisplayDialog("提示", "请先输入礼包码。", "确定");
            return;
        }

        var lines = _inputCodes.Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine("// 复制以下代码替换 GiftCodeService.ValidHashes 数组内容");
        sb.AppendLine("private static readonly string[] ValidHashes =");
        sb.AppendLine("{");

        foreach (var line in lines)
        {
            var trimmed = line.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(trimmed)) continue;

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(trimmed));
            var hashStr = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
                hashStr.Append(hash[i].ToString("x2"));

            sb.AppendLine($"    \"{hashStr}\", // {trimmed}");
        }

        sb.AppendLine("};");

        var result = sb.ToString();
        GUIUtility.systemCopyBuffer = result;
        Debug.Log($"<color=cyan>[礼包码哈希生成器]</color> 已生成 {lines.Length} 条哈希并复制到剪贴板：\n{result}");
        EditorUtility.DisplayDialog("完成", "哈希数组已复制到剪贴板，并输出到 Console。\n请粘贴替换 GiftCodeService.cs 中的 ValidHashes 数组。", "确定");
    }
}
