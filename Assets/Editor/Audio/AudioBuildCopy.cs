using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 方案 1：将 <c>Assets/Res/Audio</c> 拷贝到 StreamingAssets（WebGL/UWR）与 minigame 首包目录（微信 InnerAudio）。
/// </summary>
public static class AudioBuildCopy
{
    private const string ResAudioRoot = "Assets/Res/Audio";
    private const string StreamingAudioRoot = "Assets/StreamingAssets/Audio";
    private const string MinigameAudioRoot = "Build/minigame/minigame/Assets/Audio";

    [MenuItem("Build/Audio/拷贝 Res/Audio → StreamingAssets", false, 250)]
    public static void CopyResAudioToStreamingAssetsMenu()
    {
        CopyResAudioToStreamingAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Build/Audio/拷贝 Res/Audio → Minigame", false, 251)]
    public static void CopyResAudioToMinigameMenu() => CopyResAudioToMinigame();

    public static void CopyResAudioToStreamingAssets()
    {
        string src = Path.GetFullPath(ResAudioRoot);
        string dst = Path.GetFullPath(StreamingAudioRoot);
        CopyDirectory(src, dst);
        Debug.Log("[AudioBuildCopy] StreamingAssets ← " + ResAudioRoot);
    }

    public static void CopyResAudioToMinigame()
    {
        string src = Path.GetFullPath(ResAudioRoot);
        string dst = Path.GetFullPath(MinigameAudioRoot);
        CopyDirectory(src, dst);
        Debug.Log("[AudioBuildCopy] Minigame ← " + ResAudioRoot + " → " + MinigameAudioRoot);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogWarning("[AudioBuildCopy] 源目录不存在：" + sourceDir);
            return;
        }

        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, true);

        CopyRecursive(sourceDir, targetDir);
    }

    private static void CopyRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            if (file.EndsWith(".meta")) continue;
            string name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(targetDir, name), true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string name = Path.GetFileName(dir);
            CopyRecursive(dir, Path.Combine(targetDir, name));
        }
    }
}

public sealed class AudioBuildCopyPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        AudioBuildCopy.CopyResAudioToStreamingAssets();
        AudioBuildCopy.CopyResAudioToMinigame();
    }
}
