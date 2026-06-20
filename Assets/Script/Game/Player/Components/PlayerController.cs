using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;

    [Header("Attack (Legacy)")]
    [Tooltip("勾选后：禁用本脚本内的自动射击（改由 PlayerSkills / 技能系统驱动）")]
    public bool disableLegacyAutoShoot = false;
    public float attackSpeed = 0.5f;
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    public string enemyTag = "monster";

    [Header("Visual")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    private Camera mainCam;
    private Vector3 targetPos;
    private bool hasTarget;
    private float attackTimer;
    private Rigidbody2D _rb;
    private DynamicJoystick _joystick;
    private Vector3 _lastPosition;
    private const float FacingEpsilon = 0.001f;

    private void Awake()
    {
        mainCam = Camera.main;
        targetPos = transform.position;
        hasTarget = false;
        attackTimer = 0f;
        _rb = GetComponent<Rigidbody2D>();
        _lastPosition = transform.position;

        if (bodyRenderer == null)
        {
            Transform body = transform.Find("Body");
            if (body != null)
                bodyRenderer = body.GetComponent<SpriteRenderer>();
        }

        _joystick = FindObjectOfType<DynamicJoystick>();
    }

    private GameObject _cachedNearestEnemy;

    private void Update()
    {
        HandleInput();
        MoveToTarget();

        // 一次查询复用给射击和朝向
        _cachedNearestEnemy = FindNearestEnemy();

        if (!disableLegacyAutoShoot)
            AutoShootNearestEnemyCached();
    }

    private void LateUpdate()
    {
        UpdateBodyFacingCached();
    }

    private void HandleInput()
    {
        // 如果摇杆正在控制，跳过点击移动（避免双系统打架）
        if (_joystick != null && _joystick.JoystickActive)
        {
            hasTarget = false;
            return;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            SetMoveTarget(Input.GetTouch(0).position);
        }
        else if (Input.GetMouseButtonDown(0))
        {
            SetMoveTarget(Input.mousePosition);
        }
    }

    private void SetMoveTarget(Vector3 screenPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 world = mainCam.ScreenToWorldPoint(screenPos);
        world.z = 0f;
        targetPos = world;
        hasTarget = true;
    }

    private void MoveToTarget()
    {
        if (_joystick != null && _joystick.JoystickActive)
        {
            hasTarget = false;
            return;
        }

        if (!hasTarget) return;

        Vector3 cur = transform.position;
        Vector3 next = Vector3.MoveTowards(cur, targetPos, moveSpeed * Time.deltaTime);
        Vector2 next2D = new Vector2(next.x, next.y);

        // 墙壁回避：目标位置卡墙则尝试拆轴滑动，均失败则停在本帧
        if (WallStuckResolver.HasWallOverlap(next2D))
        {
            // 仅 X 轴移动
            Vector2 slideX = new Vector2(next2D.x, cur.y);
            if (!WallStuckResolver.HasWallOverlap(slideX))
            {
                next = new Vector3(slideX.x, slideX.y, cur.z);
            }
            else
            {
                // 仅 Y 轴移动
                Vector2 slideY = new Vector2(cur.x, next2D.y);
                if (!WallStuckResolver.HasWallOverlap(slideY))
                {
                    next = new Vector3(slideY.x, slideY.y, cur.z);
                }
                else
                {
                    // 全部堵死，停止本帧移动
                    next = cur;
                }
            }
        }

        transform.position = next;

        // 兜底：若因物理推动等原因已卡入墙壁，强制推出
        WallStuckResolver.ResolveTransform(transform);

        if ((targetPos - transform.position).sqrMagnitude < 0.01f)
            hasTarget = false;
    }

    private void UpdateBodyFacingCached()
    {
        if (bodyRenderer == null) return;

        GameObject enemy = _cachedNearestEnemy;
        if (enemy != null)
        {
            float dx = enemy.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > FacingEpsilon)
            {
                bodyRenderer.flipX = dx < 0f;
                _lastPosition = transform.position;
                return;
            }
        }

        float horizontal = transform.position.x - _lastPosition.x;
        _lastPosition = transform.position;

        if (Mathf.Abs(horizontal) <= FacingEpsilon) return;
        bodyRenderer.flipX = horizontal < 0f;
    }

    private void AutoShootNearestEnemyCached()
    {
        if (bulletPrefab == null) return;

        attackTimer += Time.deltaTime;
        float interval = Mathf.Max(0.01f, attackSpeed);
        if (attackTimer < interval) return;

        GameObject enemy = _cachedNearestEnemy;
        if (enemy == null) return;

        Vector2 dir = (enemy.transform.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Bullet", out _))
                return;
        }

        GameObject bullet = GameObjectPool.Get(bulletPrefab, transform.position, Quaternion.identity);
        if (bullet == null)
            bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

        SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);

        PlayerBullet pb = bullet.GetComponent<PlayerBullet>();
        if (pb != null)
        {
            pb.Launch(dir, bulletSpeed, 1f, 5f, SkillId.AutoProjectile);
        }
        else
        {
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = dir * bulletSpeed;
        }

        attackTimer = 0f;
    }

    private GameObject FindNearestEnemy()
    {
        return CombatTargetRegistry.FindNearest(enemyTag, transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 玩家不要被怪物推走：碰撞时抵消怪物施加的冲量
        if (collision.rigidbody != null && collision.collider.CompareTag(enemyTag))
        {
            // 把碰撞法线方向的相对速度清零，防止被推
            _rb.velocity = Vector2.zero;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.rigidbody != null && collision.collider.CompareTag(enemyTag))
        {
            _rb.velocity = Vector2.zero;
        }
    }
}
