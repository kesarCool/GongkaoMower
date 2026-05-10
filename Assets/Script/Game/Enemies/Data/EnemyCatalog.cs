using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyCatalog
/// - 在 Inspector 里维护“怪物ID -> EnemyDefinition(包含 prefab/数值/资源引用)”的列表
/// - 供 Spawner 通过怪物ID生成使用
///
/// 用法：
/// 1) 场景里建一个空物体 EnemyCatalog，挂此脚本
/// 2) 在 entries 列表中配置每个怪的 id、prefab、速度、血量、子弹prefab等
/// </summary>
[DisallowMultipleComponent]
public class EnemyCatalog : MonoBehaviour
{
    [Tooltip("怪物配置列表（按ID生成时会从这里查）")]
    public List<EnemyDefinition> entries = new List<EnemyDefinition>();

    private Dictionary<int, EnemyDefinition> _map;

    private void Awake()
    {
        BuildMap();
    }

    private void OnValidate()
    {
        // Editor 下改了 entries，尽量保持映射可用（避免运行时才发现重复ID）
        BuildMap();
    }

    private void BuildMap()
    {
        _map = new Dictionary<int, EnemyDefinition>();
        for (int i = 0; i < entries.Count; i++)
        {
            EnemyDefinition def = entries[i];
            if (def == null) continue;

            if (_map.ContainsKey(def.id))
            {
                Debug.LogWarning($"EnemyCatalog: 发现重复怪物ID={def.id}，后者会覆盖前者。");
            }
            _map[def.id] = def;
        }
    }

    public bool TryGet(int id, out EnemyDefinition def)
    {
        if (_map == null) BuildMap();
        return _map.TryGetValue(id, out def);
    }
}

