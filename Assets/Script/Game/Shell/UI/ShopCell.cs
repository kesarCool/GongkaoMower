using ProtoTable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店商品格子：绑定 ShopTable + ItemTable 数据，处理购买点击。
/// 挂在 ShopCell Prefab 上。
/// </summary>
[DisallowMultipleComponent]
public class ShopCell : MonoBehaviour
{
    [Header("UI 子控件")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private TextMeshProUGUI textPrice;
    [SerializeField] private TextMeshProUGUI textLimit;
    [SerializeField] private TextMeshProUGUI textOldPrice; // 预留划线原价
    [SerializeField] private Button btnBuy;
    [SerializeField] private GameObject imgSoldOut;
    [SerializeField] private GameObject redDotTag; // 免费/广告标签

    public ShopTable ShopRow { get; private set; }
    public ItemTable ItemRow { get; private set; }

    private System.Action<ShopCell> _onBuyClicked;

    private void OnEnable()
    {
        if (btnBuy != null) btnBuy.onClick.AddListener(OnBuyClicked);
    }

    private void OnDisable()
    {
        if (btnBuy != null) btnBuy.onClick.RemoveListener(OnBuyClicked);
    }

    private void Reset()
    {
        if (icon == null) icon = transform.Find("Icon")?.GetComponent<Image>();
        if (textName == null) textName = transform.Find("TextName")?.GetComponent<TextMeshProUGUI>();
        if (textPrice == null) textPrice = transform.Find("TextPrice")?.GetComponent<TextMeshProUGUI>();
        if (textLimit == null) textLimit = transform.Find("TextLimit")?.GetComponent<TextMeshProUGUI>();
        if (btnBuy == null) btnBuy = transform.Find("BtnBuy")?.GetComponent<Button>();
        if (imgSoldOut == null) imgSoldOut = transform.Find("ImgSoldOut")?.gameObject;
        if (redDotTag == null) redDotTag = transform.Find("RedDotTag")?.gameObject;
    }

    public void Bind(ShopTable shopRow, System.Action<ShopCell> onBuyClicked)
    {
        ShopRow = shopRow;
        _onBuyClicked = onBuyClicked;

        // 查 ItemTable 获取图标/名称
        ItemRow = null;
#if USE_FB_TABLE
        if (TableManager.Instance != null)
        {
            var dict = TableManager.Instance.GetTable<ItemTable>();
            if (dict != null && dict.TryGetValue(shopRow.ItemID, out var obj))
                ItemRow = obj as ItemTable;
        }
#endif

        // 图标
        if (icon != null)
        {
            if (ItemRow != null && !string.IsNullOrEmpty(ItemRow.IconPath))
            {
                var sprite = Resources.Load<Sprite>(ItemRow.IconPath);
                if (sprite != null) icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            else
            {
                icon.enabled = false;
            }
        }

        // 名称
        if (textName != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(textName);
            textName.text = shopRow.LatticeName ?? "";
        }

        // 价格
        int price = ShopService.GetPrice(shopRow);
        string currencyLabel;
        if (shopRow.PriceType == 0)
            currencyLabel = "";
        else if (shopRow.PriceType == 3)
            currencyLabel = "";
        else
            currencyLabel = shopRow.PriceType == 2 ? "钻石" : "金币";
        if (textPrice != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(textPrice);
            if (shopRow.PriceType == 0)
                textPrice.text = "免费";
            else if (shopRow.PriceType == 3)
                textPrice.text = "看广告";
            else
                textPrice.text = $"{price} {currencyLabel}";
        }

        // 划线原价（预留）
        if (textOldPrice != null)
        {
            textOldPrice.gameObject.SetActive(false); // 暂不使用
        }

        // 限购计数
        RefreshLimitDisplay();

        // 售罄遮罩
        bool soldOut = ShopService.IsSoldOut(shopRow);
        if (imgSoldOut != null) imgSoldOut.SetActive(soldOut);
        if (btnBuy != null) btnBuy.interactable = !soldOut;

        // 红点标签：免费和看广告显示（售罄不显示）
        if (redDotTag != null)
            redDotTag.SetActive(!soldOut && (shopRow.PriceType == 0 || shopRow.PriceType == 3));
    }

    /// <summary>购买后外部调用来刷新限购显示。</summary>
    public void RefreshLimitDisplay()
    {
        if (ShopRow == null || textLimit == null) return;

        int remaining = ShopService.GetRemainingPurchases(ShopRow);
        if (remaining < 0) // 不限购
        {
            textLimit.text = "";
        }
        else
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(textLimit);
            int total = ShopRow.PurchaseNum;
            textLimit.text = $"限购{remaining}/{total}";
        }
    }

    private void OnBuyClicked()
    {
        UiClickSound.Play();
        _onBuyClicked?.Invoke(this);
    }
}
