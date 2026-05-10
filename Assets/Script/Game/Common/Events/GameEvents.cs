using UnityEngine;

/// <summary>
/// 全局事件定义（强类型事件对象）
/// - 每个事件就是一个“数据载体”，字段可按需求扩展 => 支持任意参数个数
/// </summary>
public struct EnemyDiedEvent
{
    public EnemyBase enemy;      // 直接给引用，订阅方可判断是否是自己关心的对象
    public int enemyId;
    public int rewardKillCount;
    public Vector3 position;
}

/// <summary>怪物受到伤害（用于飘字、受击音效等；在扣血与 OnDamaged 之后发布）</summary>
public struct EnemyDamagedEvent
{
    public EnemyBase enemy;
    public float damage;
    public Vector3 worldPosition;
}

public struct CardSelectionTriggeredEvent
{
    public Transform player;
    public int triggerCount;
    public int energyLeft;
}

