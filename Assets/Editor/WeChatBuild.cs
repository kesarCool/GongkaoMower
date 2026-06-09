using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 微信小游戏构建：配置 PlayerSettings + 调用 WX SDK 导出 minigame。
/// 快捷入口：菜单 Build/微信小游戏 - 一键构建。
/// 也可使用 SDK 自带菜单：微信小游戏 / 转换小游戏（提供更多配置选项）。
/// </summary>
public static class WeChatBuild
{
    private const string WebGLBuildDir = "Build/WebGL";
    private const string MiniGameBuildDir = "Build/minigame";

    [MenuItem("Build/微信小游戏 - 一键构建", false, 100)]
    public static void Build()
    {
        // 0. 清理旧构建产物，防止 Windows 文件占用导致 IOException
        CleanBuildOutput();

        // 1. 切换到 WebGL 平台
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[WeChatBuild] 切换到 WebGL 平台失败");
                return;
            }
        }

        // 2. 应用微信兼容的 PlayerSettings
        ApplyWeChatSettings();

        // 3. 确保 USE_FB_TABLE 宏定义
        SetDefineSymbols(BuildTargetGroup.WebGL, "USE_FB_TABLE");

        // 4. 确保输出目录
        if (!Directory.Exists(WebGLBuildDir))
            Directory.CreateDirectory(WebGLBuildDir);

        // 5. 调用 WX SDK 的 DoExport（内部会执行 WebGL 构建 + minigame 导出）
        Debug.Log("[WeChatBuild] 开始微信小游戏构建与导出...");
        ExportViaWXSDK();
    }

    /// <summary>
    /// 通过 WX SDK 的 WXEditorWindow.DoExport 执行构建 + 导出。
    /// 会打开配置窗口让用户确认 AppID 等参数后执行。
    /// </summary>
    private static void ExportViaWXSDK()
    {
        var windowType = System.Type.GetType("WeChatWASM.WXEditorWindow, Assembly-CSharp");
        if (windowType == null)
        {
            Debug.LogError("[WeChatBuild] 未找到微信小游戏 SDK。请确认已导入 WX-WASM-SDK。");
            return;
        }

        // 获取或创建 WXEditorWindow 实例
        var window = EditorWindow.GetWindow(windowType, false, "微信小游戏导出", true);
        if (window == null)
        {
            Debug.LogError("[WeChatBuild] 无法创建 WXEditorWindow。");
            return;
        }

        // 设置输出目录
        var webglDirField = windowType.GetField("webglDir", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (webglDirField != null)
            webglDirField.SetValue(null, WebGLBuildDir);

        // 调用 DoExport(true) 执行 WebGL 构建 + minigame 导出
        var doExportMethod = windowType.GetMethod("DoExport",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (doExportMethod != null)
        {
            doExportMethod.Invoke(window, new object[] { true });
        }
        else
        {
            Debug.LogError("[WeChatBuild] 未找到 WXEditorWindow.DoExport 方法。请使用菜单：微信小游戏 / 转换小游戏");
        }
    }

    /// <summary>
    /// 微信小游戏兼容的 PlayerSettings。
    /// 注意：WX SDK 的 DoExport 会覆盖部分设置（如 template、compressionFormat），
    /// 此处仅设置不影响 SDK 的通用项。
    /// </summary>
    private static void ApplyWeChatSettings()
    {
        // 微信不支持 Linear，必须用 Gamma
        PlayerSettings.colorSpace = ColorSpace.Gamma;

        // WebGL 内存：微信建议 256MB+
        PlayerSettings.WebGL.memorySize = 256;

        // 线程：微信不支持 SharedArrayBuffer
        PlayerSettings.WebGL.threadsSupport = false;

        // IL2CPP 代码裁剪（WX SDK 会覆盖部分设置，这里设基础值）
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);

        // 异常处理：允许 try-catch 正常工作，体积增量可接受
        // 注：某些 Unity 版本中枚举名称为 ExplicitlyThrownOnly，较老版本为 ExplicitlyThrown
        var exceptionSupportType = typeof(WebGLExceptionSupport);
        if (System.Enum.IsDefined(exceptionSupportType, "ExplicitlyThrown"))
        {
            PlayerSettings.WebGL.exceptionSupport = (WebGLExceptionSupport)System.Enum.Parse(exceptionSupportType, "ExplicitlyThrown");
        }
        else if (System.Enum.IsDefined(exceptionSupportType, "ExplicitlyThrownOnly"))
        {
            PlayerSettings.WebGL.exceptionSupport = (WebGLExceptionSupport)System.Enum.Parse(exceptionSupportType, "ExplicitlyThrownOnly");
        }
        else
        {
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        }

        // WebAssembly linker target
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

        Debug.Log("[WeChatBuild] PlayerSettings 已配置: Gamma, 256MB, NoThreads, Medium Strip");
    }

    /// <summary>
    /// 构建前清理旧输出目录。
    /// 先尝试直接删除；被占用时重命名为 .old 后缀再尝试删（让新构建能继续写）。
    /// 常见锁文件元凶：微信开发者工具打开了 minigame 项目、文件资源管理器浏览 Build 目录。
    /// </summary>
    private static void CleanBuildOutput()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string[] dirs = { "Build/minigame", "Build/WebGL" };

        foreach (string dir in dirs)
        {
            string fullPath = Path.Combine(projectRoot, dir);
            if (!Directory.Exists(fullPath)) continue;

            // 先去只读属性
            try
            {
                foreach (string f in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
            }
            catch { /* 个别文件可能锁着，不管，继续 */ }

            if (TryDeleteOrMoveAside(fullPath, out string error))
            {
                Debug.Log($"[WeChatBuild] 已清理: {dir}");
            }
            else
            {
                Debug.LogWarning($"[WeChatBuild] 清理失败({dir}): {error}");
                bool retry = EditorUtility.DisplayDialog("构建警告",
                    $"无法删除 {dir}\n\n{error}\n\n常见原因:\n• 微信开发者工具打开了该项目\n• 文件资源管理器正在浏览 Build 目录\n\n关掉上述窗口后点“重试”,或点“跳过”继续构建(可能仍会失败).",
                    "重试", "跳过");
                if (retry)
                {
                    // 再试一次
                    if (TryDeleteOrMoveAside(fullPath, out error))
                        Debug.Log($"[WeChatBuild] 重试清理成功: {dir}");
                    else
                        Debug.LogWarning($"[WeChatBuild] 重试清理仍失败({dir}): {error}");
                }
            }
        }
    }

    /// <returns>成功返回 true。</returns>
    private static bool TryDeleteOrMoveAside(string fullPath, out string error)
    {
        try
        {
            Directory.Delete(fullPath, true);
            error = null;
            return true;
        }
        catch (System.Exception ex)
        {
            // 文件被占用 → 尝试重命名到 .old，让 SDK 能写新文件
            try
            {
                string oldPath = fullPath.TrimEnd('/', '\\') + ".old";
                if (Directory.Exists(oldPath))
                    Directory.Delete(oldPath, true);
                Directory.Move(fullPath, oldPath);
                Debug.Log($"[WeChatBuild] {fullPath} 被占用，已重命名为 {oldPath}（重启后可手动删除）");
                error = null;
                return true;
            }
            catch (System.Exception ex2)
            {
                error = $"{ex.Message}\n重命名也失败: {ex2.Message}";
                return false;
            }
        }
    }

    private static void SetDefineSymbols(BuildTargetGroup group, string symbols)
    {
        var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        if (current.Contains(symbols)) return;

        var updated = string.IsNullOrEmpty(current) ? symbols : current + ";" + symbols;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, updated);
        Debug.Log($"[WeChatBuild] 已添加宏定义 [{group}]: {symbols}");
    }
}
