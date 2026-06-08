using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 围绕 Tilemap 边界自动生成一圈碰撞体围墙。菜单入口：Tools/Tilemap 围一圈墙
/// </summary>
public static class AutoWallAroundTilemap
{
    [MenuItem("Tools/Tilemap 围一圈墙", false, 222)]
    public static void Run()
    {
        var grid = Object.FindObjectOfType<Grid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("缺少 Grid", "Scene 中未找到 Grid。", "确定");
            return;
        }

        var tilemap = grid.GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            EditorUtility.DisplayDialog("缺少 Tilemap", "Grid 下未找到 Tilemap。", "确定");
            return;
        }

        // 读 tilemap 实际范围（CompressBounds 确保精确）
        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        Vector3 min = tilemap.CellToWorld(bounds.min);
        Vector3 max = tilemap.CellToWorld(bounds.max);

        float left   = min.x;
        float right  = max.x;
        float bottom = min.y;
        float top    = max.y;
        float thickness = 0.3f;

        Debug.Log($"[围墙] Tilemap 边界: left={left:F1} right={right:F1} bottom={bottom:F1} top={top:F1}");

        // 找 Wall 父节点
        Transform wallParent = grid.transform.parent != null
            ? grid.transform.parent.Find("Wall")
            : null;

        if (wallParent == null)
        {
            var wallGo = new GameObject("Wall");
            wallGo.transform.SetParent(grid.transform.parent, false);
            wallParent = wallGo.transform;
        }

        // 清空旧围墙
        for (int i = wallParent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(wallParent.GetChild(i).gameObject);

        // 四面墙——紧贴 tilemap 边界内侧，与 IsInsideMap 对齐
        CreateWall(wallParent, "Wall_Left",
            new Vector2(left, (top + bottom) / 2f),
            new Vector2(thickness, top - bottom));

        CreateWall(wallParent, "Wall_Right",
            new Vector2(right, (top + bottom) / 2f),
            new Vector2(thickness, top - bottom));

        CreateWall(wallParent, "Wall_Top",
            new Vector2((left + right) / 2f, top),
            new Vector2(right - left, thickness));

        CreateWall(wallParent, "Wall_Bottom",
            new Vector2((left + right) / 2f, bottom),
            new Vector2(right - left, thickness));

        EditorUtility.DisplayDialog("完成",
            $"已围绕 Tilemap 生成围墙：\n"
            + $"范围: ({left:F1}, {bottom:F1}) → ({right:F1}, {top:F1})\n"
            + $"大小: {right - left:F1} × {top - bottom:F1}",
            "确定");
    }

    [MenuItem("Tools/Tilemap 围一圈墙", true)]
    public static bool Validate() => !EditorApplication.isPlaying;

    private static void CreateWall(Transform parent, string name, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;

        // 静态 Rigidbody2D，可以挡住玩家和怪物
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
}
