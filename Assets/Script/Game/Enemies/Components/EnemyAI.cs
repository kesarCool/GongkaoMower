using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 2f;
    public int hp = 2;
    [Tooltip("主角的Tag，默认用 Player")]
    public string playerTag = "Player";

    private Transform player;
    private Rigidbody2D rb;
    private float _nextDamageToPlayerTime;
    private PlayerHealth _cachedPlayerHealth;
    private EnemyBase _cachedEnemyBase;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Make sure enemies are driven by physics so they can collide/push each other.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        FindPlayer();
        _cachedEnemyBase = GetComponent<EnemyBase>();
    }

    private BossBrain _brain;

    private void Awake()
    {
        _brain = GetComponent<BossBrain>();
    }

    private void FixedUpdate()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (_brain != null && _brain.IsBusy)
            return;

        // 帧级卡墙推出：若因碰撞挤压等原因嵌入墙壁，逐步推出
        WallStuckResolver.ResolveTransform(transform);

        Vector2 current = rb.position;
        Vector2 target = player.position;
        Vector2 next = Vector2.MoveTowards(current, target, moveSpeed * Time.fixedDeltaTime);

        // 墙壁回避：目标位置卡墙则拆轴滑动
        if (WallStuckResolver.HasWallOverlap(next))
        {
            Vector2 slideX = new Vector2(next.x, current.y);
            if (!WallStuckResolver.HasWallOverlap(slideX))
                next = slideX;
            else
            {
                Vector2 slideY = new Vector2(current.x, next.y);
                if (!WallStuckResolver.HasWallOverlap(slideY))
                    next = slideY;
            }
        }

        rb.MovePosition(next);
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        player = p != null ? p.transform : null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;
        TryDamagePlayer();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag)) return;
        TryDamagePlayer();
    }

    private void TryDamagePlayer()
    {
        if (Time.time < _nextDamageToPlayerTime) return;
        _nextDamageToPlayerTime = Time.time + 0.35f;

        if (player == null) return;

        // 缓存 PlayerHealth，避免每次碰撞 GetComponentInChildren
        if (_cachedPlayerHealth == null)
            _cachedPlayerHealth = player.GetComponentInChildren<PlayerHealth>(true);
        if (_cachedPlayerHealth == null) return;

        float dmg = _cachedEnemyBase != null ? _cachedEnemyBase.ContactDamage : 1f;
        _cachedPlayerHealth.TakeDamage(Mathf.Max(0.01f, dmg), transform);
    }
}