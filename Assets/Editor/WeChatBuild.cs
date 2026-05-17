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

    [MenuItem("Build/微信小游戏 - 一键构建", false, 100)]
    public static void Build()
    {
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

        // WebAssembly linker target
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

        Debug.Log("[WeChatBuild] PlayerSettings 已配置: Gamma, 256MB, NoThreads, Medium Strip");
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
