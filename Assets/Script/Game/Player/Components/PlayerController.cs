using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;

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
    }

    private void Update()
    {
        HandleInput();
        MoveToTarget();
        if (!disableLegacyAutoShoot)
            AutoShootNearestEnemy();
    }

    private void LateUpdate()
    {
        UpdateBodyFacing();
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
        // 摇杆激活时不执行（避免和摇杆抢控）
        if (_joystick == null)
            _joystick = FindObjectOfType<DynamicJoystick>();
        if (_joystick != null && _joystick.JoystickActive)
        {
            hasTarget = false;
            return;
        }

        if (!hasTarget) return;

        Vector3 cur = transform.position;
        Vector3 next = Vector3.MoveTowards(cur, targetPos, moveSpeed * Time.deltaTime);
        transform.position = next;

        if ((targetPos - transform.position).sqrMagnitude < 0.01f)
            hasTarget = false;
    }

    private void UpdateBodyFacing()
    {
        if (bodyRenderer == null) return;

        // 优先朝向最近怪物（攻击方向）
        GameObject enemy = CombatTargetRegistry.FindNearest(enemyTag, transform.position);
        if (enemy != null)
        {
            float dx = enemy.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > FacingEpsilon)
            {
                bodyRenderer.flipX = dx < 0f;
                _lastPosition = transform.position; // 同步位置记录，避免下一帧移动方向覆盖
                return;
            }
        }

        // 无怪时退回移动方向
        float horizontal = transform.position.x - _lastPosition.x;
        _lastPosition = transform.position;

        if (Mathf.Abs(horizontal) <= FacingEpsilon) return;
        bodyRenderer.flipX = horizontal < 0f;
    }

    private void AutoShootNearestEnemy()
    {
        if (bulletPrefab == null) return;

        attackTimer += Time.deltaTime;
        float interval = Mathf.Max(0.01f, attackSpeed);
        if (attackTimer < interval) return;

        GameObject enemy = FindNearestEnemy();
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
