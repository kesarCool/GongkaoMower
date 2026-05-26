using UnityEngine;

/// <summary>
/// 玩家子弹基类——与 BossBullet 平行独立。
/// 目标 tag="monster"，碰撞时统计 BattleRunMetrics。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class PlayerBullet : MonoBehaviour
{
    [Header("通用")]
    public float speed = 10f;
    public float damage = 20f;
    public float lifetime = 5f;
    public LayerMask wallMask;

    [Header("技能来源")]
    public SkillId skillSource = SkillId.None;

    protected Rigidbody2D _rb;
    protected Vector2 _dir;
    protected float _elapsed;
    protected bool _fired;

    public string targetTag = "monster";

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    protected virtual void OnEnable()
    {
        _elapsed = 0f;
        _fired = true;
    }

    /// <summary>
    /// 统一发射入口：技能升级后数值通过此方法写入每一发子弹。
    /// </summary>
    public virtual void Launch(Vector2 direction, float overrideSpeed, float overrideDamage,
        float overrideLifetime, SkillId source)
    {
        _dir = direction.normalized;
        if (overrideSpeed > 0f) speed = overrideSpeed;
        if (overrideDamage > 0f) damage = overrideDamage;
        if (overrideLifetime > 0f) lifetime = overrideLifetime;
        skillSource = source;

        float rot = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        _rb.MoveRotation(rot);
    }

    protected virtual void FixedUpdate()
    {
        if (!_fired) return;

        _elapsed += Time.fixedDeltaTime;
        if (lifetime > 0f && _elapsed >= lifetime)
        {
            Release();
            return;
        }

        OnFrameMove();
    }

    protected abstract void OnFrameMove();

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!_fired) return;

        if (other.CompareTag(targetTag))
        {
            EnemyBase eb = other.GetComponent<EnemyBase>();
            if (eb == null) eb = other.GetComponentInParent<EnemyBase>();

            if (eb != null)
            {
                eb.TakeDamage(damage, skillSource);
                BattleRunMetrics.AddSkillDamage(skillSource, damage);
            }

            OnHitEnemy(other);
        }
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (wallMask.value != 0 && (wallMask.value & (1 << collision.gameObject.layer)) != 0)
            OnHitWall(collision);
    }

    /// <summary>撞到怪时调用（子类可重写做穿透/反弹）。默认消失。</summary>
    protected virtual void OnHitEnemy(Collider2D other)
    {
        Release();
    }

    /// <summary>撞到墙时调用（子类可重写做反弹）。默认消失。</summary>
    protected virtual void OnHitWall(Collision2D collision)
    {
        Release();
    }

    protected void Release()
    {
        _fired = false;
        SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
        GameObjectPool.Release(gameObject);
    }
}
