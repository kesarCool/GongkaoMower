using System.Collections;
using UnityEngine;

/// <summary>
/// 周身刀气爆发：蓄力后向 N 个方向同时发射直飞刀气。
/// 参数格式：elementNum = "8,8,25,7" (冷却8s, 数量8, 伤害25, 速度7)
/// </summary>
public class BladeBurstModule : BossSkillModule
{
    public int bladeCount = 8;
    public float bladeSpeed = 7f;
    public float bladeRange = 6f;
    public float damage = 25f;
    public float chargeTime = 0.5f;
    public LayerMask wallMask;

    private GameObject _bladePrefab;
    private SpriteRenderer[] _sprites;
    private Color[] _originalColors;
    private Rigidbody2D _rb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);

        float[] p = ParseFloats(rawParams, 4);
        interval   = p[0] > 0f ? p[0] : 8f;
        bladeCount = Mathf.Max(2, (int)(p[1] > 0f ? p[1] : 8f));
        damage     = p[2] > 0f ? p[2] : 25f;
        bladeSpeed = p[3] > 0f ? p[3] : 7f;
        cooldown   = interval * 0.4f;

        _rb = boss.GetComponent<Rigidbody2D>();
        _sprites = boss.GetComponentsInChildren<SpriteRenderer>();
        if (_sprites != null && _sprites.Length > 0)
        {
            _originalColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _originalColors[i] = _sprites[i].color;
        }

        // 一次缓存
        _bladePrefab = brain.FindPrefab("StraightBullet");
        if (_bladePrefab == null)
            _bladePrefab = brain.FindPrefab("HomingBullet");

        Debug.Log($"[BladeBurstModule] 初始化 count={bladeCount} dmg={damage} speed={bladeSpeed}");
    }

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true);
        yield return new WaitForSeconds(chargeTime);
        SetSpritesFlash(false);

        float dmg = damage > 0f ? damage : boss.GetComponent<EnemyBase>()?.ContactDamage ?? 25f;
        float lifetm = bladeRange / Mathf.Max(0.1f, bladeSpeed);
        float angleStep = 360f / bladeCount;

        for (int i = 0; i < bladeCount; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 pos = _rb.position + dir * 0.6f;
            SpawnBlade(pos, dir, dmg, lifetm);
        }

        Debug.Log($"[BladeBurstModule] 发射 {bladeCount} 刀气");
        yield return new WaitForSeconds(0.15f);
        brain.IsBusy = false;
    }

    private void SpawnBlade(Vector2 pos, Vector2 dir, float dmg, float lifetm)
    {
        if (_bladePrefab == null) return;

        GameObject bullet = GameObjectPool.Get(_bladePrefab, pos, Quaternion.identity);
        if (bullet == null)
            bullet = Object.Instantiate(_bladePrefab, pos, Quaternion.identity);

        // 优先 StraightBullet，回退 HomingBullet
        BossBullet bb = bullet.GetComponent<BossBullet>();
        if (bb != null)
        {
            bb.Launch(dir, bladeSpeed, dmg, lifetm);
        }
        else
        {
            Rigidbody2D brb = bullet.GetComponent<Rigidbody2D>();
            if (brb != null) brb.velocity = dir * bladeSpeed;
            if (brain != null)
                brain.StartCoroutine(DestroyAfter(bullet, lifetm));
        }
    }

    private static IEnumerator DestroyAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) GameObjectPool.Release(obj);
    }

    private void SetSpritesFlash(bool flash)
    {
        if (_sprites == null || _originalColors == null) return;
        Color c = new Color(0.5f, 0.85f, 1f, 1f);
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null)
                _sprites[i].color = flash ? c : _originalColors[i];
    }
}
