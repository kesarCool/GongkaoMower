using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 微信小游戏 <c>wx.loadSubpackage</c> 的占位实现：编辑器/PC 不调用真接口；WebGL 未接插件时默认成功，避免卡死。
/// 接入正式 SDK 后，在 WebGL Player 分支内替换为插件/JSLib 回调，并保持与 game.json 分包名一致。
/// </summary>
public static class WeChatSubpackagePlaceholder
{
    /// <summary>顺序加载多个分包；<paramref name="onProgress"/> 整体 0~1。</summary>
    public static IEnumerator LoadSubpackagesRoutine(
        IReadOnlyList<string> names,
        float editorSimulatedDelaySeconds,
        Action<float> onProgress,
        Action onSuccess,
        Action<string> onFail)
    {
        onProgress?.Invoke(0f);

        if (names == null || names.Count == 0)
        {
            onProgress?.Invoke(1f);
            onSuccess?.Invoke();
            yield break;
        }

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var t0 = (float)i / names.Count;
            var t1 = (float)(i + 1) / names.Count;
            var failed = false;
            string err = null;

            yield return LoadOneSubpackageRoutine(
                name.Trim(),
                editorSimulatedDelaySeconds,
                p => onProgress?.Invoke(Mathf.Lerp(t0, t1, Mathf.Clamp01(p))),
                () => { },
                e =>
                {
                    failed = true;
                    err = e;
                });

            if (failed)
            {
                onFail?.Invoke(err ?? name);
                yield break;
            }

            onProgress?.Invoke(t1);
        }

        onProgress?.Invoke(1f);
        onSuccess?.Invoke();
    }

    private static IEnumerator LoadOneSubpackageRoutine(
        string name,
        float editorSimulatedDelaySeconds,
        Action<float> onProgress,
        Action onSuccess,
        Action<string> onFail)
    {
#if UNITY_EDITOR
        if (editorSimulatedDelaySeconds > 0f)
        {
            float u = 0f;
            while (u < 1f)
            {
                u += Time.unscaledDeltaTime / Mathf.Max(0.01f, editorSimulatedDelaySeconds);
                onProgress?.Invoke(Mathf.Clamp01(u));
                yield return null;
            }
        }
        else
            onProgress?.Invoke(1f);

        GameLog.Info("[WeChatSubpackagePlaceholder] Editor 跳过真实 wx.loadSubpackage，分包名=" + name);
        onSuccess?.Invoke();
        yield break;
#elif UNITY_WEBGL
        // 微信小游戏：分包由 game.json 配置，运行时自动加载（WX-WASM-SDK v2023.02 无需手动 LoadSubpackage）
        onProgress?.Invoke(1f);
        GameLog.Info("[WeChatSubpackagePlaceholder] WebGL 分包由微信运行时自动管理，分包名=" + name);
        onSuccess?.Invoke();
        yield break;
#else
        onProgress?.Invoke(1f);
        onSuccess?.Invoke();
        yield break;
#endif
    }
}
