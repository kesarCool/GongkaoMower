using UnityEngine;

/// <summary>
/// 环绕刀片：在玩家周围生成若干子刀片并旋转；伤害由 SkillOrbBladeHit 处理
/// </summary>
public class SkillOrbitingBlades : SkillBase
{
    public int bladeCount = 3;
    public float orbitRadius = 1.2f;
    public float rotateSpeedDeg = 180f;
    public float damagePerTick = 1f;
    public float tickInterval = 0.15f;

    private readonly Sprite _bladeSprite;
    private readonly int _spriteSortingOrder;
    private readonly Color _spriteTint;
    private readonly float _visualScale;

    private Transform _orbitRoot;
    private float _angle;

    /// <param name="bladeSprite">美术图；为 null 时使用程序化白块占位</param>
    /// <param name="spriteSortingOrder">SpriteRenderer.sortingOrder，被地面挡住时调大</param>
    /// <param name="spriteTint">与美术色相乘；保留原画用白色</param>
    /// <param name="visualScale">刀片根节点统一缩放</param>
    public SkillOrbitingBlades(
        Sprite bladeSprite,
        int bladeCount,
        float orbitRadius,
        float rotateSpeedDeg,
        float damagePerTick,
        float tickInterval,
        int spriteSortingOrder,
        Color spriteTint,
        float visualScale)
    {
        Id = SkillId.OrbitingBlades;
        _bladeSprite = bladeSprite;
        _spriteSortingOrder = spriteSortingOrder;
        _spriteTint = spriteTint;
        _visualScale = Mathf.Max(0.01f, visualScale);
        this.bladeCount = Mathf.Max(1, bladeCount);
        this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
        this.rotateSpeedDeg = rotateSpeedDeg;
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
    }

    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx);
        EnsureOrbitVisuals();
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        if (_orbitRoot != null)
        {
            Object.Destroy(_orbitRoot.gameObject);
            _orbitRoot = null;
        }
    }

    public override void OnLevelUp()
    {
        base.OnLevelUp();
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;

        if (_orbitRoot == null) EnsureOrbitVisuals();

        _orbitRoot.position = _ctx.player.position;
        _angle += rotateSpeedDeg * deltaTime;
        _orbitRoot.rotation = Quaternion.Euler(0f, 0f, _angle);
    }

    private void EnsureOrbitVisuals()
    {
        if (_orbitRoot != null) return;
        if (_ctx.player == null) return;

        GameObject root = new GameObject("OrbitingBlades");
        root.transform.SetParent(_ctx.player, false);
        root.transform.localPosition = Vector3.zero;
        _orbitRoot = root.transform;

        RebuildBlades();
    }

    private void RebuildBlades()
    {
        if (_orbitRoot == null) return;

        // 清理旧刀片
        for (int i = _orbitRoot.childCount - 1; i >= 0; i--)
            Object.Destroy(_orbitRoot.GetChild(i).gameObject);

        float step = 360f / bladeCount;
        for (int i = 0; i < bladeCount; i++)
        {
            GameObject blade = new GameObject($"Blade_{i}");
            blade.transform.SetParent(_orbitRoot, false);

            float deg = i * step;
            Vector3 local = Quaternion.Euler(0f, 0f, deg) * (Vector3.right * orbitRadius);
            blade.transform.localPosition = local;
            blade.transform.localScale = Vector3.one * _visualScale;

            var sr = blade.AddComponent<SpriteRenderer>();
            sr.sprite = _bladeSprite != null ? _bladeSprite : RuntimeSprites.GetUiPlaceholderSprite();
            sr.color = _spriteTint;
            sr.sortingOrder = _spriteSortingOrder;

            var col = blade.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.35f, 0.8f);

            var rb = blade.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var hit = blade.AddComponent<SkillOrbBladeHit>();
            hit.damagePerTick = damagePerTick;
            hit.tickInterval = tickInterval;
        }
    }

    public void ApplyRuntimeStats(int bladeCount, float orbitRadius, float rotateSpeedDeg, float damagePerTick, float tickInterval)
    {
        this.bladeCount = Mathf.Max(1, bladeCount);
        this.orbitRadius = Mathf.Max(0.1f, orbitRadius);
        this.rotateSpeedDeg = rotateSpeedDeg;
        this.damagePerTick = Mathf.Max(0.01f, damagePerTick);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        RebuildBlades();
    }
}
