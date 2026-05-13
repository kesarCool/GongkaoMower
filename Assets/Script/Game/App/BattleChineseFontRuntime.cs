using TMPro;
using UnityEngine;

/// <summary>
/// 局内中文字体：enemy prefab 已改为 LiberationSans，避免对 msyh 的序列化引用；进 <c>Game</c> 后由此从 Resources 加载并赋给 <see cref="EnemyWordLabel"/>。
/// 资源路径：<c>Assets/Resources/Fonts/msyh SDF</c> → <see cref="Resources.Load{T}"/>(<see cref="DefaultResourcesPath"/>)。
/// </summary>
/// <remarks>
/// Resources 内资源仍会打进 Player 数据包；缩首包的目标是「壳层 prefab/场景不拖 msyh」与「首场景依赖链不含大字库」。
/// 若需微信首包物理体积再降，应改为分包/Addressables 远程加载后再赋值，而非放在 Resources。
/// </remarks>
public static class BattleChineseFontRuntime
{
    public const string DefaultResourcesPath = "Fonts/msyh SDF";

    private static TMP_FontAsset _loaded;

    public static TMP_FontAsset LoadedFont => _loaded;

    /// <summary>从 Resources 加载一次（与当前场景无关；敌人仅应在 Game 中创建）。</summary>
    public static void EnsureLoaded()
    {
        if (_loaded != null)
            return;

        _loaded = Resources.Load<TMP_FontAsset>(DefaultResourcesPath);
        if (_loaded == null)
        {
            Debug.LogWarning(
                "[BattleChineseFontRuntime] 未找到 Resources 字体 \"" + DefaultResourcesPath +
                "\"。请将 msyh SDF 放在 Assets/Resources/Fonts/ 下。");
        }
    }

    public static void TryApplyTo(EnemyWordLabel label)
    {
        if (label == null || _loaded == null)
            return;

        label.ApplyBattleChineseFont(_loaded);
    }
}
