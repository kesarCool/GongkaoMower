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
    public bool isCrit;
    public bool isPenetration;
}

/// <summary>怪物受到伤害被抵抗（用于灰色飘字）。在 EnemyDamagedEvent 之后发布。</summary>
public struct DamageResistedEvent
{
    public EnemyBase enemy;
    /// <summary>被抵抗掉的伤害量。</summary>
    public float resistedAmount;
    public Vector3 worldPosition;
    /// <summary>伤害是否被完全抵消（扣血=0 → "免伤"；扣血>0 → "抵抗"）。</summary>
    public bool fullyNegated;
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

/// <summary>中间波 Boss 被击杀，通知 <see cref="SpawnerWaves"/> 推进到下一波。</summary>
public struct BossWaveCompletedEvent
{
    /// <summary>生成该 Boss 的 <see cref="SpawnerWaves"/> 实例。</summary>
    public SpawnerWaves spawner;
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

/// <summary>玩家数据变更（金币/碎片/物品增减）。局外订阅刷新 UI。</summary>
public struct PlayerDataChangedEvent { }

/// <summary>技能施放/命中（供音效等旁路系统订阅；手雷等在落地爆炸时发布，其余技能在释放时发布）。</summary>
public struct SkillCastEvent
{
    public SkillId skillId;
    public Vector3 worldPosition;
}

// ── 成就系统事件 ──

/// <summary>钻石收入（AddDiamond 正数时发布）。</summary>
public struct DiamondEarnedEvent
{
    public int amount;
}

/// <summary>钻石消费（SpendDiamond 成功后发布）。</summary>
public struct DiamondSpentEvent
{
    public int amount;
}

/// <summary>关卡通关（胜利结算时发布）。</summary>
public struct ChapterClearedEvent
{
    public int levelId;
    public bool isFirstClear;
}

/// <summary>角色解锁时发布。</summary>
public struct CharacterUnlockedEvent
{
    public string characterId;
}

/// <summary>技能达到满级时发布。</summary>
public struct SkillMaxLevelReachedEvent
{
    public SkillId skillId;
    public bool isPassive;
}

/// <summary>关卡累计获得星星（delta=本次新增的星星数）。</summary>
public struct StarEarnedEvent
{
    public int levelId;
    public int stars; // 本次 delta（首次通关 1~3，重复挑战改善 1~2）
}

/// <summary>局外英雄升级（每次升级发布一条）。</summary>
public struct HeroLevelUpEvent
{
    public string characterId;
}

/// <summary>局外英雄升阶（每次升阶发布一条）。</summary>
public struct HeroStageUpEvent
{
    public string characterId;
}

/// <summary>金币收入（AddGold 正数时发布）。</summary>
public struct GoldEarnedEvent
{
    public int amount;
}

/// <summary>金币消费（SpendGold 成功后发布）。</summary>
public struct GoldSpentEvent
{
    public int amount;
}

/// <summary>红点角标数据变更。sourceKey 为发生变化的节点路径（如 "battle/achievement"）。</summary>
public struct RedDotChangedEvent
{
    public string sourceKey;
}

