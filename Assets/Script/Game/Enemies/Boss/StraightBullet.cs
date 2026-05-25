using UnityEngine;

/// <summary>
/// 直飞子弹：沿初始方向直线飞行，不追踪。
/// 用于刀气爆发等技能。
/// </summary>
public class StraightBullet : BossBullet
{
    protected override void OnEnable()
    {
        base.OnEnable();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        _rb.velocity = _dir * speed;

        // 旋转朝向飞行方向
        float rot = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        _rb.MoveRotation(rot);
    }
}
