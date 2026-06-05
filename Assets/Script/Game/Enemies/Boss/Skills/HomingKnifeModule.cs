using UnityEngine;

/// <summary>
/// 追踪小刀技能：每隔 interval 秒发射追踪玩家的刀。
/// elementNum = "3,120,5,20,4" = cooldown, turnRate, lifetime, damage, knifeSpeed
/// </summary>
public class HomingKnifeModule : BossSkillModule
{
    private GameObject _knifePrefab;
    private float _turnRate = 120f;
    private float _lifetime = 5f;
    private float _damage = 20f;
    private float _knifeSpeed = 4f;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        requiresTarget = true;

        float[] p = ParseFloats(rawParams, 5);
        interval   = p[0] > 0f ? p[0] : 3f;
        _turnRate  = p[1] > 0f ? p[1] : 120f;
        _lifetime  = p[2] > 0f ? p[2] : 5f;
        _damage    = p[3] > 0f ? p[3] : 20f;
        _knifeSpeed = p[4] > 0f ? p[4] : 4f;
        cooldown   = interval * firstDelayMul;

        _knifePrefab = brain.FindPrefab("HomingBullet");
    }

    public override bool CanTrigger() => base.CanTrigger() && _knifePrefab != null;

    public override void Execute()
    {
        Transform target = FindPlayer();
        if (target == null) return;

        float dmg = ResolveDamage(_damage, 20f);
        Vector2 dir = ((Vector2)target.position - (Vector2)boss.position).normalized;

        GameObject bullet = SpawnBullet(_knifePrefab, boss.position, Quaternion.identity);
        var hb = bullet != null ? bullet.GetComponent<HomingBullet>() : null;
        if (hb != null) hb.Launch(dir, _knifeSpeed, dmg, _lifetime);

        ResetCooldown();
    }
}
