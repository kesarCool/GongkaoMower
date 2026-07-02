using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Guest 单存档：关卡星级（历史最高）、最短通关时间、击杀（历史最高）。</summary>
public sealed class PlayerProfileService
{
    public const string SaveKey = "player_save_v1";
    public const string GuestIdKey = "guest_player_id";

    private static PlayerProfileService _instance;
    public static PlayerProfileService Instance => _instance ??= new PlayerProfileService();

    private readonly ISaveStorage _storage;
    private readonly Dictionary<int, LevelProgressEntry> _levels = new Dictionary<int, LevelProgressEntry>();
    private PlayerSaveData _data;
    private bool _loaded;

    public string PlayerId => _data?.playerId ?? string.Empty;
    public bool IsLoaded => _loaded;
    public PlayerSaveData Data { get { if (!_loaded) LoadOrCreate(); return _data; } }

    /// <summary>存档被篡改/损坏，等待 UI 就绪后弹提示。</summary>
    public bool PendingCorruptionAlert { get; private set; }

    /// <summary>消费挂起的损坏提示标记（由 UI 层在 Start 时调用）。</summary>
    public void ConsumeCorruptionAlert()
    {
        if (!PendingCorruptionAlert) return;
        PendingCorruptionAlert = false;

        GameErrorPresenter.Show(GameErrorCodes.SaveLoadFailed);
        if (UIManager.Instance != null)
            UIManager.Instance.ShowAlert("数据异常", "存档校验失败，数据已重置。", null);
    }

    public void MarkDirtyAndSave() { if (_data != null) Persist(); }

    // ── 货币 ──

    public int Gold { get { if (!_loaded) LoadOrCreate(); return _data?.gold ?? 0; } }
    public int Diamond { get { if (!_loaded) LoadOrCreate(); return _data?.diamond ?? 0; } }

    /// <summary>增加金币（正数增加，负数扣除）。返回变化后的余额。</summary>
    public int AddGold(int delta)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return 0;
        _data.gold = Mathf.Max(0, _data.gold + delta);
        Persist();

        // 成就系统：金币收入事件
        if (delta > 0)
            EventBus.Publish(new GoldEarnedEvent { amount = delta });

