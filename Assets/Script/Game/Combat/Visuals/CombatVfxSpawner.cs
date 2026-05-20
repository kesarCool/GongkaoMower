using UnityEngine;

/// <summary>
/// 战斗特效统一入口：从 <see cref="GameObjectPool"/> 借出带 <see cref="PooledCombatVfx"/> 的 Prefab 并播放。
/// 技能 / 投射物 / 伤害反馈等需要「一次性池化 VFX」时只调本类，不要绕过它直接 <see cref="GameObjectPool.Get"/> 播粒子。
/// </summary>
public static class CombatVfxSpawner
{
    public const string SpawnLimiterKey = "CombatVfx";

    public static bool TryPlayPooled(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return false;

        if (SpawnLimiter.Instance != null && !SpawnLimiter.Instance.CanSpawn(SpawnLimiterKey, out _))
            return false;

        GameObject go = GameObjectPool.Get(prefab, position, rotation);
        if (go == null) return false;

        SpawnLimiter.Instance?.RegisterSpawned(SpawnLimiterKey, go);

        PooledCombatVfx vfx = go.GetComponent<PooledCombatVfx>();
        if (vfx == null)
            vfx = go.GetComponentInChildren<PooledCombatVfx>(true);
        if (vfx == null)
            vfx = go.AddComponent<PooledCombatVfx>();

        vfx.Play();
        return true;
    }

    internal static void NotifyReleased(GameObject instance)
    {
        SpawnLimiter.Instance?.Unregister(SpawnLimiterKey, instance);
    }
}
