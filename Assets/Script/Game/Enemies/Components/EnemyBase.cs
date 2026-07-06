using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EnemyBase（怪物运行时基类）
/// - 保存怪物基础属性：ID、名称、速度、血量、伤害、击杀奖励等
/// - 支持从 EnemyDefinition 初始化（由生成器按ID生成后调用）
/// - 提供受伤/死亡事件（OnDamaged/OnDied）
/// - 死亡时自动给 GameLayer 增加击杀数（可关）
///
/// 说明：
/// - 移动仍由 EnemyAI 负责（你选择 5.A 组合方式）
/// - 精英/Boss/远程怪等在此基础上派生
/// </summary>
[DisallowMultipleComponent]
public class EnemyBase : MonoBehaviour
{
    [System.Serializable] public class FloatEvent : UnityEvent<float> { }

    [Header("基础信息（运行时）")]
    [SerializeField] private int enemyId;
    [SerializeField] private string enemyName;

    [Header("数值（运行时）")]
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float maxHp = 10f;
    [SerializeField] protected float hp = 10f;
    [SerializeField] protected float damage = 1f;
    [SerializeField] protected float defense;
    [SerializeField] protected int rewardKillCount = 1;

    [Header("资源引用（运行时，可选）")]
    [SerializeField] protected Sprite sprite;
    [SerializeField] protected GameObject bulletPrefab;

    [Header("事件")]
    [Tooltip("受到伤害时触发（参数为伤害值）")]
    public FloatEvent OnDamaged = new FloatEvent();

    [Tooltip("死亡时触发")]
    public UnityEvent OnDied = new UnityEvent();

    [Header("死亡后处理")]
    [Tooltip("死亡时是否自动给 GameLayer 增加击杀数")]
    public bool addKillToGameLayer = true;

    /// <summary>复活用：为 true 时 Die() 不发布事件、不回收，仅隐藏。</summary>
    [System.NonSerialized] public bool preventPoolDeath;

    private ResistShield _cachedResistShield;
    private bool _resistShieldCached;

    public int EnemyId => enemyId;
    public string EnemyName => enemyName;

    /// <summary>运行时覆盖显示名（如文字怪词条），不影响 prefab 上序列化的默认值存档。</summary>
    public void SetRuntimeDisplayName(string name)
    {
        enemyName = name ?? string.Empty;
    }
    public float MoveSpeed => moveSpeed;
    public float Hp => hp;
    public float MaxHp => maxHp;
    /// <summary>近战/碰撞伤害（关卡表可覆盖 <see cref="ApplyWaveStatOverrides"/>）。</summary>
    public float ContactDamage => damage;
    public GameObject BulletPrefab => bulletPrefab;

    /// <summary>
    /// 从配表写入 id、名字、prefab 引用。数值（攻血速）走 <see cref="ApplyTableStats"/>。
    /// </summary>
    public virtual void InitFromDefinition(EnemyDefinition def)
    {
        if (def == null) return;

        enemyId = def.id;
        enemyName = def.enemyName;
        sprite = def.sprite;
        bulletPrefab = def.bulletPrefab;

        ApplyToComponents();

        // BossBrain 公用 prefab：monsterId 在 Unity Start 时为 0，等 InitFromDefinition 注入
        var brain = GetComponent<BossBrain>();
        if (brain != null) brain.OnEnemyDataReady();
    }

    /// <summary>
    /// 唯一数值入口：所有攻血速防只能从 Excel 表（LevelWave.attack/maxHp/speed/defense）来。
    /// </summary>
    public virtual void ApplyTableStats(int attackRaw, int hpRaw, float moveSpeed, int defenseRaw = 0)
    {
        if (attackRaw > 0)    damage    = attackRaw;
        if (hpRaw > 0)        maxHp     = Mathf.Max(1f, hpRaw);
        if (moveSpeed > 0f)   this.moveSpeed = moveSpeed;
        if (defenseRaw > 0)   this.defense = defenseRaw;
        hp = maxHp;

        SyncComponents();
    }

    /// <summary>
    /// 关卡波次表覆盖（已废弃，用 ApplyTableStats 代替）。保留以兼容旧调用。
    /// </summary>
    public virtual void ApplyWaveStatOverrides(int attackOverride, int maxHpOverride)
    {
        ApplyTableStats(attackOverride, maxHpOverride, 0);
    }

