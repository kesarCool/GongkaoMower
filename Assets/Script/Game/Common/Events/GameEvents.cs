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

/// <summary>选卡流程结束（面板关闭、可继续战斗）时发布，供 HUD 等刷新能量进度。</summary>
public struct CardSelectionEndedEvent { }

/// <summary>关卡进入某一爆兵波次（1-based，供 HUD 显示「波次 n/m」）。</summary>
public struct BattleWaveChangedEvent
{
    public int currentWave;
    public int totalWaves;
    /// <summary>发布事件的 <see cref="SpawnerWaves"/> 实例。</summary>
    public Component spawner;
}

/// <summary>关卡波次刷怪协程已完整跑完（胜利以最后一波 Boss 死亡为准，不要求清场）。</summary>
public struct BattleWavesCompletedEvent
{
    /// <summary>发布事件的 <see cref="SpawnerWaves"/> 实例（便于多刷怪器时区分）。</summary>
    public Component spawner;
}

/// <summary>主角受到伤害（扣血与 <see cref="PlayerHealth.OnDamaged"/> 之后发布）。</summary>
public struct PlayerDamagedEvent
{
    public PlayerHealth playerHealth;
    public float damage;
    public float hpLeft;
    public Transform damageSource;
}

/// <summary>主角血量归零（局内失败入口）。</summary>
public struct PlayerDiedEvent
{
    public PlayerHealth playerHealth;
}

/// <summary>技能施放/命中（供音效等旁路系统订阅；手雷等在落地爆炸时发布，其余技能在释放时发布）。</summary>
public struct SkillCastEvent
{
    public SkillId skillId;
    public Vector3 worldPosition;
}

