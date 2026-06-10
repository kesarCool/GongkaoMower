using System.Collections;
using UnityEngine;

/// <summary>
/// Spawner
/// - 使用协程按固定时间间隔生成敌人预制体
/// - 在屏幕边缘随机位置生成（基于 Camera.ViewportToWorldPoint）
/// - 生成后可设置敌人速度（EnemyAI.moveSpeed）与血量（EnemyStats.hp）
/// </summary>
public class Spawner : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("要生成的敌人预制体")]
    public GameObject enemyPrefab;

    [Tooltip("生成间隔（秒），例如 1 表示每秒生成一次")]
    public float spawnInterval = 1f;

    [Tooltip("生成时离屏幕边缘向外偏移（世界单位），避免刚好卡在边界上")]
    public float edgeWorldOffset = 0.5f;

    [Tooltip("用于计算屏幕边缘的位置；不填则使用 Camera.main")]
    public Camera worldCamera;

    [Header("Enemy Params (Optional)")]
    [Tooltip("是否随机敌人移动速度并写入 EnemyAI.moveSpeed")]
    public bool applyMoveSpeed = true;

    [Tooltip("敌人移动速度范围（会随机）")]
    public Vector2 enemySpeedRange = new Vector2(1.5f, 3.5f);

    [Tooltip("是否设置敌人血量（写入 EnemyStats.hp）")]
    public bool applyHp = true;

    [Tooltip("敌人血量范围（会随机）")]
    public Vector2 enemyHpRange = new Vector2(3f, 10f);

    private Coroutine _routine;

    private void OnEnable()
    {
        if (_routine == null)
            _routine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float interval = Mathf.Max(0.01f, spawnInterval);

            if (enemyPrefab != null)
            {
                SpawnOne();
            }
            else
            {
                Debug.LogWarning("Spawner: enemyPrefab is not assigned.");
            }

            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnOne()
    {
        // 检查上限与节流
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Enemy", out _))
                return;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Spawner: no camera found (assign worldCamera or tag a camera as MainCamera).");
            return;
        }

        Vector3 pos = GetRandomEdgeWorldPos(cam);
        pos.z = 0f;

        GameObject enemy = GameObjectPool.Get(enemyPrefab, pos, Quaternion.identity);
        SpawnLimiter.Instance?.RegisterSpawned("Enemy", enemy);

        // 生成后立即检测卡墙并推出
        WallStuckResolver.ResolveTransform(enemy.transform);

        if (applyMoveSpeed)
        {
            float spd = Random.Range(enemySpeedRange.x, enemySpeedRange.y);
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.moveSpeed = spd;
        }

        if (applyHp)
        {
            float hp = Random.Range(enemyHpRange.x, enemyHpRange.y);
            EnemyStats stats = enemy.GetComponent<EnemyStats>();
            if (stats == null) stats = enemy.AddComponent<EnemyStats>();
            stats.hp = hp;
            stats.maxHp = Mathf.Max(stats.maxHp, hp);
        }
    }

    /// <summary>
    /// 在屏幕四条边随机取点，再转为世界坐标。
    /// Viewport 坐标范围 [0..1]：x=0/1 为左右边，y=0/1 为上下边。
    /// </summary>
    private Vector3 GetRandomEdgeWorldPos(Camera cam)
    {
        // 0:Left 1:Right 2:Bottom 3:Top
        int side = Random.Range(0, 4);
        float t = Random.value;

        Vector3 vp;
        switch (side)
        {
            case 0: vp = new Vector3(0f, t, cam.nearClipPlane); break;
            case 1: vp = new Vector3(1f, t, cam.nearClipPlane); break;
            case 2: vp = new Vector3(t, 0f, cam.nearClipPlane); break;
            default: vp = new Vector3(t, 1f, cam.nearClipPlane); break;
        }

        Vector3 world = cam.ViewportToWorldPoint(vp);

        // 向屏幕外侧再推一点，避免直接生成在画面内边缘
        Vector2 pushDir;
        switch (side)
        {
            case 0: pushDir = Vector2.left; break;
            case 1: pushDir = Vector2.right; break;
            case 2: pushDir = Vector2.down; break;
            default: pushDir = Vector2.up; break;
        }

        world += (Vector3)(pushDir * edgeWorldOffset);
        return world;
    }
}
