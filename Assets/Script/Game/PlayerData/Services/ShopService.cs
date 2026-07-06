using System;
using ProtoTable;
using UnityEngine;

/// <summary>
/// 商店服务：购买/限购/刷新/解锁判断。
/// 调用入口在 ShopCell → ShopView，不依赖 MonoBehaviour。
/// </summary>
public enum ShopPurchaseResult
{
    Success,
    SoldOut,            // 已售罄
    NotEnoughGold,      // 金币不足
    NotEnoughDiamond,   // 钻石不足
    Locked,             // 未解锁
    Error,              // 其他错误
}

public static class ShopService
{
    /// <summary>刷新类型常量（与 ShopTable.Refresh 值对齐：1=每天, 7=每周一, 2=每月1号, 0/空=不刷新）。</summary>
    public const int RefreshNone = 0;
    public const int RefreshDaily = 1;
    public const int RefreshWeekly = 7;
    public const int RefreshMonthly = 2;

    /// <summary>是否需要刷新（跨周期检查）。返回被重置的 refreshType 列表。</summary>
    public static void CheckAndRefresh()
    {
        var svc = PlayerProfileService.Instance;
        if (svc?.Data?.shopPurchaseLogs == null) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool dailyReset = false, weeklyReset = false, monthlyReset = false;

        foreach (var log in svc.Data.shopPurchaseLogs)
        {
            if (log == null || log.lastResetTimestamp <= 0) continue;

            var row = ShopCatalog.Instance.Get(log.shopItemId);
            if (row == null) continue;

            DateTime last = DateTimeOffset.FromUnixTimeSeconds(log.lastResetTimestamp).LocalDateTime;
            DateTime today = DateTimeOffset.FromUnixTimeSeconds(now).LocalDateTime;

            if (row.Refresh == RefreshDaily && last.Date < today.Date)
                dailyReset = true;
            else if (row.Refresh == RefreshWeekly && GetWeekStart(last) < GetWeekStart(today))
                weeklyReset = true;
            else if (row.Refresh == RefreshMonthly && GetMonthStart(last) < GetMonthStart(today))
                monthlyReset = true;
        }

        if (dailyReset) svc.ResetShopPurchasesByRefresh(RefreshDaily);
        if (weeklyReset) svc.ResetShopPurchasesByRefresh(RefreshWeekly);
        if (monthlyReset) svc.ResetShopPurchasesByRefresh(RefreshMonthly);
    }

    /// <summary>获取实际售价。</summary>
    public static int GetPrice(ShopTable row)
    {
        if (row == null) return 0;
        int discount = Mathf.Clamp(row.PurchaseDiscount, 0, 100);
        return discount > 0 ? row.OldPrice * discount / 100 : row.OldPrice;
    }

    /// <summary>商品是否已解锁。</summary>
    public static bool IsUnlocked(ShopTable row)
    {
        if (row == null) return false;
        if (row.Unlock <= 0) return true;

        // 确保 profile 已加载（HasCleared 内部不自动 LoadOrCreate）
        PlayerProfileService.Instance.LoadOrCreate();

        bool cleared = PlayerProfileService.Instance.HasCleared(row.Unlock);

        if (row.Hide != 0)
        {
            // Hide=1: 关卡未解锁时隐藏商品入口，解锁后显示
            // Hide=其他: 无条件隐藏
            if (row.Hide == 1)
            {
                if (!cleared) return false;
                // 已解锁 → 不隐藏，继续往下
            }
            else
            {
                return false;
            }
        }

        return cleared;
    }

    /// <summary>本周期剩余可购买次数（-1=不限购）。</summary>
    public static int GetRemainingPurchases(ShopTable row)
    {
        if (row == null || row.PurchaseNum <= 0) return -1; // 不限购
        int bought = PlayerProfileService.Instance.GetShopPurchaseCount(row.ID);
        return Mathf.Max(0, row.PurchaseNum - bought);
    }

    /// <summary>是否已售罄。</summary>
    public static bool IsSoldOut(ShopTable row) => GetRemainingPurchases(row) == 0;

    /// <summary>执行购买。返回结果 + 错误信息。</summary>
    public static ShopPurchaseResult Purchase(ShopTable row, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (row == null) { errorMessage = "商品不存在"; return ShopPurchaseResult.Error; }
        if (!IsUnlocked(row)) { errorMessage = "未满足解锁条件"; return ShopPurchaseResult.Locked; }
        if (IsSoldOut(row)) { errorMessage = "已售罄"; return ShopPurchaseResult.SoldOut; }

        int price = GetPrice(row);
        int count = Mathf.Max(1, row.ItemNumber);

        var svc = PlayerProfileService.Instance;
        bool isDiamond = row.PriceType == 2;
        bool isFree = row.PriceType == 0;
        bool isAd = row.PriceType == 3;

        if (isFree || isAd)
        {
            // 免费/广告：不扣货币，直接给物品
        }
        else if (isDiamond)
        {
            if (!svc.CanAffordDiamond(price))
            {
                errorMessage = "钻石不足";
                return ShopPurchaseResult.NotEnoughDiamond;
            }
            svc.SpendDiamond(price);
        }
        else
        {
            if (!svc.CanAffordGold(price))
            {
                errorMessage = "金币不足";
                return ShopPurchaseResult.NotEnoughGold;
            }
            svc.SpendGold(price);
        }

        svc.AddItem(row.ItemID, count);
        svc.RecordShopPurchase(row.ID);

        string currencyLabel = isFree ? "免费" : isAd ? "广告" : isDiamond ? "钻石" : "金币";
        GameLog.Info($"[Shop] 购买成功: {row.ShopName} ×{count}, 消耗 {price} {currencyLabel}");
        return ShopPurchaseResult.Success;
    }

    private static DateTime GetWeekStart(DateTime dt)
    {
        // 周一为一周之始
        int diff = (7 + (int)dt.DayOfWeek - 1) % 7;
        return dt.AddDays(-diff).Date;
    }

    private static DateTime GetMonthStart(DateTime dt) => new DateTime(dt.Year, dt.Month, 1);

    /// <summary>根据 Refresh 类型计算下一个重置时刻（本地时间）。</summary>
    public static DateTime GetNextResetTime(int refresh)
    {
        DateTime now = DateTime.Now;
        switch (refresh)
        {
            case RefreshDaily:
            {
                DateTime next = now.Date.AddDays(1);
                return next;
            }
            case RefreshWeekly:
            {
                DateTime weekStart = GetWeekStart(now);
                DateTime next = weekStart.AddDays(7);
                return next;
            }
            case RefreshMonthly:
            {
                DateTime monthStart = GetMonthStart(now);
                DateTime next = monthStart.AddMonths(1);
                return next;
            }
            default:
                return DateTime.MaxValue; // 不刷新
        }
    }
}
