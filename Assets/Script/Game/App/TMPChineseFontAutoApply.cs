using TMPro;
using UnityEngine;

/// <summary>
/// 挂到带 <see cref="TMP_Text"/> 的物体上，Start 时应用 <see cref="BattleChineseFontRuntime"/> 的 msyh SDF。
/// </summary>
[DisallowMultipleComponent]
public class TMPChineseFontAutoApply : MonoBehaviour
{
    private void Start()
    {
        BattleChineseFontRuntime.EnsureLoaded();
        var tmp = GetComponent<TMP_Text>();
        if (tmp != null)
            BattleChineseFontRuntime.ApplyToTMP(tmp);
    }
}
