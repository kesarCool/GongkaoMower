using UnityEngine;

/// <summary>
/// Boss 波次竞技场锁定：Boss 出场时在四周生成围墙，玩家无法离开。
/// Boss 被击杀时拆除围墙。
/// 不干涉镜头、不干涉摇杆，仅物理阻拦。
/// </summary>
[DisallowMultipleComponent]
public class BossArenaLock : MonoBehaviour
{
    [Header("竞技场大小")]
    public float arenaHalfWidth = 6f;
    public float arenaHalfHeight = 4.5f;

    [Header("围墙")]
    [Tooltip("挂 SpriteRenderer 的墙 prefab（留空则用纯色方块）")]
    public GameObject wallPrefab;
    [Tooltip("墙的厚度")]
    public float wallThickness = 0.5f;

    private EnemyBase _currentBoss;
    private GameObject _wallParent;
    private bool _locked;
    private Vector2 _arenaCenter;

    /// <summary>竞技场是否已锁定（围墙存在）。</summary>
    public bool IsLocked => _locked;

    /// <summary>场景中查找活跃的 BossArenaLock（避免每帧 FindObjectOfType）。</summary>
    public static BossArenaLock FindInScene()
    {
        return FindObjectOfType<BossArenaLock>();
    }

    /// <summary>将世界坐标 clamp 到竞技场内（留 0.5 单位边距）。</summary>
    public Vector2 ClampInside(Vector2 worldPos)
    {
        float pad = 0.8f;
        float l = _arenaCenter.x - arenaHalfWidth + pad;
        float r = _arenaCenter.x + arenaHalfWidth - pad;
        float b = _arenaCenter.y - arenaHalfHeight + pad;
        float t = _arenaCenter.y + arenaHalfHeight - pad;
        return new Vector2(
            Mathf.Clamp(worldPos.x, l, r),
            Mathf.Clamp(worldPos.y, b, t));
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged, owner: this);
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        UnlockArena();
    }

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        if (_locked) return;
        if (e.enemy == null) return;
        if (!e.enemy.TryGetComponent<LastWaveBossMarker>(out _)) return;

        _currentBoss = e.enemy;

        // 以角色当前位置为竞技场中心
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _arenaCenter = player != null ? (Vector2)player.transform.position : e.worldPosition;

        LockArena();
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (!_locked || _currentBoss != e.enemy) return;
        _currentBoss = null;
        UnlockArena();
    }

    private void LockArena()
    {
        if (_locked) return;
        _locked = true;

        // 把竞技场中心 clamp 到地图边界内，避免内墙超出外墙
        ClampArenaCenterToMapBounds();

        _wallParent = new GameObject("BossArenaWalls");

        float t = _arenaCenter.y + arenaHalfHeight;
        float b = _arenaCenter.y - arenaHalfHeight;
        float l = _arenaCenter.x - arenaHalfWidth;
        float r = _arenaCenter.x + arenaHalfWidth;
        float w = arenaHalfWidth * 2f;
        float h = arenaHalfHeight * 2f;
        float thick = 0.4f;

        BuildWall("WallTop",    new Vector2(_arenaCenter.x, t), new Vector2(w, thick));
        BuildWall("WallBottom", new Vector2(_arenaCenter.x, b), new Vector2(w, thick));
        BuildWall("WallLeft",   new Vector2(l, _arenaCenter.y), new Vector2(thick, h));
        BuildWall("WallRight",  new Vector2(r, _arenaCenter.y), new Vector2(thick, h));

        // Boss 出生在围墙外时，拉入场内（避免 Boss 被自己的竞技场挡在外面）
        PullBossesIntoArena(l, r, b, t);

        GameLog.Info($"[BossArenaLock] 围墙生成 center={_arenaCenter} size={w}x{h}");
    }

    /// <summary>把围墙范围外的 Boss 拽入竞技场，边缘 clamp（保留相对方位）。</summary>
    private static void PullBossesIntoArena(float left, float right, float bottom, float top)
    {
        const float pad = 0.8f;
        float il = left + pad;
        float ir = right - pad;
        float ib = bottom + pad;
        float it = top - pad;

        var markers = FindObjectsOfType<LastWaveBossMarker>();
        foreach (var m in markers)
        {
            Vector2 pos = m.transform.position;
            bool outside = pos.x < left || pos.x > right || pos.y < bottom || pos.y > top;
            if (!outside) continue;

            float x = Mathf.Clamp(pos.x, il, ir);
            float y = Mathf.Clamp(pos.y, ib, it);
            m.transform.position = new Vector3(x, y, m.transform.position.z);

            WallStuckResolver.ResolveTransform(m.transform);

            GameLog.Info($"[BossArenaLock] Boss '{m.name}' ({pos.x:F1},{pos.y:F1}) → 拉入竞技场 ({x:F1},{y:F1})");
        }
    }

    /// <summary>确保竞技场不超出地图 Tilemap 的外墙范围。</summary>
    private void ClampArenaCenterToMapBounds()
    {
        var loader = BattleMapLoader.Instance;
        if (loader == null || loader.GroundTilemap == null) return;

        var tm = loader.GroundTilemap;
        tm.CompressBounds();
        BoundsInt cb = tm.cellBounds;
        Vector3 mapMin = tm.CellToWorld(cb.min);
        Vector3 mapMax = tm.CellToWorld(cb.max);

        // 留出外墙 + 缓冲距离
        const float pad = 0.8f;
        float clampedX = Mathf.Clamp(_arenaCenter.x,
            mapMin.x + arenaHalfWidth + pad,
            mapMax.x - arenaHalfWidth - pad);
        float clampedY = Mathf.Clamp(_arenaCenter.y,
            mapMin.y + arenaHalfHeight + pad,
            mapMax.y - arenaHalfHeight - pad);

        if (!Mathf.Approximately(clampedX, _arenaCenter.x) || !Mathf.Approximately(clampedY, _arenaCenter.y))
        {
            GameLog.Info($"[BossArenaLock] 竞技场中心从 ({_arenaCenter.x:F1},{_arenaCenter.y:F1}) clamp 到 ({clampedX:F1},{clampedY:F1})，防止超出地图");
            _arenaCenter = new Vector2(clampedX, clampedY);
        }
    }

    private void BuildWall(string name, Vector2 pos, Vector2 size)
    {
        GameObject wall;
        if (wallPrefab != null)
        {
            wall = Instantiate(wallPrefab, pos, Quaternion.identity, _wallParent.transform);
            wall.name = name;
            wall.transform.position = pos;
        }
        else
        {
            wall = new GameObject(name);
            wall.transform.SetParent(_wallParent.transform);
            wall.transform.position = pos;
        }

        // ── 碰撞 ──
        var col = wall.GetComponent<BoxCollider2D>();
        if (col == null) col = wall.AddComponent<BoxCollider2D>();
        col.size = size;

        var rb = wall.GetComponent<Rigidbody2D>();
        if (rb == null) rb = wall.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        // 标记（反卡墙检测）
        if (wall.GetComponent<WallMarker>() == null)
            wall.AddComponent<WallMarker>();

        // ── 视觉：Tiled 模式让一张小图沿墙重复铺满 ──
        var sr = wall.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.tileMode = SpriteTileMode.Continuous;
        }
    }

    private void UnlockArena()
    {
        if (!_locked) return;
        _locked = false;

        if (_wallParent != null) Destroy(_wallParent);
        _wallParent = null;

        GameLog.Info("[BossArenaLock] 围墙拆除");
    }
}
