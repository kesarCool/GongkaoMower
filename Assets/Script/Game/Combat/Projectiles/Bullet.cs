using UnityEngine;

/// <summary>[Obsolete] 请迁移到 PlayerStraightBullet。保留此文件仅用于旧 Prefab 兼容。</summary>
[System.Obsolete]
public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 5f;
    public string enemyTag = "monster";

    [SerializeField]
    [Tooltip("Prefab 默认伤害；技能子弹在发射时由 ApplySkillShot 覆盖")]
    private float damage = 1f;

    [SerializeField]
    private SkillId damageSourceSkillId = SkillId.None;

    private Vector2 direction = Vector2.right;
    private bool hit;
    private Collider2D col;
    private float _alive;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        _alive = 0f;
        hit = false;
        if (col != null) col.enabled = true;
    }

    public void SetDirection(Vector2 dir, float overrideSpeed = -1f)
    {
        if (dir.sqrMagnitude > 0.0001f) direction = dir.normalized;
        if (overrideSpeed > 0f) speed = overrideSpeed;
    }

    /// <summary>技能发射时写入伤害与统计来源（覆盖本 Prefab 上的 damage）。</summary>
    public void ApplySkillShot(SkillId skillId, float skillDamage)
    {
        damageSourceSkillId = skillId;
        damage = Mathf.Max(0.01f, skillDamage);
    }

    private void Update()
    {
        _alive += Time.deltaTime;
        if (lifeTime > 0f && _alive >= lifeTime)
        {
            SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
            GameObjectPool.Release(gameObject);
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hit) return;

        if (other.CompareTag(enemyTag))
        {
            hit = true;
            if (col != null) col.enabled = false;

            EnemyBase eb = other.GetComponent<EnemyBase>();
            if (eb == null) eb = other.GetComponentInParent<EnemyBase>();
            if (eb != null)
            {
                eb.TakeDamage(damage, damageSourceSkillId);
                SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
                GameObjectPool.Release(gameObject);
                return;
            }

            EnemyAI ai = other.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.hp -= 1;
                if (ai.hp <= 0)
                {
                    var pooled = other.GetComponent<PooledObject>();
                    if (pooled != null && pooled.sourcePrefabId != 0)
                        GameObjectPool.Release(other.gameObject);
                    else
                        Destroy(other.gameObject);
                }
            }

            SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
            GameObjectPool.Release(gameObject);
        }
    }
}
