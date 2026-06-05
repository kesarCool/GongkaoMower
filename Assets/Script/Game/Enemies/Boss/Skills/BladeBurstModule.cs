using System.Collections;
using UnityEngine;

/// <summary>
/// 周身刀气爆发：蓄力后向 N 个方向同时发射直飞刀气。
/// elementNum = "8,8,25,7" = cooldown, bladeCount, damage, bladeSpeed
/// </summary>
public class BladeBurstModule : BossSkillModule
{
    private int _bladeCount = 8;
    private float _bladeSpeed = 7f;
    private float _bladeRange = 6f;
    private float _damage = 25f;
    private float _chargeTime = 0.5f;

    private GameObject _bladePrefab;
    private Rigidbody2D _rb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        firstDelayMul = 0.4f; // 大技能出场多等一会

        float[] p = ParseFloats(rawParams, 4);
        interval   = p[0] > 0f ? p[0] : 8f;
        _bladeCount = Mathf.Max(2, (int)(p[1] > 0f ? p[1] : 8f));
        _damage     = p[2] > 0f ? p[2] : 25f;
        _bladeSpeed = p[3] > 0f ? p[3] : 7f;
        cooldown   = interval * firstDelayMul;

        _rb = boss.GetComponent<Rigidbody2D>();
        CacheSprites();

        _bladePrefab = brain.FindPrefab("StraightBullet");
        if (_bladePrefab == null) _bladePrefab = brain.FindPrefab("HomingBullet");
    }

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true, new Color(0.5f, 0.85f, 1f, 1f));
        yield return new WaitForSeconds(_chargeTime);
        SetSpritesFlash(false);

        float dmg = ResolveDamage(_damage, 25f);
        float lifetm = _bladeRange / Mathf.Max(0.1f, _bladeSpeed);
        float angleStep = 360f / _bladeCount;

        for (int i = 0; i < _bladeCount; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            SpawnBlade(_rb.position + dir * 0.6f, dir, dmg, lifetm);
        }

        yield return new WaitForSeconds(0.15f);
        brain.IsBusy = false;
    }

    private void SpawnBlade(Vector2 pos, Vector2 dir, float dmg, float lifetm)
    {
        GameObject bullet = SpawnBullet(_bladePrefab, pos, Quaternion.identity);
        if (bullet == null) return;
        var bb = bullet.GetComponent<BossBullet>();
        if (bb != null) { bb.Launch(dir, _bladeSpeed, dmg, lifetm); }
    }
}
