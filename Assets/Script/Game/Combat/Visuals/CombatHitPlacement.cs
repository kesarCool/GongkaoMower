using UnityEngine;

/// <summary>Visual hit placement only. Never changes collision, targeting or damage coordinates.</summary>
public static class CombatHitPlacement
{
    public static Vector2 Resolve(Collider2D target, Vector2 source, out EnemyWordLabel label, out Transform anchor, out Vector3 localPoint)
    {
        label = null;
        anchor = null;
        localPoint = Vector3.zero;
        if (target == null) return source;
        var enemy = target.GetComponentInParent<EnemyBase>();
        label = enemy != null ? enemy.GetComponentInChildren<EnemyWordLabel>() : target.GetComponentInParent<EnemyWordLabel>();
        if (label != null && label.TryGetHitAnchor(source, out anchor, out localPoint))
            return anchor.TransformPoint(localPoint);
        label = null;
        return target.ClosestPoint(source);
    }

    public static Vector2 Contact(Collider2D target, Vector2 source)
    {
        return Resolve(target, source, out _, out _, out _);
    }

    public static Vector2 Center(Transform target)
    {
        if (target == null) return Vector2.zero;
        var enemy = target.GetComponentInParent<EnemyBase>();
        var label = enemy != null ? enemy.GetComponentInChildren<EnemyWordLabel>() : target.GetComponentInChildren<EnemyWordLabel>();
        return label != null && label.TryGetVisualCenter(out var point) ? point : (Vector2)target.position;
    }
}
