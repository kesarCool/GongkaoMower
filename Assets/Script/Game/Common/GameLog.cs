using System.IO;
using UnityEngine;

/// <summary>
/// 通用日志文件输出。写 Console 的同时写工程根目录下的 game_debug.log，
/// 方便在编辑器外或打包后定位问题。
/// WebGL 平台只输出 Debug.Log，不写文件（WebGL 无文件系统）。
/// </summary>
public static class GameLog
{
	private static string _path;
	private static readonly object _lock = new object();

	/// <summary>日志文件路径：{工程根目录}/game_debug.log</summary>
	public static string FilePath
	{
		get
		{
			if (_path == null)
			{
				// Application.dataPath = 工程根目录/Assets，取上一级即工程根目录
				string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
				_path = Path.Combine(projectRoot, "game_debug.log");
			}
			return _path;
		}
	}

	public static void Info(string message)
	{
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

#if !UNITY_WEBGL
		// WebGL 无文件系统，跳过文件 I/O，避免 FileStream 构造器触发无法捕获的原生异常
		lock (_lock)
		{
			try { File.AppendAllText(FilePath, line + "\n"); }
			catch (System.Exception) { /* 写文件失败不影响游戏 */ }
		}
#endif
	}

	/// <summary>清空日志文件。</summary>
	public static void Clear()
	{
#if !UNITY_WEBGL
		lock (_lock)
		{
			try { if (File.Exists(FilePath)) File.Delete(FilePath); }
			catch (System.Exception) { }
		}
#endif
	}
}
