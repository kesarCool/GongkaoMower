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
        if (monsterId <= 0) return;
        BuildModules();
    }

    private void Update()
    {
        if (_modules.Count == 0) return;

        float dt = Time.deltaTime;
        foreach (BossSkillModule mod in _modules)
        {
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
        if (monster == null)
        {
            Debug.LogWarning($"[BossBrain] 未在 Monster 表找到 monsterId={monsterId}");
            return;
        }

        string[] skillTypes = ToArray(monster.element);
        string[] skillParams = ToArray(monster.elementNum);

        if (skillTypes.Length == 0)
        {
            Debug.Log($"[BossBrain] monsterId={monsterId} 未配置技能（element[] 为空），Boss 仅会追人。");
            return;
        }

        for (int i = 0; i < skillTypes.Length; i++)
        {
            string type = skillTypes[i]?.Trim();
            if (string.IsNullOrEmpty(type)) continue;

            string rawParams = i < skillParams.Length ? skillParams[i] : string.Empty;
            BossSkillModule mod = CreateModule(type);
            if (mod != null)
            {
                mod.Init(rawParams, this);
                _modules.Add(mod);
                Debug.Log($"[BossBrain] monsterId={monsterId} 加载技能: {type} params={rawParams}");
            }
            else
            {
                Debug.LogWarning($"[BossBrain] monsterId={monsterId} 未知技能类型: {type}");
            }
        }
#endif
    }

    private static BossSkillModule CreateModule(string type)
    {
        switch (type)
        {
            case "homingKnife": return new HomingKnifeModule();
            case "dash":         return new DashModule();
            // 后续扩展：
            // case "bladeBurst":   return new BladeBurstModule();
            // case "clone":        return new CloneModule();
            // case "summon":       return new SummonModule();
            // case "revive":       return new ReviveModule();
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
