using UnityEngine;

/// <summary>Rest on the ground, home directly toward the player, and collect once on contact.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnergyPickup : MonoBehaviour, IPoolReceiver
{
    private enum State { Resting, Sucking, FlyingBack }

    [Header("拾取")]
    [Tooltip("提供的能量值")]
    public int amount = 1;
    public string playerTag = "Player";
    [Tooltip("地面掉落物的存在时间；已开始吸附的能量会完成收集")]
    public float lifeTime = 15f;

    [Header("吸附")]
    [Tooltip("玩家进入此半径后开始吸附飞行")]
    public float suctionRadius = 2f;
    [Tooltip("朝人物收拢速度的加速度，不保留横向惯性")]
    public float suctionAccel = 12f;
    [Tooltip("相对人物的最大收拢速度；人物移动的距离会额外补偿")]
    public float suctionMaxSpeed = 7f;
    public float suctionInitialSpeed = 5f;
    public float groundRestTime = .25f;

    [Header("自动回收")]
    [Tooltip("自动收集时相对人物的收拢速度")]
    public float flyBackMaxSpeed = 22f;
    [Tooltip("中心距离判定；碰到人物碰撞体也会立即收集")]
    public float collectDistance = .1f;

    private float _alive, _stateTimer, _speed;
    private State _state;
    private Transform _player;
    private PlayerEnergy _recipient;
    private Vector2 _lastTargetPosition;
    private bool _released;

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;
    private void OnEnable() => ResetState();
    void IPoolReceiver.OnPoolGet() => ResetState();
    void IPoolReceiver.OnPoolRelease()
    {
        _released = true;
        _player = null;
        _recipient = null;
    }

    private void ResetState()
    {
        _alive = _stateTimer = _speed = 0;
        _state = State.Resting;
        _player = null;
        _recipient = null;
        _lastTargetPosition = Vector2.zero;
        _released = false;
    }

    // Run after player input/movement so the target is this frame's final position.
    private void LateUpdate() => Tick(Time.deltaTime);

    private void Tick(float dt)
    {
        if (_released || dt <= 0) return;
        _alive += dt;
        if (_state == State.Resting)
        {
            if (_alive >= lifeTime) { Release(); return; }
            _stateTimer += dt;
            if (_stateTimer < groundRestTime || !EnsurePlayer()) return;
            if (((Vector2)(_player.position - transform.position)).sqrMagnitude > suctionRadius * suctionRadius) return;
            _state = State.Sucking;
            _speed = Mathf.Clamp(suctionInitialSpeed, .1f, Mathf.Max(.1f, suctionMaxSpeed));
            _lastTargetPosition = _player.position;
        }

        if (!EnsurePlayer())
        {
            _state = State.Resting;
            _stateTimer = _speed = 0;
            return;
        }

        Vector2 position = transform.position;
        Vector2 target = _player.position;
        float radius = Mathf.Max(.01f, collectDistance);
        if ((target - position).sqrMagnitude <= radius * radius) { Collect(); return; }

        if (_state == State.Sucking)
            _speed = Mathf.MoveTowards(_speed, Mathf.Max(.1f, suctionMaxSpeed), Mathf.Max(0, suctionAccel) * dt);
        else
            _speed = Mathf.Max(.1f, flyBackMaxSpeed);

        // Compensate target travel, then close the remaining gap. Even upgraded player
        // movement cannot leave a latched pickup following forever. MoveTowards clamps
        // the last step, so no overshoot, tangential orbit, or low-frame-rate oscillation.
        float targetTravel = Vector2.Distance(target, _lastTargetPosition);
        _lastTargetPosition = target;
        Vector2 next = Vector2.MoveTowards(position, target, targetTravel + _speed * dt);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        if ((target - next).sqrMagnitude <= radius * radius) Collect();
    }

    private bool EnsurePlayer()
    {
        if (_player != null && _recipient != null && _recipient.isActiveAndEnabled) return true;
        var player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null && BindPlayer(player.transform);
    }

    private bool BindPlayer(Transform player)
    {
        var recipient = player.GetComponentInParent<PlayerEnergy>();
        if (recipient == null || !recipient.isActiveAndEnabled || !recipient.CompareTag(playerTag)) return false;
        _recipient = recipient;
        _player = recipient.transform;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_released || !BindPlayer(other.transform)) return;
        // Contact is collection, rather than another outward bounce. This also handles
        // child colliders and different sprite sorting Z values in the 2D world.
        Collect();
    }

    /// <summary>Latch this pickup for the periodic radius collector; repeated calls are harmless.</summary>
    public void ForceCollectBy(Transform player)
    {
        if (_released || !isActiveAndEnabled || player == null) return;
        bool sameTarget = _player != null && (player == _player || player.IsChildOf(_player));
        if (!BindPlayer(player)) return;
        if (_state != State.Resting && sameTarget)
        {
            _state = State.FlyingBack;
            return;
        }
        _state = State.FlyingBack;
        _lastTargetPosition = _player.position;
    }

    private void Collect()
    {
        if (_released || _recipient == null) return;
        var recipient = _recipient;
        int value = amount;
        // Return and latch before notifying UI/level-up subscribers, which may pause
        // or re-enter collection. Each pool lease can credit its energy only once.
        Release();
        recipient.AddEnergy(value);
    }

    private void Release()
    {
        if (_released) return;
        _released = true;
        SpawnLimiter.Instance?.Unregister("EnergyPickup", gameObject);
        GameObjectPool.Release(gameObject);
    }
}
