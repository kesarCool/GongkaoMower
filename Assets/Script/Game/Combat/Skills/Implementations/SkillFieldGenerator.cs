using UnityEngine;

/// <summary>
/// 力场发生器：力场跟随玩家，范围内敌人持续受到 DPS（OverlapCircle 检测）。
/// </summary>
public class SkillFieldGenerator : SkillBase
{
    public float radius = 1.5f;
    public float damagePerSecond = 15f;
    public float damageTickInterval = 0.2f;
    public float visualIntensity = 1f;
    public float visualBaseDiameter = 2f;
    public int sortingOrder = 30;

    private readonly GameObject _fieldVisualPrefab;
    private Transform _fieldRoot;
    private GameObject _visualInstance;
    private float _damageAccumulator;

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

    public SkillFieldGenerator(GameObject fieldVisualPrefab)
    {
        Id = SkillId.FieldGenerator;
        _fieldVisualPrefab = fieldVisualPrefab;
    }

    public void ApplyRuntimeStats(
        float newRadius,
        float newDamagePerSecond,
        float newDamageTickInterval,
        float newVisualIntensity)
    {
        radius = Mathf.Max(0.2f, newRadius);
        damagePerSecond = Mathf.Max(0.01f, newDamagePerSecond);
        damageTickInterval = Mathf.Clamp(newDamageTickInterval, 0.05f, 1f);
        visualIntensity = Mathf.Max(0.1f, newVisualIntensity);
        RefreshVisual();
    }

    public override void OnEquip(SkillContext ctx)
    {
        base.OnEquip(ctx);
        EnsureVisual();
        RefreshVisual();
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

        if (damagePerSecond <= 0f || damageTickInterval <= 0f) return;

        _damageAccumulator += deltaTime;
        while (_damageAccumulator >= damageTickInterval)
        {
            _damageAccumulator -= damageTickInterval;
            ApplyDamageInRadius(damagePerSecond * damageTickInterval);
        }
    }

    private void EnsureVisual()
    {
        if (_fieldVisualPrefab == null || _ctx.player == null) return;

        if (_fieldRoot == null)
        {
            var rootGo = new GameObject("FieldGeneratorRoot");
            _fieldRoot = rootGo.transform;
            _fieldRoot.SetParent(_ctx.player, false);
            _fieldRoot.localPosition = Vector3.zero;
        }

        if (_visualInstance != null) return;

        _visualInstance = Object.Instantiate(_fieldVisualPrefab, _fieldRoot);
        _visualInstance.name = "FieldVisual";
        _visualInstance.transform.localPosition = Vector3.zero;
        _visualInstance.transform.localRotation = Quaternion.identity;

        ApplySortingOrder(_visualInstance, sortingOrder);

        var systems = _visualInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;
            ps.Play(true);
        }
    }

    private void SyncVisualTransform()
    {
        if (_fieldRoot == null || _ctx.player == null) return;
        _fieldRoot.position = _ctx.player.position;
    }

    private void RefreshVisual()
    {
        if (_visualInstance == null) return;

        float diameter = Mathf.Max(0.01f, visualBaseDiameter);
        float scale = (radius * 2f) / diameter;
        _visualInstance.transform.localScale = Vector3.one * scale;

        var systems = _visualInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            if (ps == null) continue;

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = visualIntensity;
        }
    }

    private void DestroyVisual()
    {
        if (_visualInstance != null)
        {
            Object.Destroy(_visualInstance);
            _visualInstance = null;
        }

        if (_fieldRoot != null)
        {
            Object.Destroy(_fieldRoot.gameObject);
            _fieldRoot = null;
        }

        _damageAccumulator = 0f;
    }

    private void ApplyDamageInRadius(float damageAmount)
    {
        if (damageAmount <= 0f || _ctx.player == null) return;
        if (string.IsNullOrEmpty(_ctx.enemyTag)) return;

        Vector2 center = _ctx.player.position;
        float radiusSq = radius * radius;

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

            eb.TakeDamage(damageAmount, SkillId.FieldGenerator);
        }

        Physics2D.queriesHitTriggers = prevQueries;
    }

    private static void ApplySortingOrder(GameObject root, int order)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sortingOrder = order;
        }
    }
}
