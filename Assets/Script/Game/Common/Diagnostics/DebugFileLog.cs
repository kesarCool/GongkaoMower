using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 通用文件日志工具：写入本地文件 + 同步输出到 Unity Console。
/// 全静态，无需挂 MonoBehaviour，任意位置直接调用。
/// WebGL 平台只输出 Debug.Log，不写文件（WebGL 无文件系统）。
///
/// 用法：
///   DebugFileLog.Init("my_debug.log");        // 初始化（可选，不调则自动用 "debug.log"）
///   DebugFileLog.Log("something happened");   // 写入日志
///   DebugFileLog.Clear();                     // 清空当前日志文件
/// </summary>
public static class DebugFileLog
{
	private static string _path;
	private static bool _inited;
	private static readonly object _lock = new object();

	/// <summary>当前日志文件完整路径。</summary>
	public static string LogPath => _path;

	/// <summary>
	/// 初始化日志文件。未调用时首次 Log() 会自动以 "debug.log" 初始化。
	/// </summary>
	/// <param name="fileName">日志文件名（不含路径），如 "roadmap_debug.log"</param>
	/// <param name="clearOnInit">true 时清空旧内容重新开始</param>
	public static void Init(string fileName = "debug.log", bool clearOnInit = true)
	{
#if !UNITY_WEBGL
		lock (_lock)
		{
			_path = Path.Combine(Application.dataPath, "..", fileName);
			if (clearOnInit)
				File.WriteAllText(_path, "");
			_inited = true;
		}
#else
		_inited = true;
#endif
	}

	/// <summary>写入一行日志（自动加时间帧前缀，同时输出到 Debug.Log）。</summary>
	public static void Log(string message)
	{
		if (!_inited)
			Init();

		var line = $"[F{Time.frameCount:D4}] {message}";
		Debug.Log(line);

#if !UNITY_WEBGL
		// WebGL 无文件系统，跳过文件 I/O
		lock (_lock)
		{
			File.AppendAllText(_path, line + Environment.NewLine);
		}
#endif
	}

	/// <summary>清空当前日志文件内容。</summary>
	public static void Clear()
	{
#if !UNITY_WEBGL
		lock (_lock)
		{
			if (!_inited) return;
			File.WriteAllText(_path, "");
		}
#endif
	}

	/// <summary>写入一条不含帧号的原始行（用于分隔线等）。</summary>
	public static void Raw(string text)
	{
		if (!_inited) Init();

		Debug.Log(text);

#if !UNITY_WEBGL
		lock (_lock)
		{
			File.AppendAllText(_path, text + Environment.NewLine);
		}
#endif
	}
}
