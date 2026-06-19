using System;

/// <summary>Guest 单存档槽 JSON 根对象。</summary>
[Serializable]
public class PlayerSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public string playerId;
    public LevelProgressEntry[] levels = Array.Empty<LevelProgressEntry>();
    /// <summary>已上阵角色 ID（空 = 未选/使用默认）。</summary>
    public string equippedCharacterId;
    /// <summary>已解锁角色 ID（通关解锁或碎片解锁后加入）。</summary>
    public string[] unlockedCharacters = Array.Empty<string>();
    /// <summary>角色碎片数（characterId → 碎片数）。</summary>
    public string[] characterFragmentKeys;
    public int[] characterFragmentValues;

    // ── v4 货币 ──
    public int gold;
    public int diamond;

    // ── v4 英雄升级 ──
    public HeroUpgradeEntry[] heroUpgrades = Array.Empty<HeroUpgradeEntry>();

    // ── v4 物品背包（ID=1 金币也走这里，gold 字段为冗余快取）──
    public int[] itemIds = Array.Empty<int>();
    public int[] itemCounts = Array.Empty<int>();

    // ── v5 商店购买记录 ──
    public ShopPurchaseLog[] shopPurchaseLogs = new ShopPurchaseLog[0];

    /// <summary>升级到最新版本（结构迁移）。</summary>
    public void MigrateToLatest()
    {
        if (version >= CurrentVersion) return;

        // v3 → v4：gold/diamond/itemIds/itemCounts 默认 0/空，无需额外迁移
        version = CurrentVersion;
    }
}

/// <summary>商店单条购买记录。</summary>
[Serializable]
public class ShopPurchaseLog
{
    public int shopItemId;           // ShopTable.ID
    public int purchasedCount;       // 本周期已购买次数
    public long lastResetTimestamp;  // 上次重置时间（Unix 秒）
}
