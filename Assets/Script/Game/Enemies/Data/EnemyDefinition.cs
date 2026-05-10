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

    [Header("数值")]
    [Tooltip("移动速度（EnemyAI.moveSpeed）")]
    public float moveSpeed = 2f;

    [Tooltip("最大血量")]
    public float maxHp = 10f;

    [Tooltip("接触/攻击伤害（预留）")]
    public float damage = 1f;

    [Tooltip("死亡时增加的击杀数（用于 GameLayer 显示）")]
    public int rewardKillCount = 1;
}

