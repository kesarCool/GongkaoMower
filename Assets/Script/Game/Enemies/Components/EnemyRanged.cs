using System.Collections;
using UnityEngine;

/// <summary>
/// EnemyRanged（可攻击的远程怪示例）
/// - 基于 EnemyBase
/// - 以固定间隔朝玩家方向发射子弹（需求 6.A）
///
/// 说明：
/// - 子弹预制体默认来自 EnemyDefinition.bulletPrefab（也可在 Inspector 覆盖）
/// - 子弹脚本你可以后续做 EnemyBullet（命中玩家扣血/打印）
/// </summary>
public class EnemyRanged : EnemyBase
{
    [Header("远程攻击")]
    [Tooltip("攻击间隔（秒）。越小攻击越频繁。")]
    public float attackInterval = 1.5f;

    [Tooltip("子弹速度")]
    public float bulletSpeed = 8f;

    [Tooltip("可选：覆盖配置里的子弹预制体。不填则使用 EnemyDefinition 中的 bulletPrefab。")]
    public GameObject bulletPrefabOverride;

    [Tooltip("目标Tag（默认 Player）")]
    public string playerTag = "Player";

    private Transform _player;
    private Coroutine _routine;

    private void OnEnable()
    {
        _routine = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, attackInterval));
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (_player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            _player = p != null ? p.transform : null;
        }
        if (_player == null) return;

        GameObject prefab = bulletPrefabOverride != null ? bulletPrefabOverride : bulletPrefab;
        if (prefab == null) return;

        Vector2 dir = (Vector2)(_player.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        GameObject b = Instantiate(prefab, transform.position, Quaternion.identity);

        // 如果复用你现有 Bullet（它是打 Enemy 的），这里不会生效；
        // 建议你后续做一个 EnemyBullet 专门打 Player。
        Rigidbody2D rb = b.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = dir * bulletSpeed;
    }
}

