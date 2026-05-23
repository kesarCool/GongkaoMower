using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;

    [Header("Attack (Legacy)")]
    [Tooltip("勾选后：禁用本脚本内的自动射击（改由 PlayerSkills / 技能系统驱动）")]
    public bool disableLegacyAutoShoot = false;
    
    [Tooltip("攻击间隔（秒）。例如 0.5 表示每 0.5 秒攻击一次")]
    public float attackSpeed = 0.5f;

    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    public string enemyTag = "monster";

    [Header("Visual")]
    [Tooltip("留空则自动查找子节点 Body 的 SpriteRenderer")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    private Camera mainCam;
    private Vector3 targetPos;
    private bool hasTarget;
    private float attackTimer;
    private Rigidbody2D _rb;
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
       /// 自动射击最近敌人
       if (!disableLegacyAutoShoot)
           AutoShootNearestEnemy();
    }

    private void LateUpdate()
    {
        UpdateBodyFacing();
    }


    private void HandleInput()
    {
        // 鼠标点击（PC）
       // if (Input.GetMouseButtonDown(0))
      //  {
      //      SetMoveTarget(Input.mousePosition);
       // }

        // 触摸（手机）
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            SetMoveTarget(Input.GetTouch(0).position);
        }
    }

    private void SetMoveTarget(Vector3 screenPos)
    {
        if (mainCam == null) mainCam = Camera.main;

        Vector3 world = mainCam.ScreenToWorldPoint(screenPos);
        world.z = 0f;                 // 2D
        targetPos = world;
        hasTarget = true;
    }

    private void MoveToTarget()
    {
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

        float horizontal = 0f;
        if (_rb != null && Mathf.Abs(_rb.velocity.x) > FacingEpsilon)
            horizontal = _rb.velocity.x;
        else
            horizontal = transform.position.x - _lastPosition.x;

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

        // 检查上限与节流
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Bullet", out _))
                return;
        }

        GameObject bullet = GameObjectPool.Get(bulletPrefab, transform.position, Quaternion.identity);
        SpawnLimiter.Instance?.RegisterSpawned("Bullet", bullet);

        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null)
        {
            b.SetDirection(dir, bulletSpeed);
        }
        else
        {
            // 如果你忘了挂 Bullet 脚本，仍然让它能飞：给它一个刚体速度
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = dir * bulletSpeed;
        }

        attackTimer = 0f;
    }

    private GameObject FindNearestEnemy()
    {
        return CombatTargetRegistry.FindNearest(enemyTag, transform.position);
    }
}