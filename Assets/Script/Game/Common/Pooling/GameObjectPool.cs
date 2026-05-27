using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    /// <summary>当前由 <see cref="Get"/> 借出、尚未 <see cref="Release"/> 的实例。</summary>
    private static readonly HashSet<GameObject> LeasedObjects = new HashSet<GameObject>();

    /// <summary>全局池根节点（唯一 DontDestroyOnLoad 的容器，所有子池挂在下面）。</summary>
    private static Transform _globalPoolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    /// <summary>与 Build Settings 中局内场景名一致；若项目改名，请在进局前写入此字段。</summary>
    public static string BattleSceneNameForPoolClear = "Game";

    private static void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != BattleSceneNameForPoolClear) return;
        ClearAllPools();
    }

    /// <summary>与 sceneUnloaded 互补：单场景切换时保证在「离开局内」时清池。</summary>
    private static void OnActiveSceneChanged(Scene previousActiveScene, Scene newActiveScene)
    {
        if (!previousActiveScene.IsValid()) return;
        if (previousActiveScene.name != BattleSceneNameForPoolClear) return;
        ClearAllPools();
    }

    /// <summary>
    /// 销毁所有池内未借出实例与池根节点，并清空表；同时销毁 <see cref="LeasedObjects"/> 中仍借出的实例。
    /// 仅使用 <see cref="Object.Destroy"/>：在物理/动画/OnValidate 等回调里禁止 <c>DestroyImmediate</c>。
    /// </summary>
    public static void ClearAllPools()
    {
        if (LeasedObjects.Count > 0)
        {
            var snapshot = new GameObject[LeasedObjects.Count];
            LeasedObjects.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
            {
                GameObject go = snapshot[i];
                if (go != null)
                    Object.Destroy(go);
            }

            LeasedObjects.Clear();
        }

        foreach (var kv in Pools)
        {
            Pool p = kv.Value;
            if (p == null) continue;

            if (p.root != null)
            {
                Object.Destroy(p.root.gameObject);
                p.root = null;
            }

            p.inactive.Clear();
        }

        Pools.Clear();

        if (_globalPoolRoot != null)
        {
            Object.Destroy(_globalPoolRoot.gameObject);
            _globalPoolRoot = null;
        }
    }

    private static Pool GetPool(GameObject prefab)
    {
        if (prefab == null) return null;
        int key = prefab.GetInstanceID();
        if (Pools.TryGetValue(key, out var p)) return p;
        p = new Pool(prefab);
        Pools[key] = p;
        return p;
    }

    private static Transform EnsureGlobalRoot()
    {
        if (_globalPoolRoot != null) return _globalPoolRoot;
        var go = new GameObject("[Pool] GlobalRoot");
        Object.DontDestroyOnLoad(go);
        _globalPoolRoot = go.transform;
        return _globalPoolRoot;
    }

    private static Transform EnsureRoot(Pool p)
    {
        if (p.root != null) return p.root;
        var go = new GameObject(p.prefab.name);
        go.transform.SetParent(EnsureGlobalRoot(), false);
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

        LeasedObjects.Add(obj);
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
        LeasedObjects.Remove(obj);

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
