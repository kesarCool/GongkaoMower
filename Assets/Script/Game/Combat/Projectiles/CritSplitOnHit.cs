using System.Collections;
using UnityEngine;

/// <summary>
/// Legend 突破附件：暴击时在命中点延迟分裂出小剑气。
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class CritSplitOnHit : MonoBehaviour
{
    [Tooltip("分裂数量")]
    public int splitCount = 3;
    [Tooltip("子剑气 Prefab")]
    public GameObject splitBulletPrefab;
    [Tooltip("子剑气伤害系数")]
    public float splitDmgMul = 0.4f;
    [Tooltip("子剑气存活时间")]
    public float splitLifetime = 1.2f;

    private PlayerBullet _bullet;
    private bool _spawned;
    private static float _lastSplitTime = -999f;
    private const float SplitCooldown = 0.5f;

    private void Awake()
    {
        _bullet = GetComponent<PlayerBullet>();
    }

    private void OnEnable()
    {
        _spawned = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_spawned) return;
        if (Time.time - _lastSplitTime < SplitCooldown) return; // CD 0.5s
        if (_bullet == null) return;
        if (!_bullet.IsCrit) return;
        if (!other.CompareTag(_bullet.targetTag)) return;
        if (splitBulletPrefab == null || splitCount <= 0) return;

        _spawned = true;
        _lastSplitTime = Time.time;

        // 命中点：挂在敌人 Transform 下，坐标归零 = 敌人身上
        Vector2 hitPos = other.transform.position;
        float dmg = _bullet.damage * splitDmgMul;
        float spd = _bullet.speed;
        SkillId src = _bullet.skillSource;
        int cnt = splitCount;
        GameObject prefab = splitBulletPrefab;
        float lifetime = splitLifetime;

        // 背向玩家方向（从命中点反推玩家方向）
        Vector2 awayFromPlayer = Random.insideUnitCircle.normalized; // 兜底
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            awayFromPlayer = (hitPos - (Vector2)player.transform.position).normalized;

        // Debug.Log($"[CritSplitOnHit] 暴击分裂：count={cnt}, enemyPos={hitPos}");

        var spawner = new GameObject("CritSplitSpawner");
        spawner.transform.position = hitPos;
        var comp = spawner.AddComponent<CritSplitSpawner>();
        comp.Init(prefab, dmg, spd, src, cnt, lifetime, hitPos, awayFromPlayer);
    }
}

internal sealed class CritSplitSpawner : MonoBehaviour
{
    private GameObject _prefab;
    private float _dmg, _spd, _lifetime;
    private SkillId _src;
    private int _count;
    private Vector3 _worldPos;
    private Vector2 _awayFromPlayer;

    public void Init(GameObject prefab, float dmg, float spd, SkillId src, int count, float lifetime, Vector3 worldPos, Vector2 awayFromPlayer)
    {
        _prefab = prefab; _dmg = dmg; _spd = spd; _src = src; _count = count; _lifetime = lifetime;
        _worldPos = worldPos; _awayFromPlayer = awayFromPlayer;
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(0.06f);

        float baseAngle = Mathf.Atan2(_awayFromPlayer.y, _awayFromPlayer.x) * Mathf.Rad2Deg;
        float halfAngle = 135f; // 270° / 2

        for (int i = 0; i < _count; i++)
        {
            float angle = (baseAngle + Random.Range(-halfAngle, halfAngle)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var sub = Instantiate(_prefab, _worldPos, Quaternion.identity);
            var pb = sub.GetComponent<PlayerBullet>();
            if (pb != null)
                pb.Launch(dir, new BulletLaunchParams(_spd, _dmg, _lifetime, _src));

            if (i < _count - 1)
                yield return new WaitForSeconds(0.04f);
        }

        Destroy(gameObject);
    }
}
