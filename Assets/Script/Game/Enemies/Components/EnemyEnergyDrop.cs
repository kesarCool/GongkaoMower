using UnityEngine;

/// <summary>
/// EnemyEnergyDrop
/// - 挂在敌人 prefab 上：监听 EnemyBase.OnDied，在死亡位置生成能量掉落物
/// - 这样掉落逻辑与 EnemyBase 解耦，后续精英/Boss 可用不同掉落组件覆盖
/// </summary>
[DisallowMultipleComponent]
public class EnemyEnergyDrop : MonoBehaviour
{
    [Tooltip("能量掉落物预制体（挂 EnergyPickup）")]
    public EnergyPickup energyPickupPrefab;

    [Tooltip("掉落能量数量（会写入 EnergyPickup.amount）")]
    public int amount = 1;

    [Tooltip("掉落概率（0~1）。例如 1 表示必掉，0.3 表示 30% 概率掉落")]
    [Range(0f, 1f)]
    public float dropChance = 1f;

    [Tooltip("生成位置随机散射半径（世界单位）")]
    public float scatterRadius = 0.25f;

    private EnemyBase _enemyBase;

    private void Awake()
    {
        _enemyBase = GetComponent<EnemyBase>();
        // 通过全局事件订阅死亡事件（会自动清理销毁对象订阅）
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_enemyBase == null) return;
        if (e.enemy != _enemyBase) return;

        if (energyPickupPrefab == null) return;
        if (amount <= 0) return;
        if (dropChance <= 0f) return;
        if (Random.value > dropChance) return;

        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, scatterRadius);
        Vector3 pos = e.position + (Vector3)offset;

        // 检查上限与节流
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("EnergyPickup", out _))
                return;
        }

        EnergyPickup p = GameObjectPool.Get(energyPickupPrefab, pos, Quaternion.identity);
        p.amount = amount;
        SpawnLimiter.Instance?.RegisterSpawned("EnergyPickup", p.gameObject);
    }
}

