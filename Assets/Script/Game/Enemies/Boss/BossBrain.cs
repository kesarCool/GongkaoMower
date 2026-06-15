using System;
using System.Collections.Generic;
using ProtoTable;
using UnityEngine;

/// <summary>
/// 通用 Boss 大脑：从 Excel Monster.element[] 读技能列表，组装 SkillModule。
/// 所有 Boss Prefab 挂同一个 BossBrain，差异只在 Excel 配表。
/// </summary>
[DisallowMultipleComponent]
public class BossBrain : MonoBehaviour
{
    [Header("Boss 标识（留空则从 EnemyBase 取）")]
    public int monsterId;

    [Header("技能预制体库")]
    [Tooltip("每个技能模块按 key 从这里找预制体。例如 key=HomingBullet → HomingBullet.prefab")]
    public BossPrefabEntry[] prefabRegistry;

    [Header("技能 Inspector 配置（无需 Excel）")]
    [Tooltip("type=homingKnife/dash/bladeBurst/zone, params=参数逗号分隔")]
    public List<InspectorSkillEntry> inspectorSkills = new List<InspectorSkillEntry>();

    /// <summary>Boss 正在执行技能时 EnemyAI 暂停追人。</summary>
    public bool IsBusy { get; set; }

    private readonly List<BossSkillModule> _modules = new List<BossSkillModule>(4);

    private void Awake()
    {
        if (monsterId <= 0)
        {
            EnemyBase eb = GetComponent<EnemyBase>();
            if (eb != null) monsterId = eb.EnemyId;
        }
    }

    private void Start()
    {
        if (monsterId <= 0)
        {
            // 公用 prefab 的 monsterId=0，需等 InitFromDefinition 注入 ID
            Debug.LogWarning($"[BossBrain] monsterId=0，等待 EnemyBase.InitFromDefinition 注入……");
            return;
        }
        BuildModules();
    }

    /// <summary>由 EnemyBase.InitFromDefinition 或 SpawnerWaves 在赋完 EnemyId 后调用。</summary>
    public void OnEnemyDataReady()
    {
        if (monsterId <= 0)
        {
            EnemyBase eb = GetComponent<EnemyBase>();
            if (eb != null) monsterId = eb.EnemyId;
        }
        if (monsterId <= 0) return;
        BuildModules();
    }

    private void OnDestroy()
    {
        // 通知 ReviveModule 清理 OnDied 监听
        foreach (var mod in _modules)
        {
            if (mod is ReviveModule rv)
                rv.OnBossDestroyed();
        }
    }

    private void Update()
    {
        if (_modules.Count == 0) return;

        float dt = Time.deltaTime;
        foreach (BossSkillModule mod in _modules)
        {
            if (mod.IsPassive) continue;
            mod.Tick(dt);
            if (mod.CanTrigger() && !IsBusy)
            {
                mod.Execute();
            }
        }
    }

    /// <summary>模块通过 key 查找预制体。</summary>
    public GameObject FindPrefab(string key)
    {
        if (prefabRegistry == null) return null;
        for (int i = 0; i < prefabRegistry.Length; i++)
        {
            if (prefabRegistry[i] != null &&
                string.Equals(prefabRegistry[i].key, key, StringComparison.OrdinalIgnoreCase))
                return prefabRegistry[i].prefab;
        }
        return null;
    }

    private void BuildModules()
    {
        _modules.Clear();

#if USE_FB_TABLE
        Monster monster = FindMonsterRow(monsterId);
        if (monster != null)
        {
            string[] skillTypes = ToArray(monster.element);
            string[] skillParams = ToArray(monster.elementNum);

            if (skillTypes.Length > 0)
            {
                for (int i = 0; i < skillTypes.Length; i++)
                {
                    string type = skillTypes[i]?.Trim();
                    if (string.IsNullOrEmpty(type)) continue;
                    string rawParams = i < skillParams.Length ? skillParams[i] : string.Empty;
                    AddModule(type, rawParams, monsterId);
                }
                return;
            }
        }
        Debug.Log($"[BossBrain] monsterId={monsterId} 表配置为空，回退 Inspector 配置");
#endif
        // Inspector 兜底（无需 Excel 配表）
        foreach (var entry in inspectorSkills)
        {
            if (string.IsNullOrEmpty(entry.type)) continue;
            AddModule(entry.type, entry.params_, monsterId);
        }
    }

    private void AddModule(string type, string rawParams, int mid)
    {
        BossSkillModule mod = CreateModule(type);
        if (mod != null)
        {
            mod.moduleType = type;
            mod.Init(rawParams, this);
            _modules.Add(mod);
            Debug.Log($"[BossBrain] monsterId={mid} 加载技能: {type} interval={mod.interval}s cooldown={mod.cooldown}s params={rawParams}");
        }
        else Debug.LogWarning($"[BossBrain] monsterId={mid} 未知技能类型: {type}");
    }

    /// <summary>移除所有不在 keepTypes 中的技能模块。供 CloneModule 筛选克隆体技能。</summary>
    public void RemoveAllModulesExcept(HashSet<string> keepTypes)
    {
        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            if (!keepTypes.Contains(_modules[i].moduleType))
                _modules.RemoveAt(i);
        }
    }

    private static BossSkillModule CreateModule(string type)
    {
        switch (type)
        {
            case "homingKnife": return new HomingKnifeModule();
            case "dash":         return new DashModule();
            case "bladeBurst":   return new BladeBurstModule();
            case "zone":         return new ZoneModule();
            case "resist":       return new ResistModule();
            case "clone":        return new CloneModule();
            case "summon":       return new SummonModule();
            case "revive":       return new ReviveModule();
            default: return null;
        }
    }

#if USE_FB_TABLE
    private static Monster FindMonsterRow(int id)
    {
        if (TableManager.Instance == null) return null;
        var dict = TableManager.Instance.GetTable<Monster>();
        if (dict == null) return null;
        foreach (var kv in dict)
        {
            if (kv.Value is Monster m && (m.monsterId == id || m.ID == id))
                return m;
        }
        return null;
    }

    private static string[] ToArray(FlatBufferArray<string> fbArray)
    {
        if (fbArray == null) return Array.Empty<string>();
        string[] arr = new string[fbArray.Length];
        for (int i = 0; i < fbArray.Length; i++)
            arr[i] = fbArray[i];
        return arr;
    }
#endif
}

[Serializable]
public class BossPrefabEntry
{
    public string key;
    public GameObject prefab;
}

[System.Serializable]
public class InspectorSkillEntry
{
    public string type;
    public string params_ = string.Empty;
}
