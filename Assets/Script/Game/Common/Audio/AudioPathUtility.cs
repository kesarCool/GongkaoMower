using System.IO;
using UnityEngine;

/// <summary>Catalog 相对路径 → Editor / StreamingAssets / 微信 src。</summary>
public static class AudioPathUtility
{
    /// <summary>Catalog 路径 → 工程内源文件（Assets/Resources/Audio/...）。</summary>
    public static string ResolveEditorSourcePath(string catalogRelativePath)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return null;

        string path = catalogRelativePath.Trim().Replace('\\', '/');
        if (!path.StartsWith("Audio/", System.StringComparison.OrdinalIgnoreCase))
            return null;

        return Path.Combine(Application.dataPath, "Resources", path).Replace('\\', '/');
    }

    /// <summary>Catalog 路径 → StreamingAssets 或 WebGL 运行时 URL 路径。</summary>
    public static string ResolveStreamingUrl(string catalogRelativePath)
    {
        if (string.IsNullOrWhiteSpace(catalogRelativePath)) return null;

        string path = catalogRelativePath.Trim().Replace('\\', '/');
        string root = Application.streamingAssetsPath?.Replace('\\', '/');
        if (string.IsNullOrEmpty(root)) return null;

        return $"{root}/{path}";
    }

}