        return _data.gold;
    }

    /// <summary>增加钻石（正数增加，负数扣除）。返回变化后的余额。</summary>
    public int AddDiamond(int delta)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return 0;
        _data.diamond = Mathf.Max(0, _data.diamond + delta);
        Persist();

        // 成就系统：钻石收入事件
        if (delta > 0)
            EventBus.Publish(new DiamondEarnedEvent { amount = delta });

        return _data.diamond;
    }

    /// <summary>金币是否足够消费。</summary>
    public bool CanAffordGold(int cost) => Gold >= cost;

    /// <summary>消费金币，不足则返回 false。</summary>
    public bool SpendGold(int cost)
    {
        if (!CanAffordGold(cost)) return false;
        AddGold(-cost);

        // 成就系统：金币消费事件
        EventBus.Publish(new GoldSpentEvent { amount = cost });

        return true;
    }

    /// <summary>钻石是否足够消费。</summary>
    public bool CanAffordDiamond(int cost) => Diamond >= cost;

    /// <summary>消费钻石，不足则返回 false。</summary>
    public bool SpendDiamond(int cost)
    {
        if (!CanAffordDiamond(cost)) return false;
        AddDiamond(-cost);

        // 成就系统：钻石消费事件
        EventBus.Publish(new DiamondSpentEvent { amount = cost });

        return true;
    }

    // ── 通用物品 ──

    /// <summary>增加物品数量（ID=1 金币自动同步 gold 字段）。</summary>
    public void AddItem(int itemId, int count)
    {
        if (count <= 0) return;
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;

        // 金币/钻石走专用方法（快取 + HUD 兼容 + 事件发布）
        if (itemId == 1) { AddGold(count); return; }
        if (itemId == 2) { AddDiamond(count); return; }

        // 角色碎片：同步写入 characterFragmentKeys/Values，供角色面板查询。
        // 不 return，继续写入 itemIds/itemCounts，确保背包面板可见。
        TryRouteFragment(itemId, count);

        // 通用物品：线性查找 + 更新
        if (_data.itemIds == null) _data.itemIds = System.Array.Empty<int>();
        if (_data.itemCounts == null) _data.itemCounts = System.Array.Empty<int>();

        int idx = System.Array.IndexOf(_data.itemIds, itemId);
        if (idx >= 0 && idx < _data.itemCounts.Length)
        {
            _data.itemCounts[idx] = Mathf.Max(0, _data.itemCounts[idx] + count);
        }
        else
        {
            // 扩容
            var newIds = new int[_data.itemIds.Length + 1];
            var newCounts = new int[_data.itemCounts.Length + 1];
            System.Array.Copy(_data.itemIds, newIds, _data.itemIds.Length);
            System.Array.Copy(_data.itemCounts, newCounts, _data.itemCounts.Length);
            newIds[newIds.Length - 1] = itemId;
            newCounts[newCounts.Length - 1] = count;
            _data.itemIds = newIds;
            _data.itemCounts = newCounts;
        }

        Persist();
    }

    /// <summary>获取物品持有数量（ID=1 金币走 gold 字段）。</summary>
    public int GetItemCount(int itemId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return 0;

        if (itemId == 1) return _data.gold;
        if (itemId == 2) return _data.diamond;

        if (_data.itemIds == null || _data.itemCounts == null) return 0;
        int idx = System.Array.IndexOf(_data.itemIds, itemId);
        if (idx < 0 || idx >= _data.itemCounts.Length) return 0;
        return _data.itemCounts[idx];
    }

    // ── 英雄升级 ──

    /// <summary>获取英雄等级（未升级过返回 1）。</summary>
    public int GetHeroLevel(string characterId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data?.heroUpgrades == null) return 1;

        for (int i = 0; i < _data.heroUpgrades.Length; i++)
        {
            var e = _data.heroUpgrades[i];
            if (e != null && e.characterId == characterId)
                return Mathf.Max(1, e.level);
        }
        return 1;
    }

    /// <summary>设置英雄等级并持久化。</summary>
    private void SetHeroLevel(string characterId, int level)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;

        level = Mathf.Max(1, level);

        if (_data.heroUpgrades == null)
            _data.heroUpgrades = System.Array.Empty<HeroUpgradeEntry>();

        for (int i = 0; i < _data.heroUpgrades.Length; i++)
        {
            if (_data.heroUpgrades[i]?.characterId == characterId)
            {
                _data.heroUpgrades[i].level = level;
                Persist();
                return;
            }
        }

        // 新条目
        var arr = _data.heroUpgrades;
        System.Array.Resize(ref arr, arr.Length + 1);
        arr[arr.Length - 1] = new HeroUpgradeEntry { characterId = characterId, level = level };
        _data.heroUpgrades = arr;
        Persist();
    }

    /// <summary>升级英雄。成功返回 true，失败（金币不足/已满级）返回 false。</summary>
    public bool UpgradeHero(string characterId, HeroUpgradeData data)
    {
        if (data == null) return false;

        int currentLevel = GetHeroLevel(characterId);
        int effectiveMax = GetEffectiveMaxLevel(characterId, data);
        if (currentLevel >= effectiveMax)
        {
            Debug.LogWarning($"[PlayerProfile] {characterId} 已达满级 {data.maxLevel}");
            return false;
        }

        int cost = data.GetCostForLevel(currentLevel + 1);
        if (!SpendGold(cost))
        {
            Debug.LogWarning($"[PlayerProfile] 金币不足升级 {characterId}：需要 {cost}，持有 {Gold}");
            return false;
        }

        SetHeroLevel(characterId, currentLevel + 1);
        Debug.Log($"[PlayerProfile] {characterId} 升级 {currentLevel} → {currentLevel + 1}，消耗金币 {cost}");

        // 成就系统：英雄升级事件
        EventBus.Publish(new HeroLevelUpEvent { characterId = characterId });
        EventBus.Publish(new PlayerDataChangedEvent());

        return true;
    }

    // ── 升阶 ──

    /// <summary>获取英雄阶位（0=Normal, 1=Rare, 2=Legend）。</summary>
    public int GetHeroStage(string characterId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data?.heroUpgrades == null) return 0;
        for (int i = 0; i < _data.heroUpgrades.Length; i++)
        {
            var e = _data.heroUpgrades[i];
            if (e != null && e.characterId == characterId)
                return Mathf.Clamp(e.stage, 0, 2);
        }
        return 0;
    }

    /// <summary>获取英雄碎片持有数。</summary>
    public int GetFragmentCount(string characterId)
    {
        return CharacterUnlockEvaluator.GetFragmentCount(_data, characterId);
    }

    /// <summary>
    /// 修复历史数据不一致：itemIds 有但 characterFragmentKeys 缺失/落后的碎片数，同步补齐。
    /// 在 Home 场景加载时由 HomeHubController 调用一次。
    /// </summary>
    public void HealFragmentData(CharacterCatalog catalog)
    {
        if (_data == null || catalog == null) return;

        foreach (var def in catalog.characters)
        {
            if (def == null || def.fragmentItemId <= 0) continue;

            int fromItems = 0;
            if (_data.itemIds != null)
            {
                int itemIdx = System.Array.IndexOf(_data.itemIds, def.fragmentItemId);
                if (itemIdx >= 0 && itemIdx < (_data.itemCounts?.Length ?? 0))
                    fromItems = _data.itemCounts[itemIdx];
            }
            if (fromItems <= 0) continue;

            int fromKeys = 0;
            if (_data.characterFragmentKeys != null)
            {
                int keyIdx = System.Array.IndexOf(_data.characterFragmentKeys, def.characterId);
                if (keyIdx >= 0 && keyIdx < (_data.characterFragmentValues?.Length ?? 0))
                    fromKeys = _data.characterFragmentValues[keyIdx];
            }

            if (fromItems > fromKeys)
            {
                int diff = fromItems - fromKeys;
                AddFragments(def.characterId, diff);
                Debug.Log($"[HealFragment] {def.displayName}：characterFragmentKeys {fromKeys} → {fromItems}（补齐 {diff} 片，来自 itemIds）");
            }
        }
    }

    /// <summary>检测 itemId 是否为角色碎片，是则写入 characterFragmentKeys/Values 并返回 true。</summary>
    private bool TryRouteFragment(int itemId, int count)
    {
        var charId = GetCharacterIdForFragmentItem(itemId);
        if (string.IsNullOrEmpty(charId)) return false;
        AddFragments(charId, count);
        return true;
    }

    private static string GetCharacterIdForFragmentItem(int itemId)
    {
        if (itemId <= 0) return null;
        var catalog = GetCharacterCatalog();
        if (catalog == null)
        {
            Debug.LogWarning($"[AddItem] CharacterCatalog 未找到，碎片 itemId={itemId} 无法路由");
            return null;
        }
        foreach (var def in catalog.characters)
        {
            if (def != null && def.fragmentItemId == itemId)
            {
                Debug.Log($"[AddItem] 碎片路由: itemId={itemId} → {def.characterId} ({def.displayName})");
                return def.characterId;
            }
        }
        Debug.LogWarning($"[AddItem] 碎片 itemId={itemId} 未匹配任何角色的 fragmentItemId");
        return null;
    }

    private static CharacterCatalog _sCachedCatalog;

    /// <summary>获取 CharacterCatalog：优先静态缓存 → FindObjectOfType → Resources 兜底。</summary>
    internal static CharacterCatalog GetCharacterCatalog()
    {
        if (_sCachedCatalog != null) return _sCachedCatalog;

        var cca = UnityEngine.Object.FindObjectOfType<CharacterConfigApplier>();
        if (cca != null && cca.characterCatalog != null)
        {
            _sCachedCatalog = cca.characterCatalog;
            return _sCachedCatalog;
        }
        // Resources 兜底（需确保 Catalog 放在 Resources 目录下，否则返回 null）
        _sCachedCatalog = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
        if (_sCachedCatalog == null)
            Debug.LogWarning("[PlayerProfileService] CharacterCatalog 加载失败：不在 Resources 目录且场景中无 CharacterConfigApplier。碎片路由将失效。");
        return _sCachedCatalog;
    }

    /// <summary>增加英雄碎片。</summary>
    public void AddFragments(string characterId, int count)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null || count <= 0) return;

        if (_data.characterFragmentKeys == null)
        {
            _data.characterFragmentKeys = new string[] { characterId };
            _data.characterFragmentValues = new int[] { count };
        }
        else
        {
            int idx = System.Array.IndexOf(_data.characterFragmentKeys, characterId);
            if (idx >= 0 && idx < _data.characterFragmentValues.Length)
                _data.characterFragmentValues[idx] += count;
            else
            {
                var keys = new string[_data.characterFragmentKeys.Length + 1];
                var vals = new int[_data.characterFragmentValues.Length + 1];
                System.Array.Copy(_data.characterFragmentKeys, keys, _data.characterFragmentKeys.Length);
                System.Array.Copy(_data.characterFragmentValues, vals, _data.characterFragmentValues.Length);
                keys[keys.Length - 1] = characterId;
                vals[vals.Length - 1] = count;
                _data.characterFragmentKeys = keys;
                _data.characterFragmentValues = vals;
            }
        }
        Persist();
        EventBus.Publish(new PlayerDataChangedEvent());
    }

    /// <summary>是否满足升阶条件。返回 (canPromote, 缺少的碎片数, 等级是否达标)。</summary>
    public bool CanPromoteStage(string characterId, HeroUpgradeData data, out int missingFragments, out bool levelOk)
    {
        missingFragments = 0;
        levelOk = false;
        if (data == null) return false;

        int stage = GetHeroStage(characterId);
        int level = GetHeroLevel(characterId);
        int requiredLevel, requiredFrags;

        if (stage == 0) { requiredLevel = data.rareRequiredLevel; requiredFrags = data.rareFragmentCost; }
        else if (stage == 1) { requiredLevel = data.legendRequiredLevel; requiredFrags = data.legendFragmentCost; }
        else return false; // 已是 Legend

        levelOk = level >= requiredLevel;
        int frags = GetFragmentCount(characterId);
        missingFragments = Mathf.Max(0, requiredFrags - frags);
        return levelOk && missingFragments == 0;
    }

    /// <summary>执行升阶。成功返回 true。</summary>
    public bool PromoteStage(string characterId, HeroUpgradeData data)
    {
        if (!CanPromoteStage(characterId, data, out _, out _)) return false;

        int stage = GetHeroStage(characterId);
        int cost = stage == 0 ? data.rareFragmentCost : data.legendFragmentCost;

        // 扣除碎片（characterFragmentKeys）
        if (!_loaded) LoadOrCreate();
        int idx = _data.characterFragmentKeys != null ? System.Array.IndexOf(_data.characterFragmentKeys, characterId) : -1;
        if (idx < 0) return false;
        _data.characterFragmentValues[idx] -= cost;

        // 同步扣除 itemIds/itemCounts（保持两数组一致）
        var cat = GetCharacterCatalog();
        if (cat != null)
        {
            var def = cat.Get(characterId);
            if (def != null && def.fragmentItemId > 0 && _data.itemIds != null)
            {
                int itemIdx = System.Array.IndexOf(_data.itemIds, def.fragmentItemId);
                if (itemIdx >= 0 && itemIdx < (_data.itemCounts?.Length ?? 0))
                    _data.itemCounts[itemIdx] = Mathf.Max(0, _data.itemCounts[itemIdx] - cost);
            }
        }

        SetHeroStage(characterId, stage + 1);
        Debug.Log($"[PlayerProfile] {characterId} 升阶 {stage} → {stage + 1}，消耗碎片 {cost}");

        // 成就系统：英雄升阶事件
        EventBus.Publish(new HeroStageUpEvent { characterId = characterId });
        EventBus.Publish(new PlayerDataChangedEvent());

        return true;
    }

    private void SetHeroStage(string characterId, int stage)
    {
        if (_data?.heroUpgrades == null) return;
        for (int i = 0; i < _data.heroUpgrades.Length; i++)
        {
            if (_data.heroUpgrades[i]?.characterId == characterId)
            {
                _data.heroUpgrades[i].stage = stage;
                Persist();
                return;
            }
        }
        // 新条目（理论上不应该走到这）
        var arr = _data.heroUpgrades;
        System.Array.Resize(ref arr, arr.Length + 1);
        arr[arr.Length - 1] = new HeroUpgradeEntry { characterId = characterId, level = 1, stage = stage };
        _data.heroUpgrades = arr;
        Persist();
    }

    /// <summary>获取当前阶位的有效最大等级。</summary>
    public int GetEffectiveMaxLevel(string characterId, HeroUpgradeData data)
    {
        if (data == null) return 1;
        int stage = GetHeroStage(characterId);
        if (stage == 0) return Mathf.Min(data.rareRequiredLevel, data.maxLevel);
        if (stage == 1) return Mathf.Min(data.legendRequiredLevel, data.maxLevel);
        return data.maxLevel;
    }

    /// <summary>获取指定属性的升级倍率。</summary>
    public float GetUpgradeMul(string characterId, HeroUpgradeData data, string attrName)
    {
        if (data == null) return 1f;
        int level = GetHeroLevel(characterId);

        switch (attrName)
        {
            case "attack":       return data.EvaluateMul(data.attackMulAtMax, level);
            case "maxHp":        return data.EvaluateMul(data.maxHpMulAtMax, level);
            case "defense":      return data.EvaluateMul(data.defenseMulAtMax, level);
            case "moveSpeed":    return data.EvaluateMul(data.moveSpeedMulAtMax, level);
            case "attackRange":  return data.EvaluateMul(data.attackRangeMulAtMax, level);
            case "critRate":     return data.EvaluateAdd(data.critRateAddAtMax, level);
            case "critDmg":      return data.EvaluateMul(data.critDmgMulAtMax, level);
            case "pierceRate":   return data.EvaluateAdd(data.pierceRateAddAtMax, level);
            case "pierceCount":  return data.EvaluateAdd(data.pierceCountAddAtMax, level);
            case "penRate":      return data.EvaluateAdd(data.penRateAddAtMax, level);
            default:             return 1f;
        }
    }

    private PlayerProfileService()
    {
        _storage = new EncryptedPlayerPrefsStorage();
    }

    public void LoadOrCreate()
    {
        if (_loaded) return;

        if (!_storage.TryLoadString(GuestIdKey, out string guestId) || string.IsNullOrEmpty(guestId))
        {
            guestId = "guest_" + Guid.NewGuid().ToString("N");
            _storage.SaveString(GuestIdKey, guestId);
        }

        if (_storage.TryLoad(SaveKey, out string json) && !string.IsNullOrEmpty(json))
        {
            try
            {
                _data = JsonUtility.FromJson<PlayerSaveData>(json);
            }
            catch (Exception e)
            {
                GameErrorPresenter.Show(GameErrorCodes.SaveLoadFailed);
                Debug.LogWarning($"[PlayerProfileService] 存档解析失败，将新建：{e.Message}");
                _data = null;
            }
        }

        // 检测存档是否损坏
        bool saveCorrupted = false;
        if (_storage is EncryptedPlayerPrefsStorage encrypted)
            saveCorrupted = encrypted.WasLastLoadCorrupted;

        if (_data == null)
        {
            if (saveCorrupted)
                PendingCorruptionAlert = true; // 延迟到 UI 就绪后弹出

            _data = new PlayerSaveData
            {
                version = PlayerSaveData.CurrentVersion,
                playerId = guestId,
                levels = Array.Empty<LevelProgressEntry>(),
            };
        }
        else
        {
            if (string.IsNullOrEmpty(_data.playerId))
                _data.playerId = guestId;
            _data.MigrateToLatest();
        }

        _levels.Clear();
        if (_data.levels != null)
        {
            for (int i = 0; i < _data.levels.Length; i++)
            {
                var e = _data.levels[i];
                if (e == null || e.levelId <= 0) continue;
                _levels[e.levelId] = e;
            }
        }

        _loaded = true;
        ChapterLevelCatalog.InvalidateCache();
    }

    public bool HasCleared(int levelId)
    {
        if (!_loaded) LoadOrCreate();

        return _levels.TryGetValue(levelId, out var e) && e.cleared;
    }

    public bool TryGetProgress(int levelId, out LevelProgressEntry entry)
    {
        return _levels.TryGetValue(levelId, out entry);
    }

    public bool IsLevelUnlocked(int levelId)
    {
        return ChapterLevelUnlockEvaluator.IsLevelUnlocked(levelId, HasCleared);
    }

    /// <summary>胜利结算写入：星级取 max，时长取全局最短，击杀取 max。</summary>
    public void RecordVictory(int levelId, float durationSec, int killCount, int stars)
    {
        if (!_loaded) LoadOrCreate();
        if (levelId <= 0) return;

        stars = Mathf.Clamp(stars, 1, 3);
        durationSec = Mathf.Max(0f, durationSec);
        killCount = Mathf.Max(0, killCount);

        if (!_levels.TryGetValue(levelId, out var entry))
        {
            entry = new LevelProgressEntry { levelId = levelId };
            _levels[levelId] = entry;
        }

        bool wasCleared = entry.cleared;
        int oldStars = entry.stars;
        entry.cleared = true;
        entry.stars = Mathf.Max(entry.stars, stars);
        int starDelta = entry.stars - oldStars;
        if (!wasCleared || entry.bestTimeSec <= 0f)
            entry.bestTimeSec = durationSec;
        else
            entry.bestTimeSec = Mathf.Min(entry.bestTimeSec, durationSec);
        entry.bestKills = Mathf.Max(entry.bestKills, killCount);

        Persist();

        // 成就系统：星星累计事件（delta=本次新增的星星，重复挑战只计改善部分）
        if (starDelta > 0)
            EventBus.Publish(new StarEarnedEvent { levelId = levelId, stars = starDelta });
    }

    /// <summary>当前上阵角色 ID（持久化到本地存档）。</summary>
    public string EquippedCharacterId
    {
        get
        {
            if (!_loaded) LoadOrCreate();
            return _data?.equippedCharacterId ?? string.Empty;
        }
    }

    /// <summary>设置上阵角色并持久化。</summary>
    public void SetEquippedCharacter(string characterId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;
        _data.equippedCharacterId = characterId ?? string.Empty;
        Persist();
    }

    private void Persist()
    {
        var list = new List<LevelProgressEntry>(_levels.Count);
        foreach (var kv in _levels)
            list.Add(kv.Value);
        list.Sort((a, b) => a.levelId.CompareTo(b.levelId));
        _data.levels = list.ToArray();

        string json = JsonUtility.ToJson(_data);
        _storage.Save(SaveKey, json);
    }

    /// <summary>格式化金币显示：≥10000 显示为 "1.52w"，否则原样。</summary>
    public static string FormatGold(int gold)
    {
        if (gold >= 10000)
        {
            float w = gold / 10000f;
            // 保留最多 2 位小数，去掉末尾多余的 0
            string s = w.ToString("F2").TrimEnd('0').TrimEnd('.');
            return $"{s}w";
        }
        return gold.ToString();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/给予百万金币（开发测试）", false, 501)]
    private static void GiveOneMillionGold()
    {
        Instance.LoadOrCreate();
        Instance.AddGold(1_000_000);
        UnityEditor.EditorUtility.DisplayDialog("完成", $"金币已发放。当前余额：{FormatGold(Instance.Gold)}", "确定");
    }

    [UnityEditor.MenuItem("Tools/模拟存档篡改（测试加密）", false, 502)]
    private static void SimulateSaveCorruption()
    {
        PlayerPrefs.SetString(SaveKey, "THIS_IS_TAMPERED_DATA_!@#$%");
        PlayerPrefs.SetString(GuestIdKey, "FAKE_GUEST_12345");
        PlayerPrefs.Save();
        UnityEditor.EditorUtility.DisplayDialog("完成",
            "已写入篡改数据。\n\n关掉此窗口后重新进入 Play Mode，应看到「存档数据异常」错误提示。", "确定");
    }

    [UnityEditor.MenuItem("Tools/发放物品/金币+1万", false, 503)]
    private static void GrantGold10k() { Instance.LoadOrCreate(); Instance.AddItem(1, 10000); ShowGrantResult(1, 10000); }
#endif

    // ═══════════════ 商店购买记录 ═══════════════

    /// <summary>获取某商品的已购买次数（本周期内）。</summary>
    public int GetShopPurchaseCount(int shopItemId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data?.shopPurchaseLogs == null) return 0;
        for (int i = 0; i < _data.shopPurchaseLogs.Length; i++)
        {
            if (_data.shopPurchaseLogs[i] != null && _data.shopPurchaseLogs[i].shopItemId == shopItemId)
                return _data.shopPurchaseLogs[i].purchasedCount;
        }
        return 0;
    }

    /// <summary>记录一次购买（count 次）。</summary>
    public void RecordShopPurchase(int shopItemId, int count = 1)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;

        var logs = _data.shopPurchaseLogs ?? new ShopPurchaseLog[0];
        int idx = -1;
        for (int i = 0; i < logs.Length; i++)
        {
            if (logs[i] != null && logs[i].shopItemId == shopItemId) { idx = i; break; }
        }

        if (idx >= 0)
        {
            logs[idx].purchasedCount += count;
            logs[idx].lastResetTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        else
        {
            Array.Resize(ref logs, logs.Length + 1);
            logs[logs.Length - 1] = new ShopPurchaseLog
            {
                shopItemId = shopItemId,
                purchasedCount = count,
                lastResetTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        _data.shopPurchaseLogs = logs;
        Persist();
    }

    /// <summary>重置某 refreshType 下的所有购买计数（跨周期调用）。</summary>
    public void ResetShopPurchasesByRefresh(int refreshType)
    {
        if (!_loaded) LoadOrCreate();
        if (_data?.shopPurchaseLogs == null) return;

        var catalog = ShopCatalog.Instance;
        foreach (var log in _data.shopPurchaseLogs)
        {
            if (log == null) continue;
            var row = catalog.Get(log.shopItemId);
            if (row != null && row.Refresh == refreshType)
            {
                log.purchasedCount = 0;
                log.lastResetTimestamp = 0;
            }
        }
        Persist();
    }

    /// <summary>清空所有商店购买记录（开发用）。</summary>
    public void ClearAllShopPurchaseLogs()
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;
        _data.shopPurchaseLogs = new ShopPurchaseLog[0];
        Persist();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/商店/清空购买记录", false, 510)]
    private static void ClearShopPurchaseLogs()
    {
        Instance.LoadOrCreate();
        Instance.ClearAllShopPurchaseLogs();
        UnityEditor.EditorUtility.DisplayDialog("完成", "商店购买记录已全部清空", "确定");
    }

    [UnityEditor.MenuItem("Tools/发放物品/金币+10万", false, 504)]
    private static void GrantGold100k() { Instance.LoadOrCreate(); Instance.AddItem(1, 100000); ShowGrantResult(1, 100000); }

    [UnityEditor.MenuItem("Tools/发放物品/英雄碎片+20（弹仔）", false, 505)]
    private static void GrantFragmentPistol() { Instance.LoadOrCreate(); Instance.AddFragments("character_01", 20); UnityEditor.EditorUtility.DisplayDialog("完成", $"+20 弹仔碎片\n总计：{Instance.GetFragmentCount("character_01")} 片", "确定"); }

    [UnityEditor.MenuItem("Tools/发放物品/英雄碎片+20（刃娃）", false, 506)]
    private static void GrantFragmentSword() { Instance.LoadOrCreate(); Instance.AddFragments("character_02", 20); UnityEditor.EditorUtility.DisplayDialog("完成", $"+20 刃娃碎片\n总计：{Instance.GetFragmentCount("character_02")} 片", "确定"); }

    [UnityEditor.MenuItem("Tools/发放物品/英雄碎片+20（爆爆）", false, 507)]
    private static void GrantFragmentPanda() { Instance.LoadOrCreate(); Instance.AddFragments("character_03", 20); UnityEditor.EditorUtility.DisplayDialog("完成", $"+20 爆爆碎片\n总计：{Instance.GetFragmentCount("character_03")} 片", "确定"); }

    [UnityEditor.MenuItem("Tools/发放物品/英雄碎片+20（符仔）", false, 508)]
    private static void GrantFragmentTaoist() { Instance.LoadOrCreate(); Instance.AddFragments("character_04", 20); UnityEditor.EditorUtility.DisplayDialog("完成", $"+20 符仔碎片\n总计：{Instance.GetFragmentCount("character_04")} 片", "确定"); }

    [UnityEditor.MenuItem("Tools/发放物品/英雄碎片+20（追风）", false, 509)]
    private static void GrantFragmentIronMan() { Instance.LoadOrCreate(); Instance.AddFragments("character_05", 20); UnityEditor.EditorUtility.DisplayDialog("完成", $"+20 追风碎片\n总计：{Instance.GetFragmentCount("character_05")} 片", "确定"); }

    private static void ShowGrantResult(int itemId, int count)
    {
        UnityEditor.EditorUtility.DisplayDialog("发放完成",
            $"+{FormatGold(count)} 金币（ID={itemId}）\n当前余额：{FormatGold(Instance.Gold)}", "确定");
    }
#endif
}
