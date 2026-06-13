using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 免伤护盾：挂在 Boss GameObject 上，持续期间按伤害类型过滤减伤。
/// 由 <see cref="ResistModule"/> 创建，duration 到期或触发次数用尽后自毁。
/// </summary>
[DisallowMultipleComponent]
public class ResistShield : MonoBehaviour
{
    /// <summary>减伤比例 (0~1，1=完全免疫)。</summary>
    public float resistRatio = 0.6f;

    /// <summary>阻挡的伤害类型。</summary>
    public SkillDamageType[] blockedTypes = Array.Empty<SkillDamageType>();

    /// <summary>剩余触发次数 (-1=不限)。</summary>
    public int remainingTriggers;

    private float _elapsed;
    private float _duration;
    private SpriteRenderer[] _sprites;
    private Color[] _originalColors;
    private GameObject _activationRing;
    private static readonly Color ShieldTint = new Color(0.3f, 0.6f, 1f, 0.5f);

    /// <summary>初始化护盾参数并开始计时。</summary>
    public void Setup(float ratio, SkillDamageType[] types, int maxTriggers, float duration)
    {
        resistRatio = Mathf.Clamp01(ratio);
        blockedTypes = types ?? Array.Empty<SkillDamageType>();
        remainingTriggers = maxTriggers > 0 ? maxTriggers : -1; // -1 = 不限
        _duration = Mathf.Max(0.1f, duration);
        _elapsed = 0f;

        ApplyShieldVisual(true);
        PlayActivationEffect();
    }

    /// <summary>每帧由 EnemyBase.TakeDamage 调用。返回 true 表示本次伤害被抵抗。</summary>
    public bool ApplyResist(SkillId damageSource, ref float damage)
    {
        if (remainingTriggers == 0) return false; // 0 = 耗尽，-1 = 不限
        if (blockedTypes.Length == 0) return false;

        SkillDamageType incoming = damageSource.GetDamageType();
        bool blocked = false;
        for (int i = 0; i < blockedTypes.Length; i++)
        {
            if (blockedTypes[i] == incoming) { blocked = true; break; }
        }
        if (!blocked) return false;

        float resisted = damage * resistRatio;
        damage -= resisted;
        bool fullyNegated = damage <= 0f;
        if (damage < 0f) damage = 0f;

        if (remainingTriggers > 0) remainingTriggers--; // -1 不计次

        // 发布抵抗事件（飘字："免伤"=全挡，"抵抗"=部分挡）
        EventBus.Publish(new DamageResistedEvent
        {
            enemy = GetComponent<EnemyBase>(),
            resistedAmount = resisted,
            worldPosition = transform.position,
            fullyNegated = fullyNegated,
        });

        if (remainingTriggers == 0)
            DestroyShield();

        return true;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration)
            DestroyShield();
    }

    private void DestroyShield()
    {
        ApplyShieldVisual(false);
        Destroy(this);
    }

    // ── 视觉 ──

    /// <summary>开盾瞬间：扩散光环 + Boss 缩放脉冲。</summary>
    private void PlayActivationEffect()
    {
        // Boss 缩放脉冲
        Vector3 orig = transform.localScale;
        transform.localScale = orig * 1.15f;
        StartCoroutine(ScalePunchRoutine(orig));

        // 扩散光环 — 世界空间独立 GameObject，不受 Boss scale/层级遮挡
        _activationRing = new GameObject("ShieldRing");
        _activationRing.transform.position = transform.position;
        var sr = _activationRing.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = new Color(0.3f, 0.7f, 1f, 0.85f);
        sr.sortingOrder = 500;
        _activationRing.transform.localScale = Vector3.one * 0.3f;

        StartCoroutine(ExpandAndFade(_activationRing, sr));
    }

    private IEnumerator ScalePunchRoutine(Vector3 originalScale)
    {
        float t = 0f;
        float dur = 0.25f;
        while (t < dur && this != null)
        {
            t += Time.deltaTime;
            float u = t / dur;
            transform.localScale = Vector3.Lerp(originalScale * 1.15f, originalScale, u);
            yield return null;
        }
        if (this != null) transform.localScale = originalScale;
    }

    private static IEnumerator ExpandAndFade(GameObject ring, SpriteRenderer sr)
    {
        float t = 0f;
        float dur = 0.45f;
        Vector3 startScale = Vector3.one * 0.4f;
        Vector3 endScale = Vector3.one * 3f;
        while (t < dur && ring != null)
        {
            t += Time.deltaTime;
            float u = t / dur;
            ring.transform.localScale = Vector3.Lerp(startScale, endScale, u);
            Color c = sr.color;
            c.a = Mathf.Lerp(0.7f, 0f, u);
            sr.color = c;
            yield return null;
        }
        if (ring != null) Destroy(ring);
    }

    private void ApplyShieldVisual(bool show)
    {
        if (_sprites == null)
        {
            _sprites = GetComponentsInChildren<SpriteRenderer>();
            if (_sprites != null && _sprites.Length > 0)
            {
                _originalColors = new Color[_sprites.Length];
                for (int i = 0; i < _sprites.Length; i++)
                    _originalColors[i] = _sprites[i].color;
            }
        }

        if (_sprites == null || _originalColors == null) return;

        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null)
                _sprites[i].color = show
                    ? Color.Lerp(_originalColors[i], ShieldTint, 0.65f)
                    : _originalColors[i];
        }
    }

    private void OnDestroy()
    {
        ApplyShieldVisual(false);
        if (_activationRing != null) Destroy(_activationRing);
    }

    /// <summary>运行时生成一个径向渐变圆环贴图（避免依赖外部资源）。</summary>
    private static Sprite CreateRingSprite()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        float outerR = size * 0.48f;
        float innerR = size * 0.22f; // 宽环，避免太细看不清
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float alpha = (d < innerR || d > outerR) ? 0f
                    : 1f - Mathf.Abs(d - (innerR + outerR) * 0.5f) / ((outerR - innerR) * 0.5f);
                colors[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
