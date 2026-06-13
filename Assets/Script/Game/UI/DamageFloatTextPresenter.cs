using UnityEngine;

/// <summary>
/// 订阅 <see cref="EnemyDamagedEvent"/>，在怪物位置生成伤害飘字。场景中放一个实例并指定飘字 Prefab。
/// </summary>
[DisallowMultipleComponent]
public class DamageFloatTextPresenter : MonoBehaviour
{
    [Tooltip("须含 DamageFloatText；池化由 GameObjectPool 自动处理")]
    [SerializeField] private GameObject damageFloatPrefab;

    [Tooltip("相对锚点的生成偏移（常见：略向上；锚点优先取碰撞体/精灵中心）")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.35f, 0f);

    [Tooltip("水平面随机偏移半径（世界单位）")]
    [SerializeField] private float randomRadius = 0.08f;

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged, owner: this);
        EventBus.Subscribe<DamageResistedEvent>(OnDamageResisted, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Unsubscribe<DamageResistedEvent>(OnDamageResisted);
    }

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        if (damageFloatPrefab == null)
            return;
        if (e.damage <= 0f)
            return;

        Vector3 anchor = ResolveWorldAnchor(e);
        Vector3 rnd = Random.insideUnitCircle * randomRadius;
        Vector3 pos = anchor + spawnOffset + new Vector3(rnd.x, rnd.y, 0f);

        GameObject go = GameObjectPool.Get(damageFloatPrefab, pos, Quaternion.identity);
        if (go == null)
            return;

        DamageFloatText fx = go.GetComponent<DamageFloatText>();
        if (fx == null)
            fx = go.GetComponentInChildren<DamageFloatText>(true);
        if (fx != null)
            fx.Play(e.damage, pos, e.isCrit, e.isPenetration);
        else
            GameObjectPool.Release(go);
    }

    private void OnDamageResisted(DamageResistedEvent e)
    {
        if (damageFloatPrefab == null) return;
        if (e.resistedAmount <= 0f) return;

        Vector3 anchor = ResolveWorldAnchor(e);
        // 免伤飘字略微偏右上，避免和伤害数字重叠
        Vector3 rnd = Random.insideUnitCircle * randomRadius;
        Vector3 pos = anchor + spawnOffset + new Vector3(0.4f, 0.25f, 0f) + new Vector3(rnd.x, rnd.y, 0f);

        GameObject go = GameObjectPool.Get(damageFloatPrefab, pos, Quaternion.identity);
        if (go == null) return;

        DamageFloatText fx = go.GetComponent<DamageFloatText>();
        if (fx == null) fx = go.GetComponentInChildren<DamageFloatText>(true);
        if (fx != null)
            fx.PlayResist(e.resistedAmount, pos, e.fullyNegated);
        else
            GameObjectPool.Release(go);
    }

    /// <summary>优先用碰撞体/精灵几何中心，避免怪物 pivot 在脚底时整段偏移。</summary>
    private static Vector3 ResolveWorldAnchor(EnemyDamagedEvent e)
    {
        if (e.enemy == null)
            return e.worldPosition;

        Collider2D col = e.enemy.GetComponent<Collider2D>();
        if (col == null)
            col = e.enemy.GetComponentInChildren<Collider2D>();
        if (col != null)
            return col.bounds.center;

        SpriteRenderer sr = e.enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return e.worldPosition;
    }

    private static Vector3 ResolveWorldAnchor(DamageResistedEvent e)
    {
        if (e.enemy == null)
            return e.worldPosition;

        Collider2D col = e.enemy.GetComponent<Collider2D>();
        if (col == null)
            col = e.enemy.GetComponentInChildren<Collider2D>();
        if (col != null)
            return col.bounds.center;

        SpriteRenderer sr = e.enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
            return sr.bounds.center;

        return e.worldPosition;
    }
}
