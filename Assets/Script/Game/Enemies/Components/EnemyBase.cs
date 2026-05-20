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
    /// 由生成器/初始化逻辑调用：从配置数据写入运行时属性
    /// </summary>
    public virtual void InitFromDefinition(EnemyDefinition def)
    {
        if (def == null) return;

        enemyId = def.id;
        enemyName = def.enemyName;

        moveSpeed = def.moveSpeed;
        maxHp = Mathf.Max(1f, def.maxHp);
        hp = maxHp;
        damage = def.damage;
        rewardKillCount = def.rewardKillCount;

        sprite = def.sprite;
        bulletPrefab = def.bulletPrefab;

        ApplyToComponents();
    }

    /// <summary>
    /// 关卡波次表覆盖：attack/maxHp 为 0 表示保留 <see cref="InitFromDefinition"/> 结果。
    /// </summary>
    public virtual void ApplyWaveStatOverrides(int attackOverride, int maxHpOverride)
    {
        if (attackOverride > 0)
            damage = attackOverride;
        if (maxHpOverride > 0)
        {
            maxHp = Mathf.Max(1f, maxHpOverride);
            hp = maxHp;
        }

        ApplyToComponents();

        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.maxHp = maxHp;
            stats.hp = hp;
        }
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

    public virtual void TakeDamage(float amount, SkillId damageSource = SkillId.None)
    {
        if (amount <= 0f) return;
        if (hp <= 0f) return;

        hp -= amount;
        OnDamaged.Invoke(amount);

        if (damageSource != SkillId.None)
            BattleRunMetrics.AddSkillDamage(damageSource, amount);

        EventBus.Publish(new EnemyDamagedEvent
        {
            enemy = this,
            damage = amount,
            worldPosition = transform.position
        });

        if (hp <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        // 通过事件发布“怪物死亡”，由 UI/掉落/统计等模块订阅处理（避免强耦合 FindObjectOfType）
        int killReward = Mathf.Max(1, rewardKillCount);
        EventBus.Publish(new EnemyDiedEvent
        {
            enemy = this,
            enemyId = enemyId,
            rewardKillCount = killReward,
            position = transform.position
        });

        OnDied.Invoke();

        // 注销上限计数
        SpawnLimiter.Instance?.Unregister("Enemy", gameObject);

        // 池化回收或销毁
        var pooled = GetComponent<PooledObject>();
        if (pooled != null && pooled.sourcePrefabId != 0)
            GameObjectPool.Release(gameObject);
        else
            Destroy(gameObject);
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

