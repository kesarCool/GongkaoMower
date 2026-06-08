using UnityEngine;

/// <summary>
/// 追踪组件：子弹飞行中渐进取最近敌人方向。
/// 执行顺序设为 100，确保在 AutoProjectileBurstBullet 的 FixedUpdate 之后运行。
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class HomingOverride : MonoBehaviour
{
    [Tooltip("最大转向速率（度/秒）。低值=大弧线弯，高值=瞬间锁定")]
    public float turnRate = 150f;
    [Tooltip("追踪感知范围")]
    public float homingRange = 12f;

    private Rigidbody2D _rb;
    private float _speed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private bool _logged;
    private void FixedUpdate()
    {
        if (_speed <= 0f) _speed = _rb.velocity.magnitude;
        if (_speed <= 0f) return;
        var target = CombatTargetRegistry.FindNearest("monster", transform.position, homingRange);
        if (target == null) return;
        if (!_logged) { Debug.Log($"[HomingOverride] 追踪启动：speed={_speed}, turnRate={turnRate}, range={homingRange}"); _logged = true; }

        Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        Vector2 currentDir = _rb.velocity.normalized;
        float maxAngle = turnRate * Time.fixedDeltaTime;
        Vector2 newDir = Vector3.RotateTowards(currentDir, toTarget, maxAngle * Mathf.Deg2Rad, 0f);
        _rb.velocity = newDir * _speed;
    }
}
