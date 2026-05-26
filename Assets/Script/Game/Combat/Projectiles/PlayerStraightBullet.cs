using UnityEngine;

/// <summary>
/// 普通直飞弹：直线飞行，撞怪消失。
/// </summary>
public class PlayerStraightBullet : PlayerBullet
{
    protected override void OnFrameMove()
    {
        _rb.velocity = _dir * speed;
    }
}
