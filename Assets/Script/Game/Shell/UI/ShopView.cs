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

        var items = ShopCatalog.Instance.NormalItems;
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

        int price = ShopService.GetPrice(cell.ShopRow);
        int count = Mathf.Max(1, cell.ShopRow.ItemNumber);
        string itemName = cell.ItemRow?.ItemName ?? cell.ShopRow.ShopName ?? "物品";

        string currencyLabel = cell.ShopRow.PriceType == 2 ? "钻石" : "金币";

        UIManager.Instance.ShowConfirm("兑换确认",
            $"确定购买「{itemName}」×{count}？\n消耗 {price} {currencyLabel}",
            confirmed =>
            {
                if (!confirmed) return;

                var result = ShopService.Purchase(cell.ShopRow, out string errMsg);
                switch (result)
                {
                    case ShopPurchaseResult.Success:
                        UIManager.Instance.ShowToast($"已购买 {itemName}×{count}", 1.5f);
                        cell.RefreshLimitDisplay();
                        OnTabRefresh(); // 刷新商店列表
                        EventBus.Publish(new PlayerDataChangedEvent()); // 通知其他页签刷新
                        break;

                    case ShopPurchaseResult.SoldOut:
                        UIManager.Instance.ShowToast("已售罄", 1f);
                        break;

                    case ShopPurchaseResult.NotEnoughGold:
                        UIManager.Instance.ShowToast("金币不足", 1f);
                        break;

                    case ShopPurchaseResult.Locked:
                        UIManager.Instance.ShowToast(errMsg, 1f);
                        break;

                    default:
                        Debug.LogWarning($"[Shop] 购买失败: {errMsg}");
                        break;
                }
            });
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
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DateTime today = DateTimeOffset.FromUnixTimeSeconds(now).LocalDateTime;
            DateTime nextReset = today.Date.AddDays(1); // 明天 00:00
            long secsLeft = (long)(nextReset - today).TotalSeconds;

            if (textRefreshCountdown != null)
            {
                if (secsLeft > 0)
                {
                    textRefreshCountdown.text = $"刷新倒计时 {secsLeft / 3600:D2}:{secsLeft / 60 % 60:D2}:{secsLeft % 60:D2}";
                }
                else
                {
                    textRefreshCountdown.text = "即将刷新…";
                    ShopService.CheckAndRefresh();
                    RebuildList();
                }
            }

            if (groupCountdown != null)
                groupCountdown.SetActive(true);

            yield return new WaitForSeconds(1f);
        }
    }
}
