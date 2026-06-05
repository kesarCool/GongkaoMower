using UnityEngine;

/// <summary>
/// Boss 技能模块基类（非 MonoBehaviour）。
/// 提供 Target 查找、伤害回退、子弹生成、Sprite 闪烁的公共实现。
/// </summary>
public abstract class BossSkillModule
{
    public float interval;
    public float cooldown;
    public bool requiresTarget;
    public float firstDelayMul = 0.3f; // 出场后首次冷却的比例

    protected Transform boss;
    protected BossBrain brain;

    private Transform _target;
    private static readonly string PlayerTag = "Player";

    // Sprite 闪烁缓存（子类调用 CacheSprites 后可用 SetSpritesFlash）
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

    // ── 公共工具 ──

    protected Transform FindPlayer()
    {
        if (_target == null)
        {
            var go = GameObject.FindGameObjectWithTag(PlayerTag);
            _target = go != null ? go.transform : null;
        }
        return _target;
    }

    protected float ResolveDamage(float configured, float fallbackDefault)
    {
        if (configured > 0f) return configured;
        var eb = boss != null ? boss.GetComponent<EnemyBase>() : null;
        return eb != null ? eb.ContactDamage : fallbackDefault;
    }

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
}
