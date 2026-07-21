using UnityEngine;

/// <summary>
/// 弹道突破弹丸：可选择旋转环绕阶段后再直线飞出。
/// <para>orbitDuration &gt; 0 时先生成在角色周围旋转，到时间后沿当前切线方向飞出；orbitDuration = 0 时直接飞出。</para>
/// <para>FullScreenPenetration = true 时：穿透所有敌人不回收，飞出屏幕边界才释放。</para>
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

    /// <summary>全屏穿透模式：穿透所有敌人，仅飞出屏幕时才回收。</summary>
    public bool FullScreenPenetration { get; set; }

    private Camera _cachedCamera;
    private const float ScreenMargin = 1.5f;
    private const float FullScreenMaxLifetime = 10f;

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
        SkillId source, int pierce, bool isCrit, float pierceRate, bool isPenetration,
        float orbitDuration, float orbitRadius, float startAngleDeg, Transform player,
        bool overrideFlyDir = false, Vector2 overrideTarget = default)
    {
        Launch(baseDir, speed, damage, lifetime, source, pierce, isCrit, pierceRate, isPenetration);

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

            // 全屏穿透：每帧检测是否飞出屏幕边界
            if (FullScreenPenetration && IsOffScreen())
                Release();

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

    /// <summary>全屏穿透模式：撞敌不回收。</summary>
    protected override void OnHitEnemy(Collider2D other)
    {
        if (FullScreenPenetration)
            return;
        base.OnHitEnemy(other);
    }

    /// <summary>全屏穿透模式：撞墙不回收（兜底，wallMask=0 时不会触发）。</summary>
    protected override void OnHitWall(Collision2D collision)
    {
        if (FullScreenPenetration)
            return;
        base.OnHitWall(collision);
    }

    /// <summary>是否超出屏幕边界（含余量）。Camera.main 不可用时返回 false 兜底。</summary>
    private bool IsOffScreen()
    {
        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) return false;
        }

        Vector3 vp = _cachedCamera.WorldToViewportPoint(transform.position);
        return vp.x < -ScreenMargin || vp.x > 1f + ScreenMargin
            || vp.y < -ScreenMargin || vp.y > 1f + ScreenMargin;
    }
}
