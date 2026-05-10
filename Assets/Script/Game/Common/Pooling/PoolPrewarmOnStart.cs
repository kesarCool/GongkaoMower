using UnityEngine;

/// <summary>
/// 开局预热对象池：在 Awake/Start 时预创建指定数量的对象，避免运行时尖峰。
/// 挂在 PlayerSkills、GameLayer 或任意场景管理对象上均可。
/// </summary>
public class PoolPrewarmOnStart : MonoBehaviour
{
    [System.Serializable]
    public class PrewarmEntry
    {
        [Tooltip("要预热的预制体（必须挂有会被池化的组件，如 Bullet/EnergyPickup）")]
        public GameObject prefab;

        [Tooltip("预创建数量（建议按峰值并发量估算）")]
        [Min(0)]
        public int count = 50;

        [Tooltip("该预制体在池中最大闲置数（超过会直接销毁）")]
        [Min(1)]
        public int maxInactive = 256;
    }

    [Header("预热配置")]
    public PrewarmEntry[] entries;

    [Header("时机")]
    [Tooltip("true：在 Awake 预热；false：在 Start 预热。建议 Awake 确保其他脚本在 Start 时可直接取到池内对象。")]
    public bool prewarmInAwake = true;

    private void Awake()
    {
        if (prewarmInAwake) PrewarmAll();
    }

    private void Start()
    {
        if (!prewarmInAwake) PrewarmAll();
    }

    private void PrewarmAll()
    {
        if (entries == null || entries.Length == 0) return;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (e.prefab == null) continue;
            if (e.count <= 0) continue;

            GameObjectPool.Prewarm(e.prefab, e.count, e.maxInactive);
        }
    }
}
