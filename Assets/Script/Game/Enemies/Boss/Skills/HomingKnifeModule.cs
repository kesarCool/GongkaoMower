using UnityEngine;

/// <summary>
/// 追踪小刀技能：每隔 interval 秒发射一枚追踪玩家的刀。
/// 参数格式：elementNum = "3,120,5,20"
///   [0]=cooldown(秒), [1]=turnRate(度/秒), [2]=lifetime(秒), [3]=damage
/// </summary>
public class HomingKnifeModule : BossSkillModule
{
    public GameObject knifePrefab;
    public float turnRate = 120f;
    public float lifetime = 5f;
    public float damage = 20f;
    public float knifeSpeed = 4f;
    public string targetTag = "Player";

    private Transform _target;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        requiresTarget = true;

        float[] p = ParseFloats(rawParams, 4);
        interval   = p[0] > 0f ? p[0] : 3f;
        turnRate   = p[1] > 0f ? p[1] : 120f;
        lifetime   = p[2] > 0f ? p[2] : 5f;
        damage     = p[3] > 0f ? p[3] : 20f;
        cooldown   = interval * 0.3f; // 出场后 30% 冷却就首次攻击

        // 缓存 prefab（只查一次）
        knifePrefab = brain.FindPrefab("HomingBullet");
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (knifePrefab == null) return false;
        return true;
    }

    public override void Execute()
    {
        if (_target == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(targetTag);
            _target = go != null ? go.transform : null;
        }
        if (_target == null) return;

        float dmg = damage > 0f ? damage : boss.GetComponent<EnemyBase>()?.ContactDamage ?? 20f;
        Vector2 dir = ((Vector2)_target.position - (Vector2)boss.position).normalized;

        GameObject bullet = GameObjectPool.Get(knifePrefab, boss.position, Quaternion.identity);
        if (bullet == null)
            bullet = Object.Instantiate(knifePrefab, boss.position, Quaternion.identity);

        HomingBullet hb = bullet.GetComponent<HomingBullet>();
        if (hb != null) hb.Launch(dir, knifeSpeed, dmg, lifetime);

        ResetCooldown();
    }
}
