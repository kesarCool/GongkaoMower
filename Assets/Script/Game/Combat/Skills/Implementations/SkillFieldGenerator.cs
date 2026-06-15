using UnityEngine;

/// <summary>
/// 罡气护体：多边形填充底盘 + 多圈脉冲从角色扩散到周身。
/// 底盘标示伤害覆盖范围，脉冲波持续向外扩散，颜色由深到浅。
/// 未突破绿色，突破后金黄色。
/// </summary>
public class SkillFieldGenerator : SkillBase
{
    public float radius = 1.5f;
    public float damagePerSecond = 15f;
    public float damageTickInterval = 0.2f;
    public float visualIntensity = 1f;
    public int sortingOrder = 30;

    /// <summary>脉冲周期（秒），越小越快。随等级可调。</summary>
    public float pulseCycleDuration = 1.2f;

    // 底盘多边形边数（6=六边形）
    private const int PolygonSides = 6;
    private const float DiscBaseAlpha = 0.45f;
    private const float RingStartAlpha = 0.8f;

    // 脉冲环数量
    private const int PulseRingCount = 3;

    private Transform _fieldRoot;
    private GameObject _discInstance;
    private SpriteRenderer _discSr;
    private GameObject[] _pulseRings;
    private SpriteRenderer[] _pulseSrs;
    private float _damageAccumulator;
    private bool _isBreakthrough;

    // 未突破：亮黄罡气  /  突破后：紫红罡气
    private static readonly Color GreenAura = new Color(1f, 0.92f, 0.15f, 1f);   // #FFEB26 亮黄
    private static readonly Color GoldAura  = new Color(0.85f, 0.12f, 0.35f, 1f); // #D91F59 紫红

    // 贴图缓存（所有实例共享）
    private static Sprite _cachedDiscSprite;
    private static int _cachedDiscSides;

    // 脉冲环贴图（圆形细环，所有实例共享）
    private static Sprite _cachedRingSprite;

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

    public SkillFieldGenerator()
    {
        Id = SkillId.FieldGenerator;
    }

    public void ApplyRuntimeStats(
        float newRadius,
        float newDamagePerSecond,
        float newDamageTickInterval,
        float newVisualIntensity)
    {
        radius = Mathf.Max(0.3f, newRadius);
        damagePerSecond = Mathf.Max(0.01f, newDamagePerSecond);
        damageTickInterval = Mathf.Clamp(newDamageTickInterval, 0.05f, 1f);
        visualIntensity = Mathf.Max(0.1f, newVisualIntensity);
        RefreshVisual();
    }

    /// <summary>Legend 升阶突破（局外养成），也触发金色。</summary>
    public override void ApplyLegendBreakthrough(int stage)
    {
        _isBreakthrough = stage > 0;
        if (_isBreakthrough)
            Debug.Log($"[罡气护体] Legend 突破 stage={stage} → 金色");
        RefreshVisual();
    }

