using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerSkills
/// - 管理玩家已装备技能（Tick 驱动）
/// - 第 2 阶段：先内置 3 个示例技能（子弹/射线/环绕刀）
/// - 与能量选卡对接：订阅 CardSelectionTriggeredEvent（占位：轮流升级技能）
/// </summary>
[DisallowMultipleComponent]
public class PlayerSkills : MonoBehaviour
{
    [Header("技能目录（肉鸽卡池）")]
    public SkillCatalog skillCatalog;

    [Tooltip("角色初始技能（进入游戏时自带，等级=1）")]
    public SkillId startingSkill = SkillId.AutoProjectile;

    [Header("与旧 PlayerController 协作")]
    [Tooltip("勾选后：禁用 PlayerController 内的自动射击，改由 SkillAutoProjectile 驱动")]
    public bool disableLegacyAutoShoot = true;

    [Tooltip("索敌用的敌人 Tag（与 Bullet / PlayerController 保持一致）")]
    public string enemyTag = "monster";

    [Header("技能参数：自动子弹")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    [Tooltip("当没有 SkillCatalog 或未配置 AutoProjectile 定义时使用的回退值")]
    public float autoProjectileInterval = 0.5f;

    [Header("技能参数：射线")]
    [Tooltip("当没有 SkillCatalog 或未配置 LineBeam 定义时使用的回退值")]
    public float beamLength = 8f;
    [Tooltip("当没有 SkillCatalog 或未配置 LineBeam 定义时使用的回退值")]
    public float beamDamage = 2f;
    [Tooltip("当没有 SkillCatalog 或未配置 LineBeam 定义时使用的回退值")]
    public float beamInterval = 0.8f;
    public LayerMask beamHitMask = ~0;

    [Tooltip("射线可视化线段宽度（世界单位）")]
    public float beamVisualWidth = 0.08f;

    [Tooltip("射线可视化颜色")]
    public Color beamVisualColor = new Color(0.2f, 0.85f, 1f, 0.95f);

    [Tooltip("射线 LineRenderer 的 Sorting Order（确保在角色/地面之上）")]
    public int beamSortingOrder = 200;

    [Tooltip("射线可视化持续时间（秒）。建议略小于 beamInterval，避免线段常亮。")]
    [Range(0.02f, 0.5f)]
    public float beamVisualDuration = 0.08f;

    [Header("技能参数：环绕刀")]
    [Tooltip("环绕刀片 Sprite（拖入工程内已导入的美术）；为空则用程序化白块")]
    public Sprite bladeSprite;
    [Tooltip("刀片 SpriteRenderer.sortingOrder，被地面/角色挡住时调大")]
    public int bladeSpriteSortingOrder = 50;
    [Tooltip("与美术色相乘；保留原画用白色")]
    public Color bladeSpriteTint = Color.white;
    [Tooltip("刀片整体缩放")]
    public float bladeVisualScale = 1f;
    public int bladeCount = 3;
    public float bladeOrbitRadius = 1.2f;
    public float bladeRotateSpeed = 180f;
    public float bladeDamagePerTick = 1f;
    public float bladeTickInterval = 0.15f;

    [Header("肉鸽配置")]
    [Tooltip("技能上阵槽位上限（蛋壳特工队风格，默认5个）")]
    public const int MAX_SKILL_SLOTS = 5;

    private readonly List<ISkill> _skills = new List<ISkill>(8);
    private readonly Dictionary<SkillId, ISkill> _skillById = new Dictionary<SkillId, ISkill>(8);

    private PlayerController _pc;
    private SkillLineBeam2D _lineBeamSkill;
    private LineRenderer _beamLine;

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        if (_pc != null)
        {
            _pc.disableLegacyAutoShoot = disableLegacyAutoShoot;
            if (bulletPrefab == null && _pc.bulletPrefab != null)
                bulletPrefab = _pc.bulletPrefab;
            if (Mathf.Approximately(bulletSpeed, 10f) && !Mathf.Approximately(_pc.bulletSpeed, bulletSpeed))
                bulletSpeed = _pc.bulletSpeed;
            if (string.IsNullOrWhiteSpace(enemyTag))
                enemyTag = _pc.enemyTag;
        }

        BuildInitialSkills();
    }

