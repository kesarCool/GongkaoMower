using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight prefab-based pool for frequent spawn/despawn objects (bullets, pickups).
/// </summary>
public static class GameObjectPool
{
    private class Pool
    {
        public readonly GameObject prefab;
        public readonly Stack<GameObject> inactive = new Stack<GameObject>(64);
        public Transform root;
        public int maxInactive = 256;

        public Pool(GameObject prefab) { this.prefab = prefab; }
    }

    private static readonly Dictionary<int, Pool> Pools = new Dictionary<int, Pool>(64);

    private static Pool GetPool(GameObject prefab)
    {
        if (prefab == null) return null;
        int key = prefab.GetInstanceID();
        if (Pools.TryGetValue(key, out var p)) return p;
        p = new Pool(prefab);
        Pools[key] = p;
        return p;
    }

    private static Transform EnsureRoot(Pool p)
    {
        if (p.root != null) return p.root;
        var go = new GameObject($"[Pool] {p.prefab.name}");
        Object.DontDestroyOnLoad(go);
        p.root = go.transform;
        return p.root;
    }

    public static void Prewarm(GameObject prefab, int count, int maxInactive = 256)
    {
        var p = GetPool(prefab);
        if (p == null) return;
        p.maxInactive = Mathf.Max(0, maxInactive);
        Transform root = EnsureRoot(p);

        int toCreate = Mathf.Max(0, count) - p.inactive.Count;
        for (int i = 0; i < toCreate; i++)
        {
            var obj = Object.Instantiate(prefab, root);
            obj.SetActive(false);
            var tag = obj.GetComponent<PooledObject>();
            if (tag == null) tag = obj.AddComponent<PooledObject>();
            tag.sourcePrefabId = prefab.GetInstanceID();
            p.inactive.Push(obj);
        }
    }

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var p = GetPool(prefab);
        if (p == null) return null;

        GameObject obj = null;
        while (p.inactive.Count > 0 && obj == null)
            obj = p.inactive.Pop();

        if (obj == null)
        {
            obj = Object.Instantiate(prefab);
            var tag = obj.GetComponent<PooledObject>();
            if (tag == null) tag = obj.AddComponent<PooledObject>();
            tag.sourcePrefabId = prefab.GetInstanceID();
        }

        obj.transform.SetParent(parent, false);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        var receivers = obj.GetComponentsInChildren<IPoolReceiver>(true);
        for (int i = 0; i < receivers.Length; i++)
            receivers[i].OnPoolGet();

        return obj;
    }

    public static T Get<T>(T prefabComponent, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        if (prefabComponent == null) return null;
        var go = Get(prefabComponent.gameObject, position, rotation, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    public static void Release(GameObject obj)
    {
        if (obj == null) return;
        var tag = obj.GetComponent<PooledObject>();
        if (tag == null || tag.sourcePrefabId == 0)
        {
            Object.Destroy(obj);
            return;
        }

        if (!Pools.TryGetValue(tag.sourcePrefabId, out var p) || p == null || p.prefab == null)
        {
            Object.Destroy(obj);
            return;
        }

        var receivers = obj.GetComponentsInChildren<IPoolReceiver>(true);
        for (int i = 0; i < receivers.Length; i++)
            receivers[i].OnPoolRelease();

        obj.SetActive(false);
        obj.transform.SetParent(EnsureRoot(p), false);

        if (p.maxInactive > 0 && p.inactive.Count >= p.maxInactive)
        {
            Object.Destroy(obj);
            return;
        }

        p.inactive.Push(obj);
    }
}
