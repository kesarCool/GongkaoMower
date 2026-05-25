using UnityEngine;

/// <summary>
/// Boss 子弹基类：所有 Boss 技能发射的弹体挂这个或派生类。
/// HomingBullet → 追踪弹，StraightBullet → 直飞弹。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class BossBullet : MonoBehaviour
{
    [Header("通用")]
    public float speed = 7f;
    public float damage = 20f;
    public float lifetime = 5f;
    public LayerMask wallMask;

    [Header("目标")]
    public string targetTag = "Player";

    protected Rigidbody2D _rb;
    protected Vector2 _dir;
    protected float _elapsed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    protected virtual void OnEnable()
    {
        _elapsed = 0f;
    }

    /// <summary>外部（模块）在生成后调，设定初始方向和参数覆盖。</summary>
    public virtual void Launch(Vector2 direction, float overrideSpeed, float overrideDamage, float overrideLifetime)
    {
        _dir = direction.normalized;
        if (overrideSpeed > 0f) speed = overrideSpeed;
        if (overrideDamage > 0f) damage = overrideDamage;
        if (overrideLifetime > 0f) lifetime = overrideLifetime;
    }

    protected virtual void FixedUpdate()
    {
        _elapsed += Time.fixedDeltaTime;
        if (lifetime > 0f && _elapsed >= lifetime)
        {
            Release();
            return;
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
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

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (wallMask.value != 0 && (wallMask.value & (1 << collision.gameObject.layer)) != 0)
            Release();
    }

    protected void Release()
    {
        GameObjectPool.Release(gameObject);
    }
}
