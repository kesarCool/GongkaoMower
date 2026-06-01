using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EventBus（全局静态事件总线）
/// - 强类型事件：每种事件用一个 struct/class 作为“事件对象”，字段可任意扩展（支持任意参数个数）
/// - 支持：优先级、一次性订阅（Once）、自动清理已销毁 UnityEngine.Object 的订阅、调试日志
///
/// 用法示例：
/// - 订阅：EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner:this, priority:0);
/// - 发布：EventBus.Publish(new EnemyDiedEvent{ ... });
/// </summary>
public static class EventBus
{
    /// <summary>是否打印发布/订阅日志（建议开发期开启，发布时可关）</summary>
    public static bool DebugLogs = false;

    private interface IHandlerList
    {
        void Publish(object evt);
        void CleanupDead();
        int Count { get; }
    }

    private class Handler<T>
    {
        public Action<T> callback;
        public int priority;
        public bool once;
        public UnityEngine.Object owner; // 用于自动清理（owner 被 Destroy 后回收订阅）
    }

    private class HandlerList<T> : IHandlerList
    {
        private readonly List<Handler<T>> _handlers = new List<Handler<T>>();
        private bool _dirtySort;

        public int Count => _handlers.Count;

        public void Add(Action<T> cb, int priority, bool once, UnityEngine.Object owner)
        {
            _handlers.Add(new Handler<T>
            {
                callback = cb,
                priority = priority,
                once = once,
                owner = owner
            });
            _dirtySort = true;
        }

        public void Remove(Action<T> cb)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].callback == cb)
                    _handlers.RemoveAt(i);
            }
        }

        public void CleanupDead()
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object o = _handlers[i].owner;
                if (o != null && o == null) // Unity 的“伪 null”
                    _handlers.RemoveAt(i);
            }
        }

        public void Publish(object evt)
        {
            CleanupDead();
            if (_dirtySort)
            {
                _handlers.Sort((a, b) => b.priority.CompareTo(a.priority));
                _dirtySort = false;
            }

            // 复制一份索引遍历，避免回调中订阅/退订导致迭代异常
            for (int i = 0; i < _handlers.Count; i++)
            {
                Handler<T> h = _handlers[i];
                if (h.owner != null && h.owner == null) continue;

                h.callback?.Invoke((T)evt);

                if (h.once)
                {
                    _handlers.RemoveAt(i);
                    i--;
                }
            }
        }
    }

    private static readonly Dictionary<Type, IHandlerList> _maps = new Dictionary<Type, IHandlerList>();

    private static HandlerList<T> GetList<T>()
    {
        Type t = typeof(T);
        if (_maps.TryGetValue(t, out IHandlerList list))
            return (HandlerList<T>)list;

        HandlerList<T> created = new HandlerList<T>();
        _maps[t] = created;
        return created;
    }

    /// <summary>
    /// 订阅事件
    /// - owner：传 MonoBehaviour（this）可自动清理销毁后的订阅
    /// - priority：值越大越先收到
    /// - once：收到一次后自动退订
    /// </summary>
    public static void Subscribe<T>(Action<T> callback, UnityEngine.Object owner = null, int priority = 0, bool once = false)
    {
        if (callback == null) return;
        GetList<T>().Add(callback, priority, once, owner);
        if (DebugLogs) Debug.Log($"[EventBus] Subscribe<{typeof(T).Name}> priority={priority} once={once} owner={(owner != null ? owner.name : "null")}");
    }

    public static void Unsubscribe<T>(Action<T> callback)
    {
        if (callback == null) return;
        GetList<T>().Remove(callback);
        if (DebugLogs) Debug.Log($"[EventBus] Unsubscribe<{typeof(T).Name}>");
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    public static void Publish<T>(T evt)
    {
        HandlerList<T> list = GetList<T>();
        if (DebugLogs) Debug.Log($"[EventBus] Publish<{typeof(T).Name}> subscribers={list.Count}");
        list.Publish(evt);
    }

    /// <summary>
    /// 手动清理（可选）。一般不需要调用，发布时会自动 CleanupDead。
    /// </summary>
    public static void Cleanup()
    {
        foreach (var kv in _maps)
            kv.Value.CleanupDead();
    }
}

