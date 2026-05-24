using UnityEngine;

/// <summary>
/// 追踪子弹：飞向玩家，平滑转向，到时间/撞墙自毁。
/// 挂到 Boss 的小刀子弹 Prefab 上。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class HomingBullet : MonoBehaviour
{
    [Header("追踪")]
    public float speed = 4f;
    [Tooltip("每秒最大转向角度")]
    public float turnRate = 120f;
    [Tooltip("追踪持续时间，0=无限")]
    public float lifetime = 5f;
    public float damage = 20f;

    [Header("目标")]
    public string targetTag = "Player";

    [Header("消失条件")]
    [Tooltip("撞墙消失的 Layer Mask")]
    public LayerMask wallMask = -1;

    private Transform _target;
    private Vector2 _currentDir;
    private Rigidbody2D _rb;
    private float _elapsed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.freezeRotation = true;
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        FindTarget();
        _currentDir = _target != null
            ? ((Vector2)_target.position - _rb.position).normalized
            : transform.right;
    }

    private void FixedUpdate()
    {
        _elapsed += Time.fixedDeltaTime;
        if (lifetime > 0f && _elapsed >= lifetime)
        {
            Release();
            return;
        }

        if (_target != null)
        {
            Vector2 desired = ((Vector2)_target.position - _rb.position).normalized;
            float angle = Vector2.SignedAngle(_currentDir, desired);
            float maxTurn = turnRate * Time.fixedDeltaTime;
            float step = Mathf.Clamp(angle, -maxTurn, maxTurn);
            _currentDir = Quaternion.Euler(0f, 0f, step) * _currentDir;
            _currentDir.Normalize();
        }

        _rb.velocity = _currentDir * speed;

        // 更新朝向旋转（让刀尖指向前进方向）
        float rot = Mathf.Atan2(_currentDir.y, _currentDir.x) * Mathf.Rad2Deg;
        _rb.SetRotation(rot);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(targetTag))
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>();
            if (ph == null) ph = other.GetComponentInParent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(damage, transform);

            Release();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 撞墙消失
        if ((wallMask.value & (1 << collision.gameObject.layer)) != 0)
            Release();
    }

    private void Release()
    {
        GameObjectPool.Release(gameObject);
    }

    private void FindTarget()
    {
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        _target = go != null ? go.transform : null;
    }
}
