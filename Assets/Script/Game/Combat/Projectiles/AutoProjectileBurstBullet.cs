using UnityEngine;

/// <summary>
/// 弹道突破弹丸：可选择旋转环绕阶段后再直线飞出。
/// <para>orbitDuration &gt; 0 时先生成在角色周围旋转，到时间后沿当前切线方向飞出；orbitDuration = 0 时直接飞出。</para>
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class AutoProjectileBurstBullet : PlayerBullet
{
    private float _orbitDuration;
    private float _orbitElapsed;
    private float _orbitRadius;
    private float _startAngle;
    private Transform _player;
    private Collider2D _collider;
    private bool _inOrbit;
    private bool _overrideFlyDir;
    private Vector2 _overrideTarget;

    protected override void OnEnable()
    {
        base.OnEnable();
        _inOrbit = false;
        _orbitElapsed = 0f;
        _overrideFlyDir = false;
        if (_rb != null && !_inOrbit)
            _rb.bodyType = RigidbodyType2D.Dynamic;
    }

    /// <summary>
    /// 爆发发射：在 <paramref name="orbitDuration"/> 秒内绕 <paramref name="player"/> 逆时针旋转，
    /// 之后沿切线方向飞出。orbitDuration=0 时直接飞出。
    /// </summary>
    public void LaunchBurst(Vector2 baseDir, float speed, float damage, float lifetime,
        SkillId source, int pierce, bool isCrit, float pierceRate,
        float orbitDuration, float orbitRadius, float startAngleDeg, Transform player,
        bool overrideFlyDir = false, Vector2 overrideTarget = default)
    {
        Launch(baseDir, speed, damage, lifetime, source, pierce, isCrit, pierceRate);

        _orbitDuration = orbitDuration;
        _orbitRadius = orbitRadius;
        _startAngle = startAngleDeg * Mathf.Deg2Rad;
        _player = player;
        _orbitElapsed = 0f;
        _inOrbit = orbitDuration > 0.001f;
        _overrideFlyDir = overrideFlyDir;
        _overrideTarget = overrideTarget;

        if (_collider == null)
            _collider = GetComponent<Collider2D>();
        if (_collider != null)
            _collider.enabled = !_inOrbit;

        if (_inOrbit)
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.velocity = Vector2.zero;
        }
    }

    protected override void OnFrameMove()
    {
        if (!_inOrbit)
        {
            _rb.velocity = _dir * speed;
            return;
        }

        _orbitElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_orbitElapsed / _orbitDuration);

        // 逆时针旋转：角度递增
        float angle = _startAngle + t * 360f * Mathf.Deg2Rad;
        Vector2 orbitPos = _player != null
            ? (Vector2)_player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _orbitRadius
            : (Vector2)transform.position;

        transform.position = orbitPos;

        // 旋转期间弹丸朝向切线方向（逆时针公转）
        float tangentAngle = angle + 90f * Mathf.Deg2Rad;
        float facing = tangentAngle * Mathf.Rad2Deg;
        _rb.MoveRotation(facing);

        if (t >= 1f)
        {
            EnterFlightPhase(angle);
        }
    }

    private void EnterFlightPhase(float exitAngle)
    {
        _inOrbit = false;

        if (_collider != null)
            _collider.enabled = true;

        if (_overrideFlyDir)
        {
            // 集火：从弹丸当前位置指向目标
            _dir = (_overrideTarget - (Vector2)transform.position).normalized;
        }
        else
        {
            // 切线方向飞出
            _dir = new Vector2(-Mathf.Sin(exitAngle), Mathf.Cos(exitAngle));
        }

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.velocity = _dir * speed;

        float rot = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        _rb.MoveRotation(rot);
    }
}
