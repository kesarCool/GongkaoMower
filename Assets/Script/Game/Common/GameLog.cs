using System.IO;
using UnityEngine;

/// <summary>
/// 通用日志文件输出。写 Console 的同时写工程根目录下的 game_debug.log。
/// WebGL 平台只输出 Debug.Log，不写文件（WebGL 无文件系统）。
/// </summary>
public static class GameLog
{
    private static string _path;
    private static bool _pathLogged;
    private static readonly object _lock = new object();

    /// <summary>Info 级别日志开关：编译期决定，非 Editor 默认关闭。</summary>
#if UNITY_EDITOR
    public static bool EnableInfo = true;
#else
    public static bool EnableInfo = false;
#endif

    public static void Info(string message)
    {
        if (!EnableInfo) return;
        Write("INFO", message);
    }

    public static void Warning(string message)
    {
        Write("WARN", message);
        Debug.LogWarning(message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
        Debug.LogError(message);
    }

    private static void Write(string level, string message)
    {
        var line = $"[{System.DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        Debug.Log(line);

        // WebGL 无文件系统，跳过文件 I/O
        if (Application.platform == RuntimePlatform.WebGLPlayer) return;

        lock (_lock)
        {
            try
            {
                if (_path == null)
                {
                    _path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "game_debug.log"));
                }
                if (!_pathLogged)
                {
                    _pathLogged = true;
                    Debug.Log($"[GameLog] 文件路径: {_path}  |  dataPath: {Application.dataPath}  |  platform: {Application.platform}");
                }
                File.AppendAllText(_path, line + "\n");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameLog] 写文件失败: {ex.Message}");
            }
        }
    }

    /// <summary>清空日志文件。</summary>
    public static void Clear()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer) return;
        lock (_lock)
        {
            try { if (_path != null && File.Exists(_path)) File.Delete(_path); }
            catch (System.Exception) { }
        }
    }
}
