using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Shared player relocation for initial spawn, revival and Boss arena entry.</summary>
public static class BattlePlayerPositioning
{
    public static bool TryGetMapCenter(out Vector2 center)
    {
        center = Vector2.zero;
        var loader = BattleMapLoader.Instance;
        if (loader == null || loader.GroundTilemap == null) return false;

        var ground = loader.GroundTilemap;
        ground.CompressBounds();
        var bounds = ground.cellBounds;
        if (ground.GetUsedTilesCount() == 0) return false;
        center = (ground.CellToWorld(bounds.min) + ground.CellToWorld(bounds.max)) * 0.5f;
        return true;
    }

    /// <summary>Prefer the geometric center; fall back to the nearest clear ground cell.</summary>
    public static bool TryGetSpawnPosition(Tilemap ground, out Vector2 position)
    {
        position = Vector2.zero;
        if (ground == null || ground.GetUsedTilesCount() == 0) return false;
        ground.CompressBounds();
        var bounds = ground.cellBounds;
        Vector2 center = (ground.CellToWorld(bounds.min) + ground.CellToWorld(bounds.max)) * .5f;
        if (IsClearSpawn(ground, center))
        {
            position = center;
            return true;
        }

        float nearest = float.PositiveInfinity;
        foreach (var cell in bounds.allPositionsWithin)
        {
            if (!ground.HasTile(cell)) continue;
            Vector2 candidate = ground.GetCellCenterWorld(cell);
            float distance = (candidate - center).sqrMagnitude;
            if (distance >= nearest || !IsClearSpawn(ground, candidate)) continue;
            nearest = distance;
            position = candidate;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    private static bool IsClearSpawn(Tilemap ground, Vector2 position)
    {
        if (WallStuckResolver.HasWallOverlap(position)) return false;
        // Check the player's footprint as well as its center, including gaps in sparse maps.
        const float radius = .35f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;
            Vector2 sample = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (!ground.HasTile(ground.WorldToCell(sample))) return false;
        }
        return ground.HasTile(ground.WorldToCell(position));
    }

    public static bool TeleportToMapCenter(Transform player)
    {
        if (player == null || !TryGetMapCenter(out Vector2 center)) return false;
        Teleport(player, center);
        return true;
    }

    public static void Teleport(Transform player, Vector2 destination)
    {
        if (player == null) return;
        Vector2 resolved = WallStuckResolver.Resolve(destination);
        player.position = new Vector3(resolved.x, resolved.y, player.position.z);
        if (player.TryGetComponent<Rigidbody2D>(out var body))
        {
            body.position = resolved;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        if (player.TryGetComponent<PlayerController>(out var controller))
            controller.ResetMovementAfterTeleport();
        Physics2D.SyncTransforms();
    }
}
