using UnityEngine;

/// <summary>
/// 能量掉落物：掉落后静止→玩家靠近吸附飞行→触发收集时先外弹再回收。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnergyPickup : MonoBehaviour, IPoolReceiver
{
    private enum State { Resting, Sucking, Bouncing, FlyingBack }

    [Header("拾取")]
    [Tooltip("提供的能量值")]
    public int amount = 1;

    [Tooltip("玩家Tag")]
    public string playerTag = "Player";

    [Tooltip("存在时间（秒），超时自动回收")]
    public float lifeTime = 15f;

    [Header("吸附")]
    [Tooltip("玩家进入此半径后开始吸附飞行")]
    public float suctionRadius = 2f;

    [Tooltip("吸附加速度")]
    public float suctionAccel = 12f;

    [Tooltip("最大吸附飞行速度")]
    public float suctionMaxSpeed = 7f;

    [Tooltip("吸附初始速度（不为0则进入吸附时立刻朝玩家弹射，避免从零加速追不上跑开的玩家）")]
    public float suctionInitialSpeed = 5f;

    [Tooltip("落到地面后的静止时间")]
    public float groundRestTime = 0.25f;

    [Header("收集反弹")]
    [Tooltip("触发收集时向外弹的初速度（越大弹得越远）")]
    public float bounceSpeed = 8f;

    [Tooltip("反弹持续时间")]
    public float bounceDuration = 0.1f;

    [Tooltip("回收飞行速度（越大回收越快越有'啪'的吸入感）")]
    public float flyBackMaxSpeed = 22f;

    [Tooltip("回收判定距离")]
    public float collectDistance = 0.1f;

    private float _alive;
    private float _stateTimer;
    private State _state;
    private Vector3 _velocity;
    private Transform _player;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // ── Pool lifecycle ───────────────────────────────────

    void IPoolReceiver.OnPoolGet() => ResetState();
    void IPoolReceiver.OnPoolRelease() { }

    private void OnEnable() => ResetState();

    private void ResetState()
    {
        _alive = 0f;
        _stateTimer = 0f;
        _state = State.Resting;
        _velocity = Vector3.zero;
        _player = null;
    }

    // ── Update dispatch ──────────────────────────────────

    private void Update()
    {
        _alive += Time.deltaTime;
        if (_alive >= lifeTime)
        {
            Release();
            return;
        }

        switch (_state)
        {
            case State.Resting:   UpdateResting();   break;
            case State.Sucking:   UpdateSucking();   break;
            case State.Bouncing:  UpdateBouncing();  break;
            case State.FlyingBack: UpdateFlyingBack(); break;
        }
    }

    // ── Resting ──────────────────────────────────────────

    private void UpdateResting()
    {
        _stateTimer += Time.deltaTime;
        if (_stateTimer < groundRestTime)
            return;

        if (!EnsurePlayer())
            return;

        if (Vector3.Distance(transform.position, _player.position) <= suctionRadius)
            EnterSucking();
    }

    // ── Sucking ──────────────────────────────────────────

    private void EnterSucking()
    {
        _state = State.Sucking;
        Vector3 to = _player.position - transform.position;
        float dist = to.magnitude;
        if (dist > 1e-6f)
            _velocity = to / dist * suctionInitialSpeed;
        else
            _velocity = Vector3.zero;
    }

    private void UpdateSucking()
    {
        if (!EnsurePlayer())
        {
            EnterResting();
            return;
        }

        AccelerateToward(_player.position, suctionAccel, suctionMaxSpeed);
        float dist = Vector3.Distance(transform.position, _player.position);
        float step = Mathf.Min(_velocity.magnitude * Time.deltaTime, dist);
        if (step > 0f)
            transform.position += _velocity.normalized * step;

        if (Vector3.Distance(transform.position, _player.position) < 0.35f)
            StartBounce();
    }

    // ── Bouncing ─────────────────────────────────────────

    private void StartBounce()
    {
        Vector3 away = (transform.position - _player.position).normalized;
        if (away.sqrMagnitude < 1e-6f)
            away = Random.insideUnitCircle.normalized;
        _velocity = away * bounceSpeed;
        _state = State.Bouncing;
        _stateTimer = 0f;
    }

    private void UpdateBouncing()
    {
        _stateTimer += Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;

        if (_stateTimer >= bounceDuration)
        {
            _state = State.FlyingBack;
            _velocity = Vector3.zero;
        }
    }

    // ── FlyingBack ───────────────────────────────────────

    private void UpdateFlyingBack()
    {
        if (!EnsurePlayer())
        {
            Release();
            return;
        }

        Vector3 to = _player.position - transform.position;
        float dist = to.magnitude;

        if (dist <= collectDistance)
        {
            Collect(_player);
            return;
        }

        float step = flyBackMaxSpeed * Time.deltaTime;
        transform.position += to / dist * Mathf.Min(step, dist);
    }

    // ── Helpers ──────────────────────────────────────────

    private void EnterResting()
    {
        _state = State.Resting;
        _stateTimer = 0f;
        _velocity = Vector3.zero;
    }

    private void AccelerateToward(Vector3 target, float accel, float maxSpeed)
    {
        Vector3 to = target - transform.position;
        float dist = to.magnitude;
        if (dist < 1e-6f)
            return;
        _velocity += to / dist * (accel * Time.deltaTime);
        float s = _velocity.magnitude;
        if (s > maxSpeed)
            _velocity = _velocity / s * maxSpeed;
    }

    private bool EnsurePlayer()
    {
        if (_player != null)
            return true;
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go != null)
            _player = go.transform;
        return _player != null;
    }

    // ── Collision ────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;
        if (_state == State.Bouncing || _state == State.FlyingBack)
            return;

        _player = other.transform;
        StartBounce();
    }

    /// <summary>强制开始向玩家回收（自动收集用）。</summary>
    public void ForceCollectBy(Transform player)
    {
        if (player == null) return;
        _player = player;
        _state = State.FlyingBack;
        _velocity = Vector3.zero;
    }

    // ── Collect & Release ────────────────────────────────

    private void Collect(Transform player)
    {
        PlayerEnergy pe = player.GetComponent<PlayerEnergy>();
        if (pe == null)
            pe = player.GetComponentInParent<PlayerEnergy>();

        if (pe != null)
            pe.AddEnergy(amount);

        Release();
    }

    private void Release()
    {
        SpawnLimiter.Instance?.Unregister("EnergyPickup", gameObject);
        GameObjectPool.Release(gameObject);
    }
}
