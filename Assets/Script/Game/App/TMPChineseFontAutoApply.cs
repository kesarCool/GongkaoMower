using TMPro;
using UnityEngine;

/// <summary>
/// 挂到带 <see cref="TMP_Text"/> 的物体上，Awake 时应用 <see cref="BattleChineseFontRuntime"/> 的 msyh SDF。
/// 用 Awake 而非 Start：TMP 在 OnEnable 时渲染文字，Awake 更早，避免渲染瞬间打出字体缺失警告。
/// </summary>
[DisallowMultipleComponent]
public class TMPChineseFontAutoApply : MonoBehaviour
{
    private void Awake()
    {
        BattleChineseFontRuntime.EnsureLoaded();
        var tmp = GetComponent<TMP_Text>();
        if (tmp != null)
            BattleChineseFontRuntime.ApplyToTMP(tmp);
    }
}
