using UnityEngine;

/// <summary>
/// 散射子弹（满级突破）：命中敌人后在命中点散射出子子弹。
/// 穿透时不触发散射，二者互斥。
/// </summary>
public class ScatterBullet : PlayerBullet
{
    [Tooltip("散射子子弹数")]
    public int scatterCount = 4;
    [Tooltip("子子弹伤害系数（0.4 = 主弹 40%）")]
    public float scatterDmgMul = 0.4f;
    [Tooltip("子子弹存活时间")]
    public float scatterLifetime = 1.5f;
    [Tooltip("散射角度范围（度），避开玩家向前方向")]
    [Range(90f, 360f)]
    public float scatterAngle = 270f;
    [Tooltip("子子弹 Prefab（通常用基础 PlayerStraightBullet）")]
    public GameObject scatterBulletPrefab;

    private Transform _playerRef;

    public void SetPlayerRef(Transform player) => _playerRef = player;

    protected override void OnFrameMove()
    {
        _rb.velocity = _dir * speed;
    }

    protected override void OnHitEnemy(Collider2D other)
    {
        // 穿透不触发散射
        if (_pierceRemaining > 0 && _pierceRate > 0f && Random.value < _pierceRate)
        {
            _pierceRemaining--;
            return;
        }

        // 产生散射
        if (scatterBulletPrefab != null && scatterCount > 0)
            SpawnScatterBullets(other);

        Release();
    }

    private void SpawnScatterBullets(Collider2D other)
    {
        Vector2 hitPos = other.ClosestPoint(transform.position);
        Vector2 away = _playerRef != null
            ? (hitPos - (Vector2)_playerRef.position).normalized
            : Random.insideUnitCircle.normalized;

        float baseAngle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
        float halfAngle = scatterAngle * 0.5f;

        for (int i = 0; i < scatterCount; i++)
        {
            float deg = baseAngle + Random.Range(-halfAngle, halfAngle);
            Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

            GameObject sub = Instantiate(scatterBulletPrefab, hitPos, Quaternion.identity);
            var pb = sub.GetComponent<PlayerBullet>();
            if (pb != null)
            {
                pb.Launch(dir, new BulletLaunchParams(speed, damage * scatterDmgMul, scatterLifetime, skillSource));
            }
        }
    }
}
