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

        Debug.Log($"[BossArenaLock] 围墙生成 center={_arenaCenter} size={w}x{h}");
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

        Debug.Log("[BossArenaLock] 围墙拆除");
    }
}
