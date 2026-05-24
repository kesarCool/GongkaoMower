using UnityEngine;

/// <summary>
/// [已废弃] Boss 行为：牛刀小试 — 追踪小刀。
/// 已被 BossBrain + HomingKnifeModule 替代。保留此文件仅用于序列化兼容。
/// 所有新 Boss 请用 BossBrain + Excel element[] 配置。
/// </summary>
[DisallowMultipleComponent]
[System.Obsolete("Use BossBrain + HomingKnifeModule instead. Excel Monster.element[] = [\"homingKnife\"]")]
public class BossBehavior_T1 : MonoBehaviour
{
    [Header("攻击节奏")]
    public float attackInterval = 3f;

    [Header("小刀")]
    public GameObject knifePrefab;
    [Tooltip("每把小刀伤害；0 表示取 Boss 的 ContactDamage")]
    public float knifeDamage;
    public float knifeSpeed = 4f;
    public float knifeLifetime = 5f;
    public float knifeTurnRate = 120f;

    [Header("目标")]
    public string targetTag = "Player";

    private Transform _target;
    private float _nextAttackTime;

    private void OnEnable()
    {
        _nextAttackTime = Time.time + attackInterval * 0.5f; // 出场 1.5 秒后首次攻击
    }

    private void Update()
    {
        if (knifePrefab == null) return;
        if (Time.time < _nextAttackTime) return;

        if (_target == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            _target = go != null ? go.transform : null;
        }
        if (_target == null) return;

        _nextAttackTime = Time.time + attackInterval;
        FireKnife();
    }

    private void FireKnife()
    {
        GameObject bullet = GameObjectPool.Get(knifePrefab, transform.position, Quaternion.identity);
        if (bullet == null)
        {
            bullet = Instantiate(knifePrefab, transform.position, Quaternion.identity);
        }

        HomingBullet hb = bullet.GetComponent<HomingBullet>();
        if (hb != null)
        {
            hb.speed = knifeSpeed;
            hb.turnRate = knifeTurnRate;
            hb.lifetime = knifeLifetime;
            // knifeDamage=0 时自动取 Boss 当前的接触伤害（来自 Excel LevelWave.attack）
            float dmg = knifeDamage > 0f ? knifeDamage : GetComponent<EnemyBase>()?.ContactDamage ?? 20f;
            hb.damage = dmg;
        }
    }
}
