using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 5f;
    public string enemyTag = "monster";
    [Tooltip("子弹伤害（优先作用于 EnemyBase）")]
    public float damage = 1f;
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
            if (col != null) col.enabled = false; // prevent multi-hit when enemies overlap

            // 优先走怪物基类（会触发 OnDied、掉落、击杀数等）
            EnemyBase eb = other.GetComponent<EnemyBase>();
            if (eb == null) eb = other.GetComponentInParent<EnemyBase>();
            if (eb != null)
            {
                eb.TakeDamage(Mathf.Max(0.01f, damage));
                SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
                GameObjectPool.Release(gameObject);
                return;
            }

            // 兼容旧逻辑：如果敌人没有 EnemyBase，则使用 EnemyAI.hp
            EnemyAI ai = other.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.hp -= 1;
                Debug.Log("Bullet hit enemy with hp: " + ai.hp);
                if (ai.hp <= 0)
                {
                    // 池化回收或销毁（兼容旧版非池化怪物）
                    var pooled = other.GetComponent<PooledObject>();
                    if (pooled != null && pooled.sourcePrefabId != 0)
                        GameObjectPool.Release(other.gameObject);
                    else
                        Destroy(other.gameObject);
                    Debug.Log("Enemy destroyed");
                }
            }

            SpawnLimiter.Instance?.Unregister("Bullet", gameObject);
            GameObjectPool.Release(gameObject);
        }
    }
}