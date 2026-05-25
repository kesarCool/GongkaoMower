using UnityEngine;

/// <summary>
/// 追踪子弹：继承 BossBullet，在飞行中平滑转向追踪目标。
/// 用 Launch() 设初始方向 + 参数，不要依赖 OnEnable 自动找方向。
/// </summary>
public class HomingBullet : BossBullet
{
    [Tooltip("每秒最大转向角度，0=不追踪变成 StraightBullet")]
    public float turnRate = 120f;

    private Transform _target;

    protected override void OnEnable()
    {
        base.OnEnable();
        _target = null;
    }

    public override void Launch(Vector2 direction, float overrideSpeed, float overrideDamage, float overrideLifetime)
    {
        base.Launch(direction, overrideSpeed, overrideDamage, overrideLifetime);

        // 初始朝向
        _rb.MoveRotation(Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_target == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            _target = go != null ? go.transform : null;
        }

        if (_target != null && turnRate > 0.01f)
        {
            Vector2 desired = ((Vector2)_target.position - _rb.position).normalized;
            float angle = Vector2.SignedAngle(_dir, desired);
            float maxTurn = turnRate * Time.fixedDeltaTime;
            float step = Mathf.Clamp(angle, -maxTurn, maxTurn);
            _dir = Quaternion.Euler(0f, 0f, step) * _dir;
            _dir.Normalize();
        }

        _rb.velocity = _dir * speed;

        float rot = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        _rb.MoveRotation(rot);
    }
}
