using UnityEngine;

/// <summary>
/// 将敌人标记为 Boss。用于竞技场围墙锁定、Boss 血条、增强死亡碎片效果、胜利/波次推进。
/// 不限于最后一波——中间波 Boss 设 <see cref="isFinalBoss"/> 为 false。
/// </summary>
[DisallowMultipleComponent]
public sealed class LastWaveBossMarker : MonoBehaviour
{
    /// <summary>此 Boss 是否为最后一波 Boss（击杀 = 胜利）。中间波 Boss 设为 false。</summary>
    public bool isFinalBoss = true;

    /// <summary>生成此 Boss 的 <see cref="SpawnerWaves"/>。用于将 <see cref="BossWaveCompletedEvent"/> 路由到正确的刷怪器。</summary>
    public SpawnerWaves spawner;
}
