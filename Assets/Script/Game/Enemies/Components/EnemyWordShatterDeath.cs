using UnityEngine;

/// <summary>
/// 已弃用：碎片改由场景中的 <see cref="WordMonsterShatterRuntime"/> 通过 <see cref="EnemyDiedEvent"/> 统一处理（全局预算 + 分帧队列）。
/// 请从预制体移除此组件，只保留 Runtime。
/// </summary>
[DisallowMultipleComponent]
public class EnemyWordShatterDeath : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogWarning(
            "[EnemyWordShatterDeath] 已弃用。请在场景中添加 WordMonsterShatterRuntime，并从本预制体移除此组件，否则会与全局碎字重复或浪费引用。");
        enabled = false;
    }
}
