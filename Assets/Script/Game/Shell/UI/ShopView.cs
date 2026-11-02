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
    [SerializeField] private HomeExchangePresentation presentation;

    private readonly List<ShopCell> _cells = new List<ShopCell>();
    private Coroutine _countdownRoutine;
    private Coroutine _adTimeoutRoutine;
    private AdPurchaseRequest _adRequest;
    private const float AdRequestTimeout = WXShareRequestRunner.ReturnTimeoutSeconds;

    public bool IsAdRewardInFlight => _adRequest != null && !_adRequest.completed;

#if UNITY_EDITOR
    /// <summary>Editor validation seam; null uses the real ad provider.</summary>
    public static Action<Action<bool>> EditorAdRequestOverride;
#endif

    // FlatBuffer table rows are read-only. Capture the row itself and reward values,
    // never a ShopCell: a profile event can destroy/rebuild every cell while an ad is open.
    private sealed class AdPurchaseRequest
    {
        public ShopTable row;
        public int itemId, count;
        public string receiptId;
        public float requestedAt;
        public bool completed;
    }

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
        ExpireAdRequest();
        ResumeAdTimeout();
        ShopService.CheckAndRefresh();
        RebuildList();
        StartCountdown();
    }

    public override void OnTabLeave()
    {
        base.OnTabLeave();
        StopCountdown();
        // Ad completion owns the business result even when this tab is hidden.
        // The unscaled deadline is checked again by its callback / next tab entry.
        _adTimeoutRoutine = null;
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
            bool bonus=presentation!=null&&(row.PriceType==0||row.PriceType==3);
            var cell = Instantiate(bonus?presentation.bonusCellPrefab:cellPrefab,
                presentation!=null?(bonus?presentation.bonusContent:presentation.fragmentContent):listContent);
            cell.Bind(row, OnCellBuyClicked);
            _cells.Add(cell);
        }

        if (emptyHint != null)
            emptyHint.SetActive(_cells.Count == 0);
        if(presentation!=null)presentation.RefreshLayout();
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
            if (c != null) {c.gameObject.SetActive(false);Destroy(c.gameObject);}
        _cells.Clear();
    }

    // ═══════════════ 购买 ═══════════════

    private void OnCellBuyClicked(ShopCell cell)
    {
        if (cell == null || cell.ShopRow == null) return;

        ExpireAdRequest();
        var row = cell.ShopRow;
        int priceType = row.PriceType;
        int itemId = row.ItemID;
        int count = Mathf.Max(1, row.ItemNumber);
        string itemName = cell.ItemRow?.ItemName ?? row.ShopName ?? "物品";
        string receiptId = "shop:" + row.ID + ":" + Guid.NewGuid().ToString("N");

        // 免费：直接发奖，不弹确认框
        if (priceType == 0)
        {
            ExecuteShopPurchase(row, itemId, count, receiptId);
            return;
        }

        // 未开通广告时使用分享返回奖励，不弹确认框。
        if (priceType == 3)
        {
            if (IsAdRewardInFlight) return;
            UIManager.Instance?.ShowToast("分享返回后领取奖励", 2f);
            ClaimAdReward(row, itemId, count, receiptId);
            return;
        }

        // 金币/钻石：弹确认框
        int price = ShopService.GetPrice(row);
        string currencyLabel = priceType == 2 ? "钻石" : "金币";
        bool confirmationHandled = false;
        UIManager.Instance.ShowConfirm("兑换确认",
            $"确定购买「{itemName}」×{count}？\n消耗 {price} {currencyLabel}",
            confirmed =>
            {
                if (confirmationHandled) return;
                confirmationHandled = true;
                if (!confirmed) return;
                ExecuteShopPurchase(row, itemId, count, receiptId);
            });
    }

    private void ExecuteShopPurchase(ShopTable row, int itemId, int count, string receiptId)
    {
        var result = ShopService.Purchase(row, out string errMsg);
        switch (result)
        {
            case ShopPurchaseResult.Success:
                OnTabRefresh();
                EventBus.Publish(new PlayerDataChangedEvent());
                RewardRevealQueue.ShowItem(receiptId, "shop", itemId, count);
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

    private void ClaimAdReward(ShopTable row, int itemId, int count, string receiptId)
    {
        var request = new AdPurchaseRequest
        {
            row = row, itemId = itemId, count = count, receiptId = receiptId,
            requestedAt = Time.realtimeSinceStartup
        };
        _adRequest = request;
        ResumeAdTimeout();
        try
        {
#if UNITY_EDITOR
            if (EditorAdRequestOverride != null)
                EditorAdRequestOverride(success => CompleteAdRequest(request, success));
            else
#endif
                WeChatRewardedAdProvider.Instance.RequestShareReward("快来一起玩文字割草机！", success => CompleteAdRequest(request, success));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Shop] 分享请求失败: {ex.Message}");
            CompleteAdRequest(request, false);
        }
    }

    private void CompleteAdRequest(AdPurchaseRequest request, bool success)
    {
        if (this == null || request.completed || _adRequest != request) return;
        if (Time.realtimeSinceStartup - request.requestedAt >= AdRequestTimeout) success = false;
        request.completed = true;
        _adRequest = null;
        if (_adTimeoutRoutine != null) { StopCoroutine(_adTimeoutRoutine); _adTimeoutRoutine = null; }

        if (success)
            ExecuteShopPurchase(request.row, request.itemId, request.count, request.receiptId);
        else
            UIManager.Instance?.ShowToast("未能确认分享返回，请重试", 1.5f);
    }

    private void ResumeAdTimeout()
    {
        if (!IsAdRewardInFlight || !isActiveAndEnabled || _adTimeoutRoutine != null) return;
        _adTimeoutRoutine = StartCoroutine(WaitForAdDeadline(_adRequest));
    }

    private void ExpireAdRequest()
    {
        if (IsAdRewardInFlight && Time.realtimeSinceStartup - _adRequest.requestedAt >= AdRequestTimeout)
            CompleteAdRequest(_adRequest, false);
    }

    private System.Collections.IEnumerator WaitForAdDeadline(AdPurchaseRequest request)
    {
        while (!request.completed && Time.realtimeSinceStartup - request.requestedAt < AdRequestTimeout)
        {
            yield return null;
        }
        _adTimeoutRoutine = null;
        CompleteAdRequest(request, false);
    }

    private void OnDestroy()
    {
        if (_adRequest != null) _adRequest.completed = true;
        _adRequest = null;
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
