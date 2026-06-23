using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 红点角标数据层（纯 C# 单例，不依赖 UI）。
/// 路径即层级（如 "battle/achievement"），父节点自动聚合子节点计数。
/// 叶子值变化 → 冒泡更新祖先 → 各发 RedDotChangedEvent。
/// </summary>
public class RedDotService
{
    private static RedDotService _instance;
    public static RedDotService Instance => _instance ??= new RedDotService();

    /// <summary>红点树节点。</summary>
    private class Node
    {
        public string key;
        public string parentKey;
        public List<Node> children = new List<Node>();
        public Func<int> computeFunc; // 叶子节点：计算函数；父节点：null
        public int cachedCount;
    }

    private readonly Dictionary<string, Node> _nodes = new Dictionary<string, Node>();
    private bool _inited;
    private CharacterCatalog _cachedCatalog;

    private RedDotService() { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => Instance.Init();

    // ── 初始化 ──

    public void Init()
    {
        if (_inited) return;
        _inited = true;

        BuildTree();
        SubscribeEvents();
        // 首次计算并发布
        RecomputeAndPublish();
    }

    private void BuildTree()
    {
        // 叶子节点定义（路径即层级）
        AddLeaf("battle/achievement", ComputeAchievementCount);
        AddLeaf("character",         ComputeCharacterCount);
        AddLeaf("shop",              ComputeShopCount);
    }

    /// <summary>注册叶子节点，自动创建父节点链。</summary>
    private void AddLeaf(string path, Func<int> computeFunc)
    {
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            // 根级叶子
            var node = GetOrCreate(path);
            node.computeFunc = computeFunc;
            return;
        }

        string parentPath = path.Substring(0, lastSlash);
        var parent = GetOrCreate(parentPath);
        var child = GetOrCreate(path);
        child.parentKey = parentPath;
        child.computeFunc = computeFunc;
        parent.children.Add(child);
    }

    private Node GetOrCreate(string key)
    {
        if (!_nodes.TryGetValue(key, out var node))
        {
            node = new Node { key = key };
            _nodes[key] = node;
        }
        return node;
    }

    private void SubscribeEvents()
    {
        EventBus.Subscribe<PlayerDataChangedEvent>(OnAnyDataChange);
        EventBus.Subscribe<HeroLevelUpEvent>(OnAnyDataChange);
        EventBus.Subscribe<HeroStageUpEvent>(OnAnyDataChange);
        EventBus.Subscribe<CharacterUnlockedEvent>(OnAnyDataChange);
        EventBus.Subscribe<GoldEarnedEvent>(OnAnyDataChange);
        EventBus.Subscribe<GoldSpentEvent>(OnAnyDataChange);
    }

    private void OnAnyDataChange<T>(T _) => RecomputeAndPublish();

    // ── 计算 ──

    private void RecomputeAndPublish()
    {
        if (_nodes.Count == 0) return;

        // 1. 叶子节点：调用 computeFunc
        foreach (var kv in _nodes)
        {
            var node = kv.Value;
            if (node.computeFunc != null)
            {
                int newVal = node.computeFunc();
                if (newVal != node.cachedCount)
                {
                    node.cachedCount = newVal;
                    EventBus.Publish(new RedDotChangedEvent { sourceKey = node.key });
                }
            }
        }

        // 2. 父节点：聚合子节点（递归冒泡）
        //    从叶子往上找父节点，逐层聚合
        var toUpdate = new HashSet<string>();
        foreach (var kv in _nodes)
        {
            var node = kv.Value;
            if (node.computeFunc != null && !string.IsNullOrEmpty(node.parentKey))
                toUpdate.Add(node.parentKey);
        }

        while (toUpdate.Count > 0)
        {
            var next = new HashSet<string>();
            foreach (var key in toUpdate)
            {
                if (!_nodes.TryGetValue(key, out var node) || node.children.Count == 0) continue;

                int sum = 0;
                for (int i = 0; i < node.children.Count; i++)
                    sum += node.children[i].cachedCount;

                if (sum != node.cachedCount)
                {
                    node.cachedCount = sum;
                    EventBus.Publish(new RedDotChangedEvent { sourceKey = node.key });
                }

                if (!string.IsNullOrEmpty(node.parentKey))
                    next.Add(node.parentKey);
            }
            toUpdate = next;
        }
    }

    // ── 公开查询 ──

    /// <summary>强制重算所有节点计数（场景加载后，数据源就绪时调用）。</summary>
    public void ForceRecompute() => RecomputeAndPublish();

    /// <summary>注入 CharacterCatalog（由 Home 场景的 HomeHubController 或其他持有者调用）。</summary>
    public void SetCharacterCatalog(CharacterCatalog catalog)
    {
        _cachedCatalog = catalog;
        RecomputeAndPublish();
    }

    /// <summary>获取指定节点的红点计数。0 表示无红点。</summary>
    public int GetCount(string sourceKey)
    {
        if (_nodes.TryGetValue(sourceKey, out var node))
            return node.cachedCount;
        return 0;
    }

    // ── 叶子计算函数 ──

    private static int ComputeAchievementCount()
    {
        var groups = AchievementService.Instance.GetAchievementGroups();
        int count = 0;
        for (int i = 0; i < groups.Count; i++)
        {
            var stages = groups[i].stages;
            for (int j = 0; j < stages.Count; j++)
            {
                if (stages[j].isCompleted && !stages[j].isClaimed)
                    count++;
            }
        }
        return count;
    }

    private int ComputeCharacterCount()
    {
        var catalog = LoadCharacterCatalog();
        if (catalog == null) return 0;

        var svc = PlayerProfileService.Instance;
        int count = 0;
        for (int i = 0; i < catalog.characters.Count; i++)
        {
            var def = catalog.characters[i];
            if (def == null) continue;

            // 1. 碎片解锁
            if (CharacterUnlockEvaluator.CanFragmentUnlock(def)) { count++; continue; }

            if (!CharacterUnlockEvaluator.IsUnlocked(def)) continue;

            // 2. 可升级（等级未满 + 金币够）
            int lv = svc.GetHeroLevel(def.characterId);
            int maxLv = svc.GetEffectiveMaxLevel(def.characterId, def.upgradeData);
            if (lv < maxLv && def.upgradeData != null)
            {
                int cost = def.upgradeData.GetCostForLevel(lv + 1);
                if (svc.CanAffordGold(cost)) { count++; continue; }
            }

            // 3. 可升阶
            if (svc.CanPromoteStage(def.characterId, def.upgradeData, out _, out _))
                count++;
        }
        return count;
    }

    private static int ComputeShopCount()
    {
        var items = ShopCatalog.Instance.NormalItems;
        if (items == null) return 0;
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var row = items[i];
            if (row == null) continue;
            // 免费或广告商品，已解锁且未售罄
            if (row.PriceType != 0 && row.PriceType != 3) continue;
            if (!ShopService.IsUnlocked(row)) continue;
            if (ShopService.IsSoldOut(row)) continue;
            count++;
        }
        return count;
    }

    /// <summary>加载 CharacterCatalog：优先缓存 → FindObjectOfType → Resources。</summary>
    private CharacterCatalog LoadCharacterCatalog()
    {
        if (_cachedCatalog != null) return _cachedCatalog;
        var cca = UnityEngine.Object.FindObjectOfType<CharacterConfigApplier>();
        if (cca != null && cca.characterCatalog != null)
            return cca.characterCatalog;
        return Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
    }
}
