using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 按 Tag 维护「可被索敌/统计」的活跃 Transform 列表，避免 <see cref="GameObject.FindGameObjectsWithTag"/> 全场景扫描与 GC。
/// 由 <see cref="EnemyBase"/> 等在 OnEnable/OnDisable 注册。
/// </summary>
public static class CombatTargetRegistry
{
    private sealed class TagBucket
    {
        public readonly List<Transform> Active = new List<Transform>(64);
    }

    private static readonly Dictionary<string, TagBucket> Buckets = new Dictionary<string, TagBucket>(4);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InstallSceneHooks()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Buckets.Clear();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != GameObjectPool.BattleSceneNameForPoolClear) return;
        ClearAll();
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (!previous.IsValid()) return;
        if (previous.name != GameObjectPool.BattleSceneNameForPoolClear) return;
        ClearAll();
    }

    public static void ClearAll()
    {
        Buckets.Clear();
    }

    public static void Register(GameObject go)
    {
        if (go == null) return;

        string tag = go.tag;
        if (string.IsNullOrEmpty(tag) || tag == "Untagged") return;

        if (!Buckets.TryGetValue(tag, out TagBucket bucket))
        {
            bucket = new TagBucket();
            Buckets[tag] = bucket;
        }

        Transform tr = go.transform;
        List<Transform> list = bucket.Active;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == tr) return;
        }

        list.Add(tr);
    }

    public static void Unregister(GameObject go)
    {
        if (go == null) return;

        string tag = go.tag;
        if (!Buckets.TryGetValue(tag, out TagBucket bucket)) return;

        RemoveTransform(bucket.Active, go.transform);
    }

    public static GameObject FindNearest(string tag, Vector3 from, float maxRange = 9999f)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        if (!Buckets.TryGetValue(tag, out TagBucket bucket)) return null;

        float maxSq = maxRange * maxRange;
        float bestSq = float.PositiveInfinity;
        Transform best = null;
        List<Transform> list = bucket.Active;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Transform tr = list[i];
            if (!IsAlive(tr))
            {
                RemoveAt(list, i);
                continue;
            }

            float sq = (tr.position - from).sqrMagnitude;
            if (sq > maxSq) continue;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = tr;
            }
        }

        return best != null ? best.gameObject : null;
    }

    public static int CountInRange(string tag, Vector3 from, float maxRange)
    {
        if (string.IsNullOrEmpty(tag)) return 0;
        if (!Buckets.TryGetValue(tag, out TagBucket bucket)) return 0;

        float maxSq = maxRange * maxRange;
        int count = 0;
        List<Transform> list = bucket.Active;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Transform tr = list[i];
            if (!IsAlive(tr))
            {
                RemoveAt(list, i);
                continue;
            }

            if (((Vector2)tr.position - (Vector2)from).sqrMagnitude <= maxSq)
                count++;
        }

        return count;
    }

    public static int CountActive(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return 0;
        if (!Buckets.TryGetValue(tag, out TagBucket bucket)) return 0;

        List<Transform> list = bucket.Active;
        int count = 0;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!IsAlive(list[i]))
            {
                RemoveAt(list, i);
                continue;
            }

            count++;
        }

        return count;
    }

    /// <summary>在范围内均匀随机选一个活跃目标（用于落雷等）。</summary>
    public static bool TryPickRandomInRange(string tag, Vector3 from, float maxRange, out Transform picked)
    {
        picked = null;
        if (string.IsNullOrEmpty(tag)) return false;
        if (!Buckets.TryGetValue(tag, out TagBucket bucket)) return false;

        float maxSq = maxRange * maxRange;
        List<Transform> list = bucket.Active;
        int eligible = 0;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Transform tr = list[i];
            if (!IsAlive(tr))
            {
                RemoveAt(list, i);
                continue;
            }

            if (((Vector2)tr.position - (Vector2)from).sqrMagnitude > maxSq)
                continue;

            eligible++;
            if (Random.Range(0, eligible) == 0)
                picked = tr;
        }

        return picked != null;
    }

    private static bool IsAlive(Transform tr)
    {
        return tr != null && tr.gameObject.activeInHierarchy;
    }

    private static void RemoveTransform(List<Transform> list, Transform tr)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] != tr) continue;
            RemoveAt(list, i);
            return;
        }
    }

    private static void RemoveAt(List<Transform> list, int index)
    {
        int last = list.Count - 1;
        if (index < last)
            list[index] = list[last];
        list.RemoveAt(last);
    }
}
