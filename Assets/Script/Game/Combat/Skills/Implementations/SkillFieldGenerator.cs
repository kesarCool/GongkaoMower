using UnityEngine;

/// <summary>罡气护体：原有周期 AOE + 独立的常驻罡环表现。</summary>
public class SkillFieldGenerator : SkillBase
{
    public float radius = 1.5f;
    public float damagePerSecond = 15f;
    public float damageTickInterval = .2f;
    public float visualIntensity = 1f;
    public int sortingOrder = -5;
    public float pulseCycleDuration = 1.2f;
    public GameObject fieldVisualPrefab;
    public float visualBaseDiameter = .875f;

    Transform _fieldRoot;
    FieldGeneratorPresentation _presentation;
    float _damageAccumulator;
    bool _isBreakthrough;
    static readonly Collider2D[] OverlapBuffer = new Collider2D[32];
    public bool IsBreakthroughVisual => _isBreakthrough || Level >= 5;

    public SkillFieldGenerator() { Id = SkillId.FieldGenerator; }
    public void ApplyRuntimeStats(float newRadius, float newDamagePerSecond, float newDamageTickInterval, float newVisualIntensity)
    {
        radius = Mathf.Max(.3f, newRadius);
        damagePerSecond = Mathf.Max(.01f, newDamagePerSecond);
        damageTickInterval = Mathf.Clamp(newDamageTickInterval, .05f, 1f);
        visualIntensity = Mathf.Max(.1f, newVisualIntensity);
        RefreshVisual();
    }
    public override void ApplyLegendBreakthrough(int stage)
    {
        _isBreakthrough = stage > 0;
        RefreshVisual();
    }
    public override void OnLevelUp() { base.OnLevelUp(); RefreshVisual(); }
    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx); EnsureVisual(); RefreshVisual(); PublishSkillCast();
    }
    public override void OnUnequip() { base.OnUnequip(); DestroyVisual(); }
    public override void Tick(float deltaTime)
    {
        if (!_equipped || _ctx.player == null) return;
        EnsureVisual();
        if (_presentation != null) { _presentation.Follow(_ctx.player); _presentation.TickVisual(deltaTime); }
        if (damagePerSecond <= 0 || damageTickInterval <= 0) return;
        _damageAccumulator += deltaTime;
        while (_damageAccumulator >= damageTickInterval)
        {
            _damageAccumulator -= damageTickInterval;
            ApplyDamageInRadius(damagePerSecond * damageTickInterval);
        }
    }
    void EnsureVisual()
    {
        if (_fieldRoot != null || _ctx.player == null) return;
        if (fieldVisualPrefab != null)
        {
            var root = Object.Instantiate(fieldVisualPrefab, _ctx.player, false);
            root.name = "FieldGeneratorRoot";
            _presentation = root.GetComponent<FieldGeneratorPresentation>();
            if (_presentation != null) _fieldRoot = root.transform;
            else { root.SetActive(false); Object.Destroy(root); }
        }
        if (_presentation == null)
        {
            _presentation = FieldGeneratorPresentation.CreateFallback(_ctx.player);
            if (_presentation != null) _fieldRoot = _presentation.transform;
        }
        RefreshVisual();
    }
    void RefreshVisual()
    {
        if (_presentation != null)
        {
            _presentation.Configure(radius, visualIntensity, pulseCycleDuration, IsBreakthroughVisual, sortingOrder, visualBaseDiameter);
            _presentation.Follow(_ctx.player);
        }
    }
    void DestroyVisual()
    {
        if (_fieldRoot != null) { _fieldRoot.gameObject.SetActive(false); Object.Destroy(_fieldRoot.gameObject); }
        _fieldRoot = null; _presentation = null; _damageAccumulator = 0;
    }

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
            if (_presentation != null) _presentation.NotifyDamageTick();
        }

        Physics2D.queriesHitTriggers = prevQueries;
    }

}
