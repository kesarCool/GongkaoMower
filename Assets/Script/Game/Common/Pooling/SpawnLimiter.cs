using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局生成限制器：管控各类对象的同时存在上限 + 生成节流（分帧）
/// </summary>
public class SpawnLimiter : MonoBehaviour
{
    [System.Serializable]
    public class LimitConfig
    {
        [Tooltip("对象类型标识（如 Bullet、Enemy、EnergyPickup）")]
        public string key;

        [Tooltip("该类型同时存在的最大数量，超过则不再生成或强制回收最旧的")]
        public int maxAlive = 100;

        [Tooltip("是否允许超限时强制回收最旧的对象来腾位置")]
        public bool recycleOldest = true;

        [Tooltip("每帧最多生成数量（0表示不限制，建议波次生成用5-10防止卡顿）")]
        public int spawnPerFrame = 0;

        [HideInInspector] public int currentAlive;
        [HideInInspector] public int spawnThisFrame;
    }

    [Header("上限配置")]
    public LimitConfig[] configs;

    private Dictionary<string, LimitConfig> _map;
    private Dictionary<string, Queue<GameObject>> _aliveObjects;

    public static SpawnLimiter Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _map = new Dictionary<string, LimitConfig>();
        _aliveObjects = new Dictionary<string, Queue<GameObject>>();

        foreach (var c in configs)
        {
            if (c == null || string.IsNullOrEmpty(c.key)) continue;
            _map[c.key] = c;
            _aliveObjects[c.key] = new Queue<GameObject>();
        }
    }

    private void LateUpdate()
    {
        // 每帧重置计数器
        foreach (var c in configs)
        {
            if (c != null) c.spawnThisFrame = 0;
        }
    }

    /// <summary>
    /// 检查是否可以生成该类型对象
    /// </summary>
    public bool CanSpawn(string key, out LimitConfig config)
    {
        config = null;
        if (string.IsNullOrEmpty(key)) return true;
        if (!_map.TryGetValue(key, out config)) return true;

        // 检查本帧节流
        if (config.spawnPerFrame > 0 && config.spawnThisFrame >= config.spawnPerFrame)
            return false;

        // 检查上限
        if (config.currentAlive >= config.maxAlive)
        {
            if (config.recycleOldest && _aliveObjects.TryGetValue(key, out var queue) && queue.Count > 0)
            {
                // 强制回收最旧的对象
                var oldest = queue.Dequeue();
                if (oldest != null)
                    GameObjectPool.Release(oldest);
                config.currentAlive = Mathf.Max(0, config.currentAlive - 1);
                return true;
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// 通知生成成功，加入存活队列
    /// </summary>
    public void RegisterSpawned(string key, GameObject obj)
    {
        if (string.IsNullOrEmpty(key) || obj == null) return;
        if (!_map.TryGetValue(key, out var config)) return;

        config.currentAlive++;
        config.spawnThisFrame++;

        if (_aliveObjects.TryGetValue(key, out var queue))
            queue.Enqueue(obj);
    }

    /// <summary>
    /// 通知对象已回收/销毁，从存活计数移除
    /// </summary>
    public void Unregister(string key, GameObject obj)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_map.TryGetValue(key, out var config)) return;

        config.currentAlive = Mathf.Max(0, config.currentAlive - 1);
    }

    /// <summary>
    /// 清理某类型的所有存活记录（场景切换时调用）
    /// </summary>
    public void Clear(string key)
    {
        if (_map.TryGetValue(key, out var c)) c.currentAlive = 0;
        if (_aliveObjects.TryGetValue(key, out var q)) q.Clear();
    }
}
