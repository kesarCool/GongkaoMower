using TMPro;
using UnityEngine;

/// <summary>
/// 局内中文字体：仅处理 EnemyWordLabel（TMP_Text）。
/// UI 层使用 TextMeshProUGUI 时由 <see cref="ApplyToHierarchy"/> / <see cref="TMPChineseFontAutoApply"/> 应用 msyh SDF。
/// </summary>
public static class BattleChineseFontRuntime
{
    public const string DefaultResourcesPath = "Fonts/msyh SDF";

    private static TMP_FontAsset _loaded;

    public static TMP_FontAsset LoadedFont => _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded != null)
            return;

        _loaded = Resources.Load<TMP_FontAsset>(DefaultResourcesPath);
        if (_loaded != null)
            GameLog.Info("[BattleChineseFontRuntime] 中文字体已加载: " + _loaded.name);
        else
            Debug.LogWarning("[BattleChineseFontRuntime] 未找到: " + DefaultResourcesPath);
    }

    public static void TryApplyTo(EnemyWordLabel label)
    {
        if (label == null || _loaded == null)
            return;

        label.ApplyBattleChineseFont(_loaded);
    }

    /// <summary>直接给单个 TMP_Text 应用中文字体（Login/Home 等场景的 UI Text 转为 TMP 后使用）。</summary>
    public static void ApplyToTMP(TMP_Text tmp)
    {
        if (tmp == null || _loaded == null) return;

        if (tmp.font != _loaded)
        {
            tmp.font = _loaded;
            tmp.SetAllDirty();
        }
    }

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
            return;

        EnsureLoaded();
        if (_loaded == null)
            return;

        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
            ApplyToTMP(tmps[i]);
    }
}
