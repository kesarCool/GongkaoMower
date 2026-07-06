using System;
using UnityEngine;

/// <summary>
/// 按配表 <c>ErrorCodeTable</c> 展示错误：<c>CodeDisplay</c> 0 仅 Log，1 Toast，2 对话框。
/// </summary>
public static class GameErrorPresenter
{
    /// <param name="errorCode">与表 <c>ErrorCode</c> 列一致，见 <see cref="GameErrorCodes"/>。</param>
    /// <param name="onDialogClosed">仅 <c>CodeDisplay=2</c> 时，点「知道了」后调用。</param>
    /// <param name="formatArgs">格式化 <c>CodeText</c>（支持 {0}…）。</param>
    public static void Show(string errorCode, Action onDialogClosed = null, params object[] formatArgs)
    {
        if (string.IsNullOrEmpty(errorCode))
            return;

#if USE_FB_TABLE
        if (!ErrorCodeCatalog.TryGet(errorCode, out var row) || row == null)
        {
            Debug.LogWarning($"[GameErrorPresenter] 未配置错误码：{errorCode}");
            return;
        }

        string message = FormatMessage(row.CodeText, formatArgs);
        int mode = row.CodeDisplay;

        if (mode == ErrorCodeCatalog.DisplayNone)
        {
            GameLog.Info($"[ErrorCode:{errorCode}] {message}");
            return;
        }

        GameLog.Info($"[ErrorCode:{errorCode}] display={mode} {message}");

        if (UIManager.Instance == null)
        {
            Debug.LogWarning($"[GameErrorPresenter] UIManager 为空，无法展示 {errorCode}：{message}");
            return;
        }

        switch (mode)
        {
            case ErrorCodeCatalog.DisplayToast:
                UIManager.Instance.ShowToast(message);
                break;
            case ErrorCodeCatalog.DisplayDialog:
                UIManager.Instance.ShowAlert(string.Empty, message, onDialogClosed);
                break;
            default:
                GameLog.Info($"[ErrorCode:{errorCode}] 未知 CodeDisplay={mode}，{message}");
                break;
        }
#else
        Debug.LogWarning($"[GameErrorPresenter] USE_FB_TABLE 未开启，错误码：{errorCode}");
#endif
    }

    private static string FormatMessage(string template, object[] formatArgs)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;
        if (formatArgs == null || formatArgs.Length == 0)
            return template;
        try
        {
            return string.Format(template, formatArgs);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameErrorPresenter] CodeText 格式化失败：{e.Message} | template={template}");
            return template;
        }
    }
}
