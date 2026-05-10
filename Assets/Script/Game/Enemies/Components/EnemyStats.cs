using UnityEngine;

/// <summary>
/// EnemyStats
/// - 敌人基础属性组件（当前血量/最大血量）
/// - 先作为通用数据容器使用：后续做血条、受伤、精英/Boss倍率时可复用
/// </summary>
[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour
{
    [Tooltip("当前血量")]
    public float hp = 5f;

    [Tooltip("最大血量（用于血条显示）")]
    public float maxHp = 5f;
}

