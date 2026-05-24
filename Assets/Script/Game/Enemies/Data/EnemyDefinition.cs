using System;
using UnityEngine;

/// <summary>
/// 怪物配置数据（第一版走 Inspector 配置；后期可以替换为表格/JSON/ScriptableObject 读入后填充）
/// </summary>
[Serializable]
public class EnemyDefinition
{
    [Header("基础信息")]
    [Tooltip("怪物ID（全局唯一，用于生成/查找配置）")]
    public int id = 1;

    [Tooltip("怪物名称（用于显示/调试）")]
    public string enemyName = "Enemy";

    [Header("资源引用")]
    [Tooltip("怪物预制体（按ID生成时实例化这个）")]
    public GameObject prefab;

    [Tooltip("怪物外观资源（可选）。如果你的prefab里已经固定了SpriteRenderer，可不填。")]
    public Sprite sprite;

    [Tooltip("远程怪使用的子弹预制体（可选）。由派生类 EnemyRanged 使用。")]
    public GameObject bulletPrefab;

    [Header("数值（已废弃——攻血速统一从 Excel 表读取）")]
    [HideInInspector] public float moveSpeed = 2f;
    [HideInInspector] public float maxHp = 10f;
    [HideInInspector] public float damage = 1f;
    [HideInInspector] public int rewardKillCount = 1;
}