    /// <summary>
    /// ApplyTableStats 完成后同步 EnemyStats 和 EnemyAI。
    /// </summary>
    private void SyncComponents()
    {
        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.maxHp = maxHp;
            stats.hp = hp;
        }

        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.moveSpeed = moveSpeed;
    }

    /// <summary>
    /// 将属性应用到组合组件（例如 EnemyAI 的速度、SpriteRenderer 的外观）
    /// </summary>
    protected virtual void ApplyToComponents()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.moveSpeed = moveSpeed;

        if (sprite != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = sprite;
        }
    }

    public virtual void TakeDamage(float amount, SkillId damageSource = SkillId.None, bool isCrit = false, bool isPenetration = false)
    {
        if (amount <= 0f) return;
        if (hp <= 0f) return;

        // 防御减伤（破防时无视）
        float final = amount;
        if (!isPenetration && defense > 0f)
            final = amount * (100f / (100f + defense));

        // Boss 免伤护盾（按技能伤害类型过滤减伤，内部发布 DamageResistedEvent）
        if (!_resistShieldCached)
        {
            _cachedResistShield = GetComponent<ResistShield>();
            _resistShieldCached = true;
        }
        if (_cachedResistShield != null)
            _cachedResistShield.ApplyResist(damageSource, ref final);

        hp -= final;
        OnDamaged.Invoke(final);

        if (damageSource != SkillId.None)
            BattleRunMetrics.AddSkillDamage(damageSource, final);

        EventBus.Publish(new EnemyDamagedEvent
        {
            enemy = this,
            damage = final,
            worldPosition = transform.position,
            isCrit = isCrit,
            isPenetration = isPenetration,
        });

        if (hp <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        // 先通知 OnDied 订阅者（供 BossBrain 复活拦截）
        OnDied.Invoke();

        // 复活拦截：BossBrain 在 OnDied 中设 preventPoolDeath=true
        if (preventPoolDeath)
        {
            GameLog.Info($"[CardTrace] EnemyBase.Die: preventPoolDeath=true on {name}, HideForRevive (NO EnemyDiedEvent)");
            HideForRevive();
            return;
        }

        bool hasBossMarker = GetComponent<LastWaveBossMarker>() != null;
        if (hasBossMarker)
            GameLog.Info($"[CardTrace] EnemyBase.Die: publishing EnemyDiedEvent for BOSS {name}");

        // 通过事件发布"怪物死亡"，由 UI/掉落/统计等模块订阅处理
        int killReward = Mathf.Max(1, rewardKillCount);
        EventBus.Publish(new EnemyDiedEvent
        {
            enemy = this,
            enemyId = enemyId,
            rewardKillCount = killReward,
            position = transform.position
        });

            // 注销上限计数
            if (SpawnLimiter.Instance != null)
                SpawnLimiter.Instance.Unregister("Enemy", gameObject);

        // 池化回收或销毁
        var pooled = GetComponent<PooledObject>();
        if (pooled != null && pooled.sourcePrefabId != 0)
            GameObjectPool.Release(gameObject);
        else
            Destroy(gameObject);
    }

    /// <summary>复活流程的第一步：隐藏渲染与碰撞，不发布死亡事件。</summary>
    public void HideForRevive()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) col = GetComponentInChildren<Collider2D>();
        if (col != null) col.enabled = false;

        var sprites = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in sprites) sr.enabled = false;

        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in renderers) mr.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;
    }

    /// <summary>复活流程的最后一步：恢复渲染与碰撞。</summary>
    public void ShowFromRevive()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) col = GetComponentInChildren<Collider2D>();
        if (col != null) col.enabled = true;

        var sprites = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in sprites) sr.enabled = true;

        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in renderers) mr.enabled = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true;
    }

    /// <summary>
    /// 由对象池在取出时调用：重置怪物状态（类似复活）
    /// </summary>
    public virtual void ResetForPool()
    {
        hp = maxHp;
        ApplyToComponents();
    }

    private void OnEnable()
    {
        if (hp <= 0f) ResetForPool();
        CombatTargetRegistry.Register(gameObject);
    }

    private void OnDisable()
    {
        CombatTargetRegistry.Unregister(gameObject);
    }
}

