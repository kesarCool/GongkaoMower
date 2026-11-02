using UnityEngine;

/// <summary>
/// 直飞子弹：沿初始方向直线飞行，不追踪。
/// 用于刀气爆发等技能。
/// </summary>
public class StraightBullet : BossBullet
{
    private BossBladeProjectilePresentation _presentation;
    protected override void OnEnable()
    {
        base.OnEnable();
        _presentation = GetComponent<BossBladeProjectilePresentation>();
    }

    public override void Launch(Vector2 direction, float overrideSpeed, float overrideDamage, float overrideLifetime)
    {
        base.Launch(direction, overrideSpeed, overrideDamage, overrideLifetime);
        if (_presentation != null) _rb.MoveRotation(Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
    }

    protected override void FixedUpdate()
    {
        if (_presentation != null && lifetime > 0f && _elapsed + Time.fixedDeltaTime >= lifetime)
            _presentation.PlayDissolve(_rb.position);
        base.FixedUpdate();
        if (!gameObject.activeInHierarchy) return;
        _rb.velocity = _dir * speed;

        // 旋转朝向飞行方向
        float rot = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        _rb.MoveRotation(rot);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (_presentation != null && other.CompareTag(targetTag))
            _presentation.PlayDissolve(other.ClosestPoint(transform.position));
        base.OnTriggerEnter2D(other);
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (_presentation != null && wallMask.value != 0 && (wallMask.value & (1 << collision.gameObject.layer)) != 0)
            _presentation.PlayDissolve(transform.position);
        base.OnCollisionEnter2D(collision);
    }
}
