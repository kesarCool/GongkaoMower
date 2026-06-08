using System.Collections;
using UnityEngine;

/// <summary>
/// 地裂技能：Boss 拍地，扇形区域延迟爆发 AOE。
/// elementNum = "10,5,5,30" = cooldown, zoneCount, zoneRadius, damage
/// </summary>
public class ZoneModule : BossSkillModule
{
    private int _zoneCount = 5;
    private float _zoneRadius = 1.5f;
    private float _damage = 30f;
    private float _warnDuration = 0.7f;
    private float _coneAngle = 150f;
    private float _spreadLength = 6f;

    private Rigidbody2D _rb;
    private GameObject _indicatorPrefab;
    private GameObject _explosionPrefab;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        firstDelayMul = 0.5f;

        float[] p = ParseFloats(rawParams, 4);
        interval   = p[0] > 0f ? p[0] : 10f;
        _zoneCount = Mathf.Max(2, (int)(p[1] > 0f ? p[1] : 5f));
        _zoneRadius = p[2] > 0f ? p[2] : 1.5f;
        _damage     = p[3] > 0f ? p[3] : 30f;
        cooldown   = interval * firstDelayMul;

        _rb = boss.GetComponent<Rigidbody2D>();
        _indicatorPrefab = brain.FindPrefab("ZoneIndicator");
        _explosionPrefab = brain.FindPrefab("ZoneExplosion");
        CacheSprites();
    }

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(ZoneRoutine());
    }

    private IEnumerator ZoneRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true, new Color(1f, 0.3f, 0f, 1f));

        Vector2 center = _rb != null ? _rb.position : (Vector2)boss.position;
        Transform target = FindPlayer();
        Vector2 toPlayer = target != null
            ? ((Vector2)target.position - center).normalized
            : Vector2.down;

        float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        float halfCone = _coneAngle * 0.5f;

        SetSpritesFlash(false);

        float dmg = ResolveDamage(_damage, 30f);
        var hitSet = new System.Collections.Generic.HashSet<int>();
        float intervalBetweenZones = _warnDuration / Mathf.Max(1, _zoneCount);

        // ── 逐个生成→预警→爆发（顺序扩散）──
        for (int i = 0; i < _zoneCount; i++)
        {
            float t = _zoneCount > 1 ? (float)i / (_zoneCount - 1) : 0.5f;
            float deg = baseAngle + Mathf.Lerp(-halfCone, halfCone, t);
            Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            float dist = Mathf.Lerp(1f, _spreadLength, t * 0.7f + Random.value * 0.3f);
            Vector2 pos = center + dir * dist;

            // 生成指示器
            GameObject go = SpawnIndicator(pos, i);
            if (go == null) continue;

            // 短闪预警
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                for (int flash = 0; flash < 2; flash++)
                {
                    sr.color = new Color(1f, 0.25f, 0f, flash % 2 == 0 ? 0.6f : 0.2f);
                    yield return new WaitForSeconds(intervalBetweenZones * 0.5f);
                }
            }
            else
            {
                yield return new WaitForSeconds(intervalBetweenZones);
            }

            // 爆发
            if (_explosionPrefab != null)
                Object.Instantiate(_explosionPrefab, new Vector3(pos.x, pos.y, -0.1f), Quaternion.identity);

            bool prev = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = true;
            var hits = Physics2D.OverlapCircleAll(pos, _zoneRadius);
            for (int h = 0; h < hits.Length; h++)
            {
                var ph = hits[h].GetComponent<PlayerHealth>();
                if (ph == null) ph = hits[h].GetComponentInParent<PlayerHealth>();
                if (ph == null) continue;
                if (!hitSet.Add(ph.GetInstanceID())) continue;
                ph.TakeDamage(dmg, boss);
            }
            Physics2D.queriesHitTriggers = prev;

            brain.StartCoroutine(PopAndDestroy(go, 0.12f));
        }

        brain.IsBusy = false;
    }

    private GameObject SpawnIndicator(Vector2 pos, int index)
    {
        if (_indicatorPrefab != null)
        {
            var go = Object.Instantiate(_indicatorPrefab, pos, Quaternion.identity, boss);
            go.name = "ZoneWarn_" + index;
            go.transform.localScale = Vector3.one * (_zoneRadius * 2f);
            return go;
        }

        var legacy = new GameObject("ZoneWarn_" + index);
        legacy.transform.position = pos;
        legacy.transform.SetParent(boss, false);
        var sr = legacy.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.25f, 0f, 0.5f);
        sr.sortingOrder = 15;
        legacy.transform.localScale = Vector3.one * (_zoneRadius * 2f);
        return legacy;
    }

    private static Sprite CreateCircleSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        float radius = size * 0.45f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.Clamp01(d / radius);
                alpha = alpha > 0.8f ? 1f : alpha * 0.8f;
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static IEnumerator PopAndDestroy(GameObject go, float duration)
    {
        if (go == null) yield break;
        var sr = go.GetComponent<SpriteRenderer>();
        Vector3 baseScale = go.transform.localScale;
        float t = 0f;
        while (t < duration && go != null)
        {
            t += Time.deltaTime;
            float u = t / duration;
            go.transform.localScale = baseScale * (1f + u * 0.5f);
            if (sr != null) { var c = sr.color; c.a = 1f - u; sr.color = c; }
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }
}