    private void Start()
    {
        var ctx = new SkillContext { player = transform, enemyTag = enemyTag };
        for (int i = 0; i < _skills.Count; i++)
            _skills[i].OnEquip(ctx);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _skills.Count; i++)
            _skills[i].OnUnequip();
        _skills.Clear();
        _skillById.Clear();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _skills.Count; i++)
            _skills[i].Tick(dt);

        // 射线可视化：在技能 Tick 之外做“超时隐藏”，避免一直亮着
        if (_lineBeamSkill != null)
            _lineBeamSkill.TickVisual();
    }

    private void BuildInitialSkills()
    {
        _skills.Clear();
        _skillById.Clear();

        if (startingSkill != SkillId.None)
            TryAddSkill(startingSkill);
    }

    private bool IsMaxLevel(ISkill skill, SkillDefinitionBase def)
    {
        if (skill == null) return true;
        int max = def != null ? Mathf.Max(1, def.maxLevel) : 5;
        return skill.Level >= max;
    }

    private SkillDefinitionBase GetDef(SkillId id)
    {
        return skillCatalog != null ? skillCatalog.Get(id) : null;
    }

    private void ApplyStatsFromDefinition(ISkill skill, SkillDefinitionBase def)
    {
        if (skill == null) return;
        if (def == null) return;

        int lv = def.ClampLevel(skill.Level);

        switch (def)
        {
            case AutoProjectileSkillDefinition ap:
            {
                if (skill is SkillAutoProjectile s)
                {
                    s.interval = Mathf.Max(0.05f, ap.IntervalAt(lv));
                    s.projectileCount = Mathf.Max(1, ap.ProjectileCountAt(lv));
                    s.spreadDegrees = Mathf.Max(0f, ap.spreadDegrees);
                }
                break;
            }
            case LineBeamSkillDefinition lb:
            {
                if (skill is SkillLineBeam2D s)
                {
                    s.interval = Mathf.Max(0.05f, lb.IntervalAt(lv));
                    s.damage = Mathf.Max(0.01f, lb.DamageAt(lv));
                    s.beamCount = Mathf.Max(1, lb.BeamCountAt(lv));
                    s.spreadDegrees = Mathf.Max(0f, lb.spreadDegrees);
                }
                break;
            }
            case OrbitingBladesSkillDefinition ob:
            {
                if (skill is SkillOrbitingBlades s)
                {
                    int count = Mathf.Max(1, ob.BladeCountAt(lv));
                    s.ApplyRuntimeStats(
                        count,
                        ob.OrbitRadiusAt(lv),
                        ob.RotateSpeedAt(lv),
                        ob.DamagePerTickAt(lv),
                        ob.TickIntervalAt(lv));
                }
                break;
            }
        }
    }

    private ISkill CreateSkill(SkillId id)
    {
        SkillDefinitionBase def = GetDef(id);

        switch (id)
        {
            case SkillId.AutoProjectile:
            {
                if (bulletPrefab == null) return null;
                var s = new SkillAutoProjectile(bulletPrefab, bulletSpeed, autoProjectileInterval);
                ApplyStatsFromDefinition(s, def);
                return s;
            }
            case SkillId.LineBeam:
            {
                var s = new SkillLineBeam2D(beamLength, beamDamage, beamInterval, beamHitMask);
                s.visualDuration = beamVisualDuration;
                EnsureBeamVisual(s);
                ApplyStatsFromDefinition(s, def);
                return s;
            }
            case SkillId.OrbitingBlades:
            {
                // Visual 优先取 PlayerSkills Inspector 上的覆盖（方便角色/皮肤差异）
                Sprite sprite = bladeSprite;
                Color tint = bladeSpriteTint;
                int order = bladeSpriteSortingOrder;
                float scale = bladeVisualScale;

                if (def is OrbitingBladesSkillDefinition ob)
                {
                    if (sprite == null) sprite = ob.bladeSprite;
                    if (tint == default) tint = ob.bladeTint;
                    if (order == 50) order = ob.sortingOrder;
                    if (Mathf.Approximately(scale, 1f)) scale = ob.visualScale;
                }

                int lv = 1;
                int count = bladeCount;
                float r = bladeOrbitRadius;
                float rot = bladeRotateSpeed;
                float dmg = bladeDamagePerTick;
                float tick = bladeTickInterval;

                if (def is OrbitingBladesSkillDefinition obd)
                {
                    count = obd.BladeCountAt(lv);
                    r = obd.OrbitRadiusAt(lv);
                    rot = obd.RotateSpeedAt(lv);
                    dmg = obd.DamagePerTickAt(lv);
                    tick = obd.TickIntervalAt(lv);
                }

                return new SkillOrbitingBlades(sprite, count, r, rot, dmg, tick, order, tint, scale);
            }
        }

        return null;
    }

    public bool TryAddSkill(SkillId id)
    {
        if (id == SkillId.None) return false;
        if (_skillById.ContainsKey(id)) return false;

        ISkill s = CreateSkill(id);
        if (s == null) return false;

        _skills.Add(s);
        _skillById[id] = s;

        if (id == SkillId.LineBeam)
            _lineBeamSkill = s as SkillLineBeam2D;

        // 已经在运行中时，立即装备
        if (isActiveAndEnabled)
            s.OnEquip(new SkillContext { player = transform, enemyTag = enemyTag });

        return true;
    }

    private void EnsureBeamVisual(SkillLineBeam2D skill)
    {
        if (skill == null) return;

        if (_beamLine == null)
        {
            Transform existing = transform.Find("SkillBeamVisual");
            if (existing != null)
                _beamLine = existing.GetComponent<LineRenderer>();
        }

        if (_beamLine == null)
        {
            GameObject go = new GameObject("SkillBeamVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _beamLine = go.AddComponent<LineRenderer>();
        }

        _beamLine.useWorldSpace = true;
        _beamLine.loop = false;
        _beamLine.widthMultiplier = 1f;
        _beamLine.startWidth = beamVisualWidth;
        _beamLine.endWidth = beamVisualWidth;
        _beamLine.startColor = beamVisualColor;
        _beamLine.endColor = beamVisualColor;
        _beamLine.sortingOrder = beamSortingOrder;
        _beamLine.enabled = false;
        _beamLine.positionCount = 2;

        // 尽量使用 Sprites/Default（2D 项目通常存在）；失败则退回 Unlit/Color
        if (_beamLine.sharedMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh != null)
                _beamLine.sharedMaterial = new Material(sh);
        }

        skill.SetBeamLine(_beamLine);
    }

    /// <summary>
    /// 供后续肉鸽 UI 调用：按技能 ID 升级
    /// </summary>
    public bool TryLevelUp(SkillId id)
    {
        if (_skillById.TryGetValue(id, out var s))
        {
            SkillDefinitionBase def = GetDef(id);
            if (def != null && IsMaxLevel(s, def)) return false;

            s.OnLevelUp();
            ApplyStatsFromDefinition(s, def);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 无 UI 占位或工具用：按装备顺序下标升级（与 RoguelikeCardManager 轮询配合）
    /// </summary>
    public void UpgradeByEquippedIndex(int index)
    {
        if (index < 0 || index >= _skills.Count) return;
        var s = _skills[index];
        s.OnLevelUp();
        ApplyStatsFromDefinition(s, GetDef(s.Id));
    }

    #region 肉鸽选卡接口

    /// <summary>
    /// 是否有空槽位可装备新技能
    /// </summary>
    public bool HasEmptySlot => _skills.Count < MAX_SKILL_SLOTS;

    /// <summary>
    /// 当前已装备技能数量
    /// </summary>
    public int EquippedSkillCount => _skills.Count;

    /// <summary>
    /// 槽位是否已满
    /// </summary>
    public bool IsFull => _skills.Count >= MAX_SKILL_SLOTS;

    /// <summary>
    /// 检查是否拥有某技能
    /// </summary>
    public bool HasSkill(SkillId id) => _skillById.ContainsKey(id);

    /// <summary>
    /// 获取技能当前等级（0=未拥有）
    /// </summary>
    public int GetSkillLevel(SkillId id)
    {
        if (_skillById.TryGetValue(id, out var s))
            return s.Level;
        return 0;
    }

    /// <summary>
    /// 检查技能是否已满级
    /// </summary>
    public bool IsMaxLevel(SkillId id)
    {
        if (!_skillById.TryGetValue(id, out var s)) return false;
        var def = GetDef(id);
        return IsMaxLevel(s, def);
    }

    /// <summary>已装备技能 ID（上阵顺序）。</summary>
    public void GetEquippedSkillIdsOrdered(List<SkillId> into)
    {
        if (into == null) return;
        into.Clear();
        for (int i = 0; i < _skills.Count; i++)
            into.Add(_skills[i].Id);
    }

    /// <summary>
    /// 槽位已满且所有技能都满级
    /// </summary>
    public bool AllSlotsFullAndMaxLevel
    {
        get
        {
            if (!IsFull) return false;
            foreach (var s in _skills)
            {
                var def = GetDef(s.Id);
                if (!IsMaxLevel(s, def)) return false;
            }
            return true;
        }
    }

    #endregion
}
