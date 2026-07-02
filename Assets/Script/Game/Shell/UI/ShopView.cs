using System;
using System.Collections.Generic;
using ProtoTable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店 Tab 页：商品列表 + 刷新倒计时 + 购买确认。
/// 继承 HomeTabViewBase，挂 ShopView Prefab 上。
/// </summary>
[DisallowMultipleComponent]
public class ShopView : HomeTabViewBase
{
    [Header("列表")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private ShopCell cellPrefab;

    [Header("刷新")]
    [SerializeField] private TextMeshProUGUI textRefreshCountdown;
    [SerializeField] private GameObject groupCountdown; // 倒计时区域（可整体隐藏）

    [Header("状态")]
    [SerializeField] private GameObject emptyHint;

    private readonly List<ShopCell> _cells = new List<ShopCell>();
    private Coroutine _countdownRoutine;

    // ═══════════════ 生命周期 ═══════════════

    public override void OnTabInit()
    {
        base.OnTabInit();

        ShopCatalog.Instance.InvalidateCache(); // 强制重读表
        BuildList();

        // 中文字体（Cell 创建后再扫一遍）
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(transform);
    }

    public override void OnTabEnter()
    {
        base.OnTabEnter();
        ShopService.CheckAndRefresh();
        RebuildList();
        StartCountdown();
    }

    public override void OnTabLeave()
    {
        base.OnTabLeave();
        StopCountdown();
    }

    public override void OnTabRefresh()
    {
        if (gameObject.activeInHierarchy)
            RebuildList();
    }

    // ═══════════════ 列表构建 ═══════════════

    private void BuildList()
    {
        ClearCells();

        var items = new List<ShopTable>(ShopCatalog.Instance.NormalItems);

        // 排序：免费 > 看广告 > 金币 > 钻石 > 已售罄
        items.Sort((a, b) =>
        {
            bool soldOutA = ShopService.IsSoldOut(a);
            bool soldOutB = ShopService.IsSoldOut(b);
            if (soldOutA != soldOutB) return soldOutA ? 1 : -1;
            return GetPriceTypePriority(a.PriceType).CompareTo(GetPriceTypePriority(b.PriceType));
        });

        foreach (var row in items)
        {
            if (!ShopService.IsUnlocked(row)) continue;
            var cell = Instantiate(cellPrefab, listContent);
            cell.Bind(row, OnCellBuyClicked);
            _cells.Add(cell);
        }

        if (emptyHint != null)
            emptyHint.SetActive(_cells.Count == 0);
    }

    /// <summary>PriceType 排序优先级（越小越靠前）。</summary>
    private static int GetPriceTypePriority(int priceType)
    {
        switch (priceType)
        {
            case 0: return 0; // 免费
            case 3: return 1; // 看广告
            case 1: return 2; // 金币
            case 2: return 3; // 钻石
            default: return 4;
        }
    }

    private void RebuildList()
    {
        ClearCells(); // 简单起见全量重建；商品数量少，性能无忧
        BuildList();
    }

    private void ClearCells()
    {
        foreach (var c in _cells)
            if (c != null) Destroy(c.gameObject);
        _cells.Clear();
    }

    // ═══════════════ 购买 ═══════════════

    private void OnCellBuyClicked(ShopCell cell)
    {
        if (cell == null || cell.ShopRow == null) return;

        int priceType = cell.ShopRow.PriceType;
        int count = Mathf.Max(1, cell.ShopRow.ItemNumber);
        string itemName = cell.ItemRow?.ItemName ?? cell.ShopRow.ShopName ?? "物品";

        // 免费：直接发奖，不弹确认框
        if (priceType == 0)
        {
            ExecuteShopPurchase(cell, itemName, count);
            return;
        }

        // 广告：直接播广告，不弹确认框
        if (priceType == 3)
        {
            UIManager.Instance.ShowToast("广告还在路上，分享先把奖励领走～", 2f);
            StartCoroutine(ClaimAdReward(cell, itemName, count));
            return;
        }

        // 金币/钻石：弹确认框
        int price = ShopService.GetPrice(cell.ShopRow);
        string currencyLabel = priceType == 2 ? "钻石" : "金币";
        UIManager.Instance.ShowConfirm("兑换确认",
            $"确定购买「{itemName}」×{count}？\n消耗 {price} {currencyLabel}",
            confirmed =>
            {
                if (!confirmed) return;
                ExecuteShopPurchase(cell, itemName, count);
            });
    }

    private void ExecuteShopPurchase(ShopCell cell, string itemName, int count)
    {
        var result = ShopService.Purchase(cell.ShopRow, out string errMsg);
        switch (result)
        {
            case ShopPurchaseResult.Success:
                UIManager.Instance.ShowToast($"已获得 {itemName}×{count}", 1.5f);
                cell.RefreshLimitDisplay();
                OnTabRefresh();
                EventBus.Publish(new PlayerDataChangedEvent());
                break;

            case ShopPurchaseResult.SoldOut:
                UIManager.Instance.ShowToast("已售罄", 1f);
                break;

            case ShopPurchaseResult.NotEnoughGold:
                UIManager.Instance.ShowToast("金币不足", 1f);
                break;

            case ShopPurchaseResult.NotEnoughDiamond:
                UIManager.Instance.ShowToast("钻石不足", 1f);
                break;

            case ShopPurchaseResult.Locked:
                UIManager.Instance.ShowToast(errMsg, 1f);
                break;

            default:
                Debug.LogWarning($"[Shop] 购买失败: {errMsg}");
                break;
        }
    }

    private System.Collections.IEnumerator ClaimAdReward(ShopCell cell, string itemName, int count)
    {
        bool adCompleted = false;
        bool adResponded = false;

        WeChatRewardedAdProvider.Instance.RequestReviveAd(success =>
        {
            adCompleted = success;
            adResponded = true;
        });

        // 等待广告回调（DefaultReviveAdProvider 同步完成，真机广告异步）
        float timeout = 30f;
        float elapsed = 0f;
        while (!adResponded && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (adCompleted)
        {
            ExecuteShopPurchase(cell, itemName, count);
        }
        else
        {
            UIManager.Instance.ShowToast("广告未完成，请稍后再试", 1.5f);
        }
    }

    // ═══════════════ 倒计时 ═══════════════

    private void StartCountdown()
    {
        StopCountdown();
        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    private void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }
    }

    private System.Collections.IEnumerator CountdownRoutine()
    {
        while (true)
        {
            int activeRefresh = ResolveDominantRefresh();
            DateTime now = DateTime.Now;
            DateTime nextReset = activeRefresh != ShopService.RefreshNone
                ? ShopService.GetNextResetTime(activeRefresh)
                : DateTime.MaxValue;

            if (textRefreshCountdown != null)
            {
                if (activeRefresh != ShopService.RefreshNone && nextReset != DateTime.MaxValue)
                {
                    double secsLeft = (nextReset - now).TotalSeconds;
                    if (secsLeft <= 0)
                    {
                        textRefreshCountdown.text = "即将刷新…";
                        ShopService.CheckAndRefresh();
                        RebuildList();
                    }
                    else if (secsLeft < 86400) // < 24 小时 → HH:MM:SS
                    {
                        textRefreshCountdown.text = $"刷新倒计时 {Math.Floor(secsLeft / 3600):00}:{Math.Floor(secsLeft / 60) % 60:00}:{Math.Floor(secsLeft) % 60:00}";
                    }
                    else // ≥ 24 小时 → X天 HH:MM:SS
                    {
                        int days = (int)Math.Floor(secsLeft / 86400);
                        double remain = secsLeft - days * 86400;
                        textRefreshCountdown.text = $"刷新倒计时 {days}天 {Math.Floor(remain / 3600):00}:{Math.Floor(remain / 60) % 60:00}:{Math.Floor(remain) % 60:00}";
                    }
                }
                else
                {
                    textRefreshCountdown.text = string.Empty;
                }
            }

            if (groupCountdown != null)
                groupCountdown.SetActive(activeRefresh != ShopService.RefreshNone && nextReset != DateTime.MaxValue);

            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// 按优先级取当前可见商品的主导刷新类型：每日(1) > 每周(7) > 每月(2) > 不刷新(0)。
    /// </summary>
    private int ResolveDominantRefresh()
    {
        bool hasDaily = false, hasWeekly = false, hasMonthly = false;
        var items = ShopCatalog.Instance.NormalItems;
        foreach (var row in items)
        {
            if (!ShopService.IsUnlocked(row)) continue;
            switch (row.Refresh)
            {
                case ShopService.RefreshDaily:   hasDaily = true; break;
                case ShopService.RefreshWeekly:  hasWeekly = true; break;
                case ShopService.RefreshMonthly: hasMonthly = true; break;
            }
        }

        if (hasDaily) return ShopService.RefreshDaily;
        if (hasWeekly) return ShopService.RefreshWeekly;
        if (hasMonthly) return ShopService.RefreshMonthly;
        return ShopService.RefreshNone;
    }
}
