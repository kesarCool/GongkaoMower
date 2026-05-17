using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// WebGL 构建脚本：一键切换平台、配置参数、打包。
/// 可在 Editor 菜单执行，也支持批处理模式。
/// </summary>
public static class WebGLBuild
{
    private const string BuildDir = "Build/WebGL";

    [MenuItem("Build/WebGL - 配置并构建")]
    public static void Build()
    {
        // 1. 切换平台
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("切换到 WebGL 平台失败，请检查是否安装了 WebGL 模块");
            return;
        }

        // 2. 配置 PlayerSettings
        ApplyPlayerSettings();

        // 3. 设置宏定义（WebGL 也需要 USE_FB_TABLE）
        SetDefineSymbols(BuildTargetGroup.WebGL, "USE_FB_TABLE");

        // 4. 获取要打包的场景
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Length == 0)
        {
            Debug.LogError("Build Settings 中没有添加场景，请先添加场景到 Build Settings");
            return;
        }

        // 5. 确保输出目录存在
        if (!Directory.Exists(BuildDir))
            Directory.CreateDirectory(BuildDir);

        // 6. 构建
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
            locationPathName = BuildDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"开始 WebGL 构建，场景数: {options.scenes.Length}，输出: {Path.GetFullPath(options.locationPathName)}");
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"<color=green>WebGL 构建成功！</color> 耗时: {summary.totalTime.TotalSeconds:F1}s, 大小: {summary.totalSize / 1024 / 1024}MB");
            Debug.Log($"输出路径: {Path.GetFullPath(BuildDir)}");
        }
        else
        {
            Debug.LogError($"<color=red>WebGL 构建失败！</color> 错误数: {summary.totalErrors}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"  [{step.name}] {msg.content}");
                }
            }
        }
    }

    [MenuItem("Build/WebGL - 仅配置(不构建)")]
    public static void ConfigureOnly()
    {
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("切换到 WebGL 平台失败");
            return;
        }

        ApplyPlayerSettings();
        SetDefineSymbols(BuildTargetGroup.WebGL, "USE_FB_TABLE");
        Debug.Log("WebGL PlayerSettings 已配置，平台已切换");
    }

    private static void ApplyPlayerSettings()
    {
        // 微信小游戏兼容：Gamma 色彩空间
        PlayerSettings.colorSpace = ColorSpace.Gamma;

        // WebGL 内存：微信建议 256MB+
        PlayerSettings.WebGL.memorySize = 256;

        // 模板：Minimal（比 Default 小，减少首包体积）
        PlayerSettings.WebGL.template = "APPLICATION:Minimal";

        // 异常处理：None（减小体积，生产环境推荐）
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        // 压缩格式：Gzip（微信支持 Gzip，也支持 Brotli 但兼容性不如 Gzip）
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;

        // 数据缓存：启用（微信小游戏利用缓存提升加载速度）
        PlayerSettings.WebGL.dataCaching = true;

        // Linker Target：WebAssembly
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

        // 线程：禁用（微信不支持 SharedArrayBuffer）
        PlayerSettings.WebGL.threadsSupport = false;

        // Strip Engine Code：已启用
        PlayerSettings.stripEngineCode = true;

        // Managed Stripping Level：High（减小包体；ProtoTable/FlatBuffers 反射已由 link.xml 保护）
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);

        // WebGL 画布大小（通过模板 index.html 控制更灵活，这里设默认值）
        PlayerSettings.WebGL.analyzeBuildSize = true;

        Debug.Log("PlayerSettings 配置完成: Gamma, 256MB, Minimal Template, Gzip, Medium Strip");
    }

    private static void SetDefineSymbols(BuildTargetGroup group, string symbols)
    {
        var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        if (current.Contains(symbols)) return;

        var updated = string.IsNullOrEmpty(current) ? symbols : current + ";" + symbols;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
        Debug.Log($"已添加宏定义 [{group}]: {symbols}");
    }
}
