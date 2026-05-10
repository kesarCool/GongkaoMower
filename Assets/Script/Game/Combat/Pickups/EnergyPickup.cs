using UnityEngine;

/// <summary>
/// EnergyPickup
/// - 挂在“能量掉落物”预制体上
/// - 玩家触碰后给玩家增加能量并销毁自身
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class EnergyPickup : MonoBehaviour
{
    [Tooltip("提供的能量值")]
    public int amount = 1;

    [Tooltip("玩家Tag（默认 Player）")]
    public string playerTag = "Player";

    [Tooltip("存在时间（秒），超时自动销毁，防止场景堆积")]
    public float lifeTime = 15f;

    private void Awake()
    {
        // 建议用 Trigger 碰撞拾取
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private float _alive;

    private void OnEnable()
    {
        _alive = 0f;
    }

    private void Update()
    {
        if (lifeTime <= 0f) return;
        _alive += Time.deltaTime;
        if (_alive >= lifeTime)
        {
            SpawnLimiter.Instance?.Unregister("EnergyPickup", gameObject);
            GameObjectPool.Release(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        PlayerEnergy pe = other.GetComponent<PlayerEnergy>();
        if (pe == null) pe = other.GetComponentInParent<PlayerEnergy>();

        if (pe != null)
        {
            pe.AddEnergy(amount);
            SpawnLimiter.Instance?.Unregister("EnergyPickup", gameObject);
            GameObjectPool.Release(gameObject);
        }
    }
}

