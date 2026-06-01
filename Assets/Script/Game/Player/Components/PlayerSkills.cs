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

    [Header("技能表现：射线")]
    [Tooltip("射线可视化线段宽度（世界单位）")]
    public float beamVisualWidth = 0.08f;

    [Tooltip("射线 LineRenderer 的 Sorting Order（确保在角色/地面之上）")]
    public int beamSortingOrder = 200;

    [Tooltip("射线可视化持续时间（秒）。建议略小于 SkillDef 间隔，避免线段常亮。")]
    [Range(0.02f, 0.5f)]
    public float beamVisualDuration = 0.08f;

    [Header("肉鸽配置")]
    [Tooltip("技能上阵槽位上限（蛋壳特工队风格，默认5个）")]
    public const int MAX_SKILL_SLOTS = 5;
    public const int MAX_PASSIVE_SLOTS = 5;

    [Header("角色属性加成（由 CharacterConfigApplier 写入）")]
    [Tooltip("攻击力系数，乘到技能伤害上。")]
    public float attackMultiplier = 1f;
    [Tooltip("暴击率 (0~1)。")]
    [Range(0f, 1f)]
    public float critRate;
    [Tooltip("暴击倍率。")]
    public float critDamageMul = 2f;
    [Tooltip("穿透数。")]
    public int pierceCount;
    [Tooltip("穿透率 (0~1，每次命中触发穿透的概率)。")]
    [Range(0f, 1f)]
    public float pierceRate;

    /// <summary>结算暴击：返回伤害倍率（未暴击=1，暴击=critDamageMul）。</summary>
    public float EvaluateCrit()
    {
        if (critRate <= 0f) return 1f;
        return Random.value < critRate ? critDamageMul : 1f;
    }

    /// <summary>结算暴击并返回是否暴击（供 DamageFloatText 用）。</summary>
    public float EvaluateCrit(out bool isCrit)
    {
        if (critRate > 0f && Random.value < critRate)
        {
            isCrit = true;
            return critDamageMul;
        }
        isCrit = false;
        return 1f;
    }

    private readonly List<ISkill> _skills = new List<ISkill>(8);
    private readonly Dictionary<SkillId, ISkill> _skillById = new Dictionary<SkillId, ISkill>(8);

    // 被动技能独立槽位
    private readonly List<ISkill> _passives = new List<ISkill>(8);
    private readonly Dictionary<SkillId, ISkill> _passiveById = new Dictionary<SkillId, ISkill>(8);

    private PlayerController _pc;
    private SkillLineBeam2D _lineBeamSkill;
    private LineRenderer[] _beamLines;

    private const int MaxLineBeamVisuals = 8;

    private void Awake()
    {
        PassiveSkillRegistry.SetPlayer(gameObject);

        _pc = GetComponent<PlayerController>();
        if (_pc != null)
        {
            _pc.disableLegacyAutoShoot = disableLegacyAutoShoot;
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
        for (int i = 0; i < _passives.Count; i++)
            _passives[i].Tick(dt);

        // 射线可视化：在技能 Tick 之外做”超时隐藏”，避免一直亮着
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
        return skill.Level >= GetEffectiveMaxLevel(skill.Id, def);
    }

    /// <summary>
    /// 有效满级：拥有羁绊被动 → maxLevel；没有羁绊被动 → maxLevel - 1。
    /// 无羁绊配置（bondedPassiveId=None）时返回 maxLevel。
    /// </summary>
    public int GetEffectiveMaxLevel(SkillId id, SkillDefinitionBase def)
    {
        if (def == null) return 5;
        if (def.bondedPassiveId == SkillId.None) return def.maxLevel;
        return _passiveById.ContainsKey(def.bondedPassiveId) ? def.maxLevel : Mathf.Max(1, def.maxLevel - 1);
    }

    private SkillDefinitionBase GetDef(SkillId id)
    {
        return skillCatalog != null ? skillCatalog.Get(id) : null;
    }

    private SkillRuntimeBindings BuildRuntimeBindings()
    {
        return new SkillRuntimeBindings
        {
            beamVisualDuration = beamVisualDuration,
            configureLineBeam = EnsureBeamVisual,
        };
    }

    private void ApplyStatsFromDefinition(ISkill skill, SkillDefinitionBase def)
    {
        if (skill == null || def == null) return;
        def.ApplyStatsToSkill(skill, def.ClampLevel(skill.Level));
    }

    private ISkill CreateSkill(SkillId id)
    {
        SkillDefinitionBase def = GetDef(id);
        if (def == null)
        {
            Debug.LogWarning($"PlayerSkills: skillCatalog 缺少 SkillId={id} 的 SkillDefinition。");
            return null;
        }

        return def.CreateRuntimeSkill(BuildRuntimeBindings());
    }

    public bool TryAddSkill(SkillId id)
    {
        if (id == SkillId.None) return false;
        if (_skillById.ContainsKey(id)) return false;

        ISkill s = CreateSkill(id);
        if (s == null) return false;

        _skills.Add(s);
        _skillById[id] = s;

        if (s is SkillLineBeam2D beam)
            _lineBeamSkill = beam;

        // 已经在运行中时，立即装备
        if (isActiveAndEnabled)
            s.OnEquip(new SkillContext { player = transform, enemyTag = enemyTag });

        return true;
    }

    private void EnsureBeamVisual(SkillLineBeam2D skill)
    {
        if (skill == null) return;

        if (_beamLines == null || _beamLines.Length < MaxLineBeamVisuals)
        {
            Transform root = transform.Find("SkillBeamVisuals");
            if (root == null)
            {
                var rootGo = new GameObject("SkillBeamVisuals");
                rootGo.transform.SetParent(transform, false);
                rootGo.transform.localPosition = Vector3.zero;
                root = rootGo.transform;
            }

            _beamLines = new LineRenderer[MaxLineBeamVisuals];
            for (int i = 0; i < MaxLineBeamVisuals; i++)
            {
                Transform child = root.Find($"Beam_{i}");
                if (child == null)
                {
                    var go = new GameObject($"Beam_{i}");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = Vector3.zero;
                    child = go.transform;
                }

                _beamLines[i] = child.GetComponent<LineRenderer>();
                if (_beamLines[i] == null)
                    _beamLines[i] = child.gameObject.AddComponent<LineRenderer>();

                ConfigureBeamLineRenderer(_beamLines[i]);
            }
        }
        else
        {
            for (int i = 0; i < _beamLines.Length; i++)
                ConfigureBeamLineRenderer(_beamLines[i]);
        }

        skill.SetBeamLines(_beamLines);
    }

    private void ConfigureBeamLineRenderer(LineRenderer lr)
    {
        if (lr == null) return;

        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = 1f;
        lr.startWidth = beamVisualWidth;
        lr.endWidth = beamVisualWidth;
        // 颜色由 SkillLineBeam2D 按射线下标（红橙黄绿青蓝紫）逐条设置
        lr.sortingOrder = beamSortingOrder;
        lr.enabled = false;
        lr.positionCount = 2;

        if (lr.sharedMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh != null)
                lr.sharedMaterial = new Material(sh);
        }
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

    /// <summary>
    /// 清空所有已装备技能（OnUnequip + 清列表）。供 <see cref="CharacterConfigApplier"/> 重建技能前调用。
    /// </summary>
    public void ClearAllSkills()
    {
        for (int i = 0; i < _skills.Count; i++)
            _skills[i].OnUnequip();
        _skills.Clear();
        _skillById.Clear();
        _lineBeamSkill = null;
    }

    /// <summary>
    /// 以当前 <see cref="startingSkill"/> 重建初始技能列表并立即 Equip。
    /// 供 <see cref="CharacterConfigApplier"/> 在修改 startingSkill 后调用。
    /// </summary>
    public void RebuildFromStartingSkill()
    {
        ClearAllSkills();
        BuildInitialSkills();

        if (isActiveAndEnabled)
        {
            var ctx = new SkillContext { player = transform, enemyTag = enemyTag };
            for (int i = 0; i < _skills.Count; i++)
                _skills[i].OnEquip(ctx);
        }
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

    /// <summary>获取已装备技能的运行时实例（调试 / Gizmo 等）。</summary>
    public T GetEquippedSkill<T>(SkillId id) where T : class, ISkill
    {
        if (_skillById.TryGetValue(id, out var s))
            return s as T;
        return null;
    }

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

    #region 被动技能

    public int EquippedPassiveCount => _passives.Count;
    public bool HasPassiveEmptySlot => _passives.Count < MAX_PASSIVE_SLOTS;
    public bool IsPassiveFull => _passives.Count >= MAX_PASSIVE_SLOTS;
    public bool HasPassiveSkill(SkillId id) => _passiveById.ContainsKey(id);
    public int GetPassiveSkillLevel(SkillId id) => _passiveById.TryGetValue(id, out var s) ? s.Level : 0;

    public bool TryAddPassive(SkillId id)
    {
        if (id == SkillId.None) return false;
        if (_passiveById.ContainsKey(id)) return false;

        ISkill s = CreateSkill(id);
        if (s == null) return false;

        _passives.Add(s);
        _passiveById[id] = s;

        if (isActiveAndEnabled)
            s.OnEquip(new SkillContext { player = transform, enemyTag = enemyTag });

        return true;
    }

    public bool TryLevelUpPassive(SkillId id)
    {
        if (!_passiveById.TryGetValue(id, out var s)) return false;
        var def = GetDef(id);
        if (def != null && IsMaxLevel(s, def)) return false;

        s.OnLevelUp();
        ApplyStatsFromDefinition(s, def);
        return true;
    }

    public bool IsPassiveMaxLevel(SkillId id)
    {
        if (!_passiveById.TryGetValue(id, out var s)) return false;
        var def = GetDef(id);
        return IsMaxLevel(s, def);
    }

    public void GetEquippedPassiveIdsOrdered(List<SkillId> into)
    {
        if (into == null) return;
        into.Clear();
        for (int i = 0; i < _passives.Count; i++)
            into.Add(_passives[i].Id);
    }

    #endregion

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