    /// <summary>局内卡牌升级时触发。</summary>
    public override void OnLevelUp()
    {
        base.OnLevelUp();
        if (Level >= 5)
            Debug.Log($"[罡气护体] 五级羁绊突破 Lv.{Level} → 金色");
    }

    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx);
        EnsureVisual();
        RefreshVisual();
        PublishSkillCast();
    }

    public override void OnUnequip()
    {
        base.OnUnequip();
        DestroyVisual();
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped || _ctx.player == null) return;

        EnsureVisual();
        SyncVisualTransform();
        PulseVisual();

        if (damagePerSecond <= 0f || damageTickInterval <= 0f) return;

        _damageAccumulator += deltaTime;
        while (_damageAccumulator >= damageTickInterval)
        {
            _damageAccumulator -= damageTickInterval;
            ApplyDamageInRadius(damagePerSecond * damageTickInterval);
        }
    }

    // ── 视觉：多边形填充底盘 + 扩散脉冲环 ──

    private void EnsureVisual()
    {
        if (_ctx.player == null) return;

        if (_fieldRoot == null)
        {
            var rootGo = new GameObject("FieldGeneratorRoot");
            _fieldRoot = rootGo.transform;
            _fieldRoot.SetParent(_ctx.player, false);
            _fieldRoot.localPosition = Vector3.zero;
        }

        // ── 底盘 ──
        if (_discInstance == null)
        {
            EnsureDiscSprite();

            _discInstance = new GameObject("AuraDisc");
            _discInstance.transform.SetParent(_fieldRoot, false);
            _discInstance.transform.localPosition = Vector3.zero;

            _discSr = _discInstance.AddComponent<SpriteRenderer>();
            _discSr.sprite = _cachedDiscSprite;
            _discSr.sortingOrder = sortingOrder;
        }

        // ── 脉冲环 ──
        if (_pulseRings == null)
        {
            EnsureRingSprite();

            _pulseRings = new GameObject[PulseRingCount];
            _pulseSrs = new SpriteRenderer[PulseRingCount];

            for (int i = 0; i < PulseRingCount; i++)
            {
                var ringGo = new GameObject($"PulseRing_{i}");
                ringGo.transform.SetParent(_fieldRoot, false);
                ringGo.transform.localPosition = Vector3.zero;

                var sr = ringGo.AddComponent<SpriteRenderer>();
                sr.sprite = _cachedRingSprite;
                sr.sortingOrder = sortingOrder + 1;

                _pulseRings[i] = ringGo;
                _pulseSrs[i] = sr;
            }
        }
    }

    private void PulseVisual()
    {
        float r = radius;
        float intensity = Mathf.Clamp01(visualIntensity);

        bool breakthrough = _isBreakthrough || Level >= 5;

        // ── 底盘：多边形，尺寸 = 半径 × 2（贴图外接圆半径 = 0.5 世界单位） ──
        if (_discSr != null)
        {
            float discScale = r * 2f;
            _discInstance.transform.localScale = Vector3.one * discScale;

            Color discColor = breakthrough ? GoldAura : GreenAura;
            discColor.a = DiscBaseAlpha * intensity;
            _discSr.color = discColor;
        }

        // ── 脉冲环：多圈不同相位，从中心扩散 ──
        if (_pulseSrs != null)
        {
            float cycle = Mathf.Max(0.1f, pulseCycleDuration);
            float phaseOffset = cycle / PulseRingCount;
            Color ringBase = breakthrough ? GoldAura : GreenAura;

            for (int i = 0; i < PulseRingCount; i++)
            {
                float phase = (Time.time + i * phaseOffset) % cycle / cycle;
                float ringScale = Mathf.Lerp(0.05f * r * 2f, r * 2f, phase);
                _pulseRings[i].transform.localScale = Vector3.one * ringScale;

                Color rc = ringBase;
                rc.a = RingStartAlpha * (1f - phase) * intensity;
                _pulseSrs[i].color = rc;
            }
        }
    }

    private void RefreshVisual()
    {
        if (_discSr != null) _discSr.sortingOrder = sortingOrder;
        for (int i = 0; i < PulseRingCount; i++)
        {
            if (_pulseSrs != null && _pulseSrs[i] != null)
                _pulseSrs[i].sortingOrder = sortingOrder + 1;
        }
    }

    private void SyncVisualTransform()
    {
        if (_fieldRoot == null || _ctx.player == null) return;
        _fieldRoot.position = _ctx.player.position;
    }

    private void DestroyVisual()
    {
        if (_pulseRings != null)
        {
            for (int i = 0; i < _pulseRings.Length; i++)
            {
                if (_pulseRings[i] != null) Object.Destroy(_pulseRings[i]);
            }
            _pulseRings = null;
            _pulseSrs = null;
        }

        if (_discInstance != null)
        {
            Object.Destroy(_discInstance);
            _discInstance = null;
            _discSr = null;
        }

        if (_fieldRoot != null)
        {
            Object.Destroy(_fieldRoot.gameObject);
            _fieldRoot = null;
        }

        _damageAccumulator = 0f;
    }

    // ── 伤害 ──

    private void ApplyDamageInRadius(float damageAmount)
    {
        if (damageAmount <= 0f || _ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        float finalDmg = GetFinalDamage(damageAmount, out bool isCrit, out bool isPenetration);
        var ps = GetPlayerSkills();
        float effectiveRadius = radius * (ps != null ? ps.attackRangeMul : 1f);
        Vector2 center = _ctx.player.position;
        float radiusSq = effectiveRadius * effectiveRadius;

        bool prevQueries = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        int count = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = OverlapBuffer[i];
            if (col == null) continue;

            EnemyBase eb = col.GetComponent<EnemyBase>();
            if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
            if (eb == null || !eb.gameObject.CompareTag(_ctx.enemyTag)) continue;

            if (((Vector2)eb.transform.position - center).sqrMagnitude > radiusSq)
                continue;

            eb.TakeDamage(finalDmg, SkillId.FieldGenerator, isCrit, isPenetration);
        }

        Physics2D.queriesHitTriggers = prevQueries;
    }

    // ── 贴图缓存 ──

    /// <summary>
    /// 多边形填充底盘：正八边形，中心浓 → 边缘淡。
    /// 像素在"中心到多边形边的归一化距离"上做径向渐变。
    /// </summary>
    private static void EnsureDiscSprite()
    {
        if (_cachedDiscSprite != null && _cachedDiscSides == PolygonSides) return;
        _cachedDiscSides = PolygonSides;

        int size = 256;
        float half = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];

        int sides = PolygonSides;
        float anglePerSide = 2f * Mathf.PI / sides;
        float halfAngle = anglePerSide * 0.5f; // π/n
        float cosHalf = Mathf.Cos(halfAngle);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // 归一化到 [0, 2π)
                if (angle < 0f) angle += 2f * Mathf.PI;

                // 当前角度离最近顶点轴的角度差
                float snap = Mathf.Round(angle / anglePerSide) * anglePerSide;
                float delta = Mathf.Abs(angle - snap);
                if (delta > halfAngle) delta = anglePerSide - delta;

                // 该角度上到多边形边的距离
                float edgeDist = half * cosHalf / Mathf.Cos(delta);
                if (edgeDist <= 0f) edgeDist = half;

                float t = edgeDist > 0.001f ? dist / edgeDist : 1f; // 0=中心, 1=边缘
                // 中心浓、边缘淡，用幂曲线让衰减更明显
                float alpha = DiscBaseAlpha * Mathf.Pow(1f - Mathf.Clamp01(t), 1.8f);

                // 边缘外侧微微发光（防锯齿感）
                if (t > 1f && t < 1.06f)
                    alpha = Mathf.Lerp(alpha, 0f, (t - 1f) / 0.06f);

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _cachedDiscSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>脉冲环贴图：薄环，扩散时呈冲击波效果。</summary>
    private static void EnsureRingSprite()
    {
        if (_cachedRingSprite != null) return;

        int size = 128;
        float half = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        float outerR = half * 0.97f;
        float innerR = half * 0.73f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha;
                if (d < innerR)
                {
                    alpha = 0f;
                }
                else if (d > outerR)
                {
                    alpha = Mathf.Lerp(0.8f, 0f, Mathf.Clamp01((d - outerR) / (half * 0.03f)));
                }
                else
                {
                    float mid = (innerR + outerR) * 0.5f;
                    float halfBand = (outerR - innerR) * 0.5f;
                    alpha = 1f - Mathf.Abs(d - mid) / halfBand;
                }
                alpha = Mathf.Clamp01(alpha);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _cachedRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
