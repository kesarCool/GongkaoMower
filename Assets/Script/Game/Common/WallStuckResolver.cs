using UnityEngine;

/// <summary>
/// 卡墙推出工具：检测实体是否与 WallMarker 碰撞体重叠，若重叠则向远离墙壁方向推出。
/// 用于生成后修正 & 运行时帧级推出。
/// </summary>
public static class WallStuckResolver
{
    private const float PushStep = 0.15f;
    private const int MaxAttempts = 40;
    private const float EntityRadius = 0.35f;

    public static Vector2 Resolve(Vector2 position, float radius = EntityRadius)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (!TryGetOverlappingWall(position, radius, out Vector2 wallCenter))
                break;

            Vector2 pushDir = (position - wallCenter).normalized;
            if (pushDir.sqrMagnitude < 0.0001f)
                pushDir = Vector2.up;

            position += pushDir * PushStep;
        }
        return position;
    }

    public static void ResolveTransform(Transform t, float radius = EntityRadius)
    {
        if (t == null) return;
        Vector3 p = t.position;
        Vector2 resolved = Resolve(new Vector2(p.x, p.y), radius);
        if (Vector2.Distance(resolved, new Vector2(p.x, p.y)) > 0.001f)
            t.position = new Vector3(resolved.x, resolved.y, p.z);
    }

    private static bool TryGetOverlappingWall(Vector2 position, float radius, out Vector2 wallCenter)
    {
        wallCenter = Vector2.zero;
        if (radius < 0f) return false;
        // Query only registered walls. A crowded enemy/bullet cluster must neither
        // increase this work nor hide a wall behind a full eight-collider buffer.
        var walls = WallMarker.Active;
        for (int i = walls.Count - 1; i >= 0; i--)
        {
            var wall = walls[i];
            if (wall == null)
            {
                walls.RemoveAt(i);
                continue;
            }
            if (wall.TryOverlapCircle(position, radius, out wallCenter)) return true;
        }
        return false;
    }

    public static bool HasWallOverlap(Vector2 position, float radius = EntityRadius)
    {
        return TryGetOverlappingWall(position, radius, out _);
    }
}
