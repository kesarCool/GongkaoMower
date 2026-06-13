using UnityEngine;

/// <summary>
/// Boss 技能模块基类（非 MonoBehaviour）。
/// 提供 Target 查找、伤害回退、子弹生成、Sprite 闪烁的公共实现。
/// </summary>
public abstract class BossSkillModule
{
    public float interval;
    public float cooldown;
    public float firstDelayMul = 0.3f;

    /// <summary>模块类型名（如 "homingKnife"、"clone"）。由 BossBrain.AddModule 赋值。</summary>
    public string moduleType;

    /// <summary>被动模块（如 revive）不被 BossBrain.Update 的 Tick/CanTrigger/Execute 循环驱动。</summary>
    public virtual bool IsPassive => false;

    protected Transform boss;
    protected BossBrain brain;

    private Transform _target;
    private static readonly string PlayerTag = "Player";

    // Sprite 闪烁缓存
    private SpriteRenderer[] _sprites;
    private Color[] _originalColors;

    public virtual void Init(string rawParams, BossBrain owner)
    {
        brain = owner;
        boss = owner.transform;
    }

    public virtual bool CanTrigger() => cooldown <= 0f;
    public abstract void Execute();

    public virtual void Tick(float dt)
    {
        if (cooldown > 0f) cooldown -= dt;
    }

    protected void ResetCooldown() => cooldown = interval;

    // ── 目标查找 ──

    protected Transform FindPlayer()
    {
        // Unity Object 假 null 检测：如果引用指向已销毁对象，强制重新查找
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            var go = GameObject.FindGameObjectWithTag(PlayerTag);
            _target = go != null ? go.transform : null;
        }
        return _target;
    }

    // ── 伤害回退 ──

    protected float ResolveDamage(float configured, float fallbackDefault)
    {
        if (configured > 0f) return configured;
        var eb = boss != null ? boss.GetComponent<EnemyBase>() : null;
        return eb != null ? eb.ContactDamage : fallbackDefault;
    }

    // ── 子弹生成 ──

    protected GameObject SpawnBullet(GameObject prefab, Vector2 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        var bullet = GameObjectPool.Get(prefab, position, rotation);
        if (bullet == null)
            bullet = Object.Instantiate(prefab, position, rotation);
        return bullet;
    }

    // ── Sprite 闪烁 ──

    protected void CacheSprites()
    {
        if (boss == null) return;
        _sprites = boss.GetComponentsInChildren<SpriteRenderer>();
        if (_sprites != null && _sprites.Length > 0)
        {
            _originalColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _originalColors[i] = _sprites[i].color;
        }
    }

    protected void SetSpritesFlash(bool flash, Color? flashColorOverride = null)
    {
        if (_sprites == null || _originalColors == null) return;
        Color fc = flashColorOverride ?? Color.red;
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] != null)
                _sprites[i].color = flash ? fc : _originalColors[i];
        }
    }

    /// <summary>定时闪光：flash 持续 duration 秒后自动恢复原色。</summary>
    protected void SetSpritesFlashTimed(Color color, float duration)
    {
        SetSpritesFlash(true, color);
        brain.StartCoroutine(RestoreColorsAfter(duration));
    }

    private System.Collections.IEnumerator RestoreColorsAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetSpritesFlash(false);
    }

    // ── 参数解析 ──

    protected static float[] ParseFloats(string raw, int expectedCount)
    {
        float[] defaults = new float[expectedCount];
        if (string.IsNullOrWhiteSpace(raw)) return defaults;
        string[] parts = raw.Split(',');
        for (int i = 0; i < Mathf.Min(parts.Length, expectedCount); i++)
        {
            if (float.TryParse(parts[i].Trim(), out float v))
                defaults[i] = v;
        }
        return defaults;
    }

    // ── 程序化贴图（集中，避免模块间重复）──

    protected static Sprite CreateCircleSprite(int size = 64, float radiusRatio = 0.45f)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        float r = size * radiusRatio;
        var c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                colors[y * size + x] = Vector2.Distance(new Vector2(x, y), c) < r ? Color.white : Color.clear;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 4f);
    }

    protected static Sprite CreateSquareSprite(int s = 8)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var colors = new Color[s * s];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}
