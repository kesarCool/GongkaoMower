using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 标记墙壁对象，供 WallStuckResolver / 卡墙检测使用。
/// 挂在 BattleMapLoader 与 BossArenaLock 生成的围墙 GameObject 上。
/// </summary>
[DisallowMultipleComponent]
public class WallMarker : MonoBehaviour
{
    internal static readonly List<WallMarker> Active = new List<WallMarker>(8);
    private Collider2D[] _colliders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();

    private void OnEnable()
    {
        _colliders = GetComponents<Collider2D>();
        if (!Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable() => Active.Remove(this);

    internal bool TryOverlapCircle(Vector2 position, float radius, out Vector2 center)
    {
        center = Vector2.zero;
        float radiusSq = radius * radius;
        for (int i = 0; i < _colliders.Length; i++)
        {
            var col = _colliders[i];
            if (col == null || !col.enabled || (col.isTrigger && !Physics2D.queriesHitTriggers)) continue;
            Bounds bounds = col.bounds;
            // Most enemies are far from the four map walls. Reject those in managed
            // code before asking the collider for its exact (possibly rotated) shape.
            float dx = position.x - Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            float dy = position.y - Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            if (dx * dx + dy * dy > radiusSq) continue;
            var body = col.attachedRigidbody;
            if (body != null && !body.simulated) continue;
            if ((col.ClosestPoint(position) - position).sqrMagnitude > radiusSq) continue;
            center = bounds.center;
            return true;
        }
        return false;
    }
}
