using UnityEngine;

/// <summary>
/// 卡墙推出工具：检测实体是否与 WallMarker 碰撞体重叠，若重叠则向远离墙壁方向推出。
/// 用于生成后修正 & 运行时帧级推出。
/// </summary>
public static class WallStuckResolver
{
    /// <summary>每次推出的步长（世界单位）。</summary>
    private const float PushStep = 0.15f;

    /// <summary>最大推出尝试次数，防止死循环。</summary>
    private const int MaxAttempts = 40;

    /// <summary>实体半径（用于 OverlapCircle）。</summary>
    private const float EntityRadius = 0.35f;

    private static readonly Collider2D[] _hitBuffer = new Collider2D[8];

    /// <summary>
    /// 检测 position 是否与墙壁重叠，若重叠则反复向远离墙壁方向步进，
    /// 返回修正后的位置。
    /// </summary>
    public static Vector2 Resolve(Vector2 position, float radius = EntityRadius)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (!TryGetOverlappingWall(position, radius, out Vector2 wallCenter))
                break;

            // 远离墙壁方向
            Vector2 pushDir = (position - wallCenter).normalized;
            if (pushDir.sqrMagnitude < 0.0001f)
                pushDir = Vector2.up; // 退化：正中圆心

            position += pushDir * PushStep;
        }

        return position;
    }

    /// <summary>
    /// 直接修正 Transform 位置（适合生成后调用）。
    /// </summary>
    public static void ResolveTransform(Transform t, float radius = EntityRadius)
    {
        if (t == null) return;
        Vector3 p = t.position;
        Vector2 resolved = Resolve(new Vector2(p.x, p.y), radius);
        if (Vector2.Distance(resolved, new Vector2(p.x, p.y)) > 0.001f)
        {
            t.position = new Vector3(resolved.x, resolved.y, p.z);
        }
    }

    /// <summary>
    /// 检测 position 处是否与墙壁重叠。返回 true + 墙壁中心；无重叠返回 false。
    /// </summary>
    private static bool TryGetOverlappingWall(Vector2 position, float radius, out Vector2 wallCenter)
    {
        wallCenter = Vector2.zero;

        int count = Physics2D.OverlapCircleNonAlloc(position, radius, _hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _hitBuffer[i];
            if (col == null) continue;
            if (col.TryGetComponent<WallMarker>(out _))
            {
                wallCenter = col.bounds.center;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检测 position 半径内是否有墙壁（不负责推出，仅查询）。
    /// </summary>
    public static bool HasWallOverlap(Vector2 position, float radius = EntityRadius)
    {
        return TryGetOverlappingWall(position, radius, out _);
    }
}
