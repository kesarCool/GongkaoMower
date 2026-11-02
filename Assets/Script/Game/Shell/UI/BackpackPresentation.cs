using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Filters a read-only inventory snapshot and presents it through a four-column pooled grid.</summary>
public sealed class BackpackPresentation : MonoBehaviour, LoopScrollPrefabSource, LoopScrollDataSource
{
    public BackpackCategoryCatalog categoryCatalog;
    public CharacterCatalog characterCatalog;
    public LoopVerticalScrollRect grid;
    public ScrollRect categoryScroll;
    public RectTransform categoryContent;
    public BackpackCategoryTab tabTemplate;
    public BackpackItemView cardPrefab;
    public TMP_Text heading, countLabel, emptyTitle, emptyDescription;
    public GameObject emptyState;
    public RectTransform emptyVisuals;
    public Sprite coinIcon, gemIcon, fallbackIcon;
    public string SelectedCategoryId { get; private set; }
    public int SelectedItemId { get; private set; }
    public IReadOnlyList<BackPackItemInfo> VisibleItems => visible;
    private readonly List<BackPackItemInfo> items = new List<BackPackItemInfo>(), visible = new List<BackPackItemInfo>();
    private readonly List<BackpackCategoryTab> tabs = new List<BackpackCategoryTab>();
    private readonly List<BackpackCategoryCatalog.Category> categories = new List<BackpackCategoryCatalog.Category>();
    private readonly Stack<Transform> pool = new Stack<Transform>();
    private readonly Dictionary<int, Sprite> icons = new Dictionary<int, Sprite>();
    private RectTransform poolRoot;
    private ItemTooltip shownTooltip;

    public void Present(List<BackPackItemInfo> source)
    {
        var labels = new StringBuilder("全部物品共种当前拥有：×背包空空的暂时没有闯关与兑换获得的物品会收在这里点击物品查看说明");
        foreach (var item in source) labels.Append(item.itemName);
        if (categoryCatalog != null && categoryCatalog.categories != null)
            foreach (var category in categoryCatalog.categories) if (category != null) labels.Append(category.label).Append(category.heading);
        WarmFont(labels.ToString());
        items.Clear(); items.AddRange(source); items.Sort(CompareItems);
        BuildTabs();
        if (categories.Count > 0) SelectCategory(categories[0].id);
        else ApplyFilter(null);
    }
    private int CompareItems(BackPackItemInfo a, BackPackItemInfo b)
    {
        int priorityA = a.type == 2 ? 0 : a.type == 1 ? 1 : 2;
        int priorityB = b.type == 2 ? 0 : b.type == 1 ? 1 : 2;
        int compare = priorityA.CompareTo(priorityB); if (compare != 0) return compare;
        if (a.type == 2 && b.type == 2 && characterCatalog != null)
        {
            string equipped = PlayerProfileService.Instance.Data.equippedCharacterId;
            var hero = characterCatalog.Get(equipped);
            if (hero != null)
            {
                if (a.itemId == hero.fragmentItemId && b.itemId != a.itemId) return -1;
                if (b.itemId == hero.fragmentItemId && a.itemId != b.itemId) return 1;
            }
        }
        compare = a.type.CompareTo(b.type); return compare != 0 ? compare : a.itemId.CompareTo(b.itemId);
    }
    private void BuildTabs()
    {
        foreach (var tab in tabs) { tab.gameObject.SetActive(false); Destroy(tab.gameObject); }
        tabs.Clear(); categories.Clear();
        if (categoryCatalog == null || categoryCatalog.categories == null) return;
        var ids = new HashSet<string>();
        foreach (var category in categoryCatalog.categories)
        {
            if (category == null || string.IsNullOrWhiteSpace(category.id) || !ids.Add(category.id)) continue;
            categories.Add(category);
            var tab = Instantiate(tabTemplate, categoryContent, false); tab.gameObject.SetActive(true);
            tab.Bind(category, () => { UiClickSound.Play(); SelectCategory(category.id); }); tabs.Add(tab);
        }
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
        categoryScroll.horizontalNormalizedPosition = 0;
    }
    public bool SelectCategory(string id)
    {
        var category = categories.Find(c => c.id == id); if (category == null) return false;
        SelectedCategoryId = id;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Select(categories[i].id == id);
        ApplyFilter(category); EnsureTabVisible(categories.IndexOf(category)); return true;
    }
    private void ApplyFilter(BackpackCategoryCatalog.Category category)
    {
        ClearSelection(); visible.Clear();
        foreach (var item in items) if (item.itemId > 0 && item.count > 0 && (category == null || category.Matches(item.type))) visible.Add(item);
        heading.text = category == null || category.allItems ? "全部物品" : string.IsNullOrEmpty(category.heading) ? category.label : category.heading;
        countLabel.text = "共 " + visible.Count + " 种";
        emptyState.SetActive(visible.Count == 0);
        bool all = category == null || category.allItems;
        emptyTitle.text = all ? "背包空空的" : "暂时没有" + category.label;
        emptyDescription.text = all ? "闯关与兑换获得的物品会收在这里" : "闯关与兑换获得的" + category.label + "会收在这里";
        grid.StopAllCoroutines(); grid.StopMovement(); grid.prefabSource = this; grid.dataSource = this;
        if (Application.isPlaying) grid.ClearCells();
        grid.totalCount = visible.Count; grid.RefillCells(0);
    }
    private void EnsureTabVisible(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
        float excess = categoryContent.rect.width - categoryScroll.viewport.rect.width; if (excess <= 0) return;
        var rt = (RectTransform)tabs[index].transform;
        float left = rt.anchoredPosition.x, right = left + rt.rect.width;
        float current = categoryScroll.horizontalNormalizedPosition * excess;
        if (left < current) categoryScroll.horizontalNormalizedPosition = Mathf.Clamp01(left / excess);
        else if (right > current + categoryScroll.viewport.rect.width) categoryScroll.horizontalNormalizedPosition = Mathf.Clamp01((right - categoryScroll.viewport.rect.width) / excess);
    }
    private void ShowItem(BackpackItemView card)
    {
        UiClickSound.Play(); SelectedItemId = card.Item.itemId; RefreshSelection();
        WarmFont(card.Item.itemName + card.Item.description);
        ItemTooltip.Show(BackpackItemView.DisplayName(card.Item.itemName), (card.Item.description ?? "") + "\n当前拥有：" + card.Item.count, (RectTransform)card.transform);
        if (UIManager.Instance != null) shownTooltip = UIManager.Instance.GetOrCreateItemTooltip();
    }
    // Some newly introduced Chinese characters are absent from the project's dynamic atlas.
    // Populate missing glyphs before assigning labels; no font source or baked atlas is duplicated.
    private static void WarmFont(string text)
    {
        BattleChineseFontRuntime.EnsureLoaded(); var font = BattleChineseFontRuntime.LoadedFont; if (font == null) return;
        var missing = new StringBuilder(); var seen = new HashSet<char>();
        foreach (char ch in text) if (!char.IsWhiteSpace(ch) && !char.IsControl(ch) && seen.Add(ch) && !font.HasCharacter(ch)) missing.Append(ch);
        if (missing.Length > 0) { string unused; font.TryAddCharacters(missing.ToString(), out unused); }
    }
    public void ClearSelection()
    {
        if (SelectedItemId != 0 && shownTooltip != null && shownTooltip.gameObject.activeSelf) ItemTooltip.Hide();
        SelectedItemId = 0; shownTooltip = null; RefreshSelection();
    }
    private void RefreshSelection()
    {
        if (grid == null || grid.content == null) return;
        foreach (var card in grid.content.GetComponentsInChildren<BackpackItemView>()) card.SetSelected(card.Item.itemId == SelectedItemId && SelectedItemId != 0);
    }
    private void LateUpdate()
    {
        if (SelectedItemId != 0 && shownTooltip != null && !shownTooltip.gameObject.activeSelf) ClearSelection();
        if (emptyVisuals != null && emptyState.activeSelf)
            emptyVisuals.localScale = Vector3.one * Mathf.Min(1, ((RectTransform)emptyState.transform).rect.height / 470);
    }
    private void OnDisable() { ClearSelection(); }
    private Transform PoolRoot()
    {
        if (poolRoot == null) { poolRoot = new GameObject("BackpackCardPool", typeof(RectTransform)).GetComponent<RectTransform>(); poolRoot.SetParent(transform, false); }
        return poolRoot;
    }
    public GameObject GetObject(int index)
    {
        if (index < 0 || index >= visible.Count) return null;
        if (pool.Count > 0) { var card = pool.Pop(); card.gameObject.SetActive(true); return card.gameObject; }
        var created = Instantiate(cardPrefab, PoolRoot(), false); created.gameObject.SetActive(false); return created.gameObject;
    }
    public void ReturnObject(Transform trans) { trans.gameObject.SetActive(false); trans.SetParent(PoolRoot(), false); pool.Push(trans); }
    public void ProvideData(Transform trans, int index)
    {
        if (index < 0 || index >= visible.Count) return;
        var item = visible[index]; Sprite sprite = null; bool portrait = false;
        if (item.type == 2 && characterCatalog != null)
            foreach (var hero in characterCatalog.characters)
                if (hero != null && hero.fragmentItemId == item.itemId) { sprite = hero.portrait; portrait = sprite != null; break; }
        if (item.itemId == 1) sprite = coinIcon;
        else if (item.itemId == 2) sprite = gemIcon;
        if (sprite == null && !icons.TryGetValue(item.itemId, out sprite))
        {
            sprite = string.IsNullOrEmpty(item.iconPath) ? null : Resources.Load<Sprite>(item.iconPath); icons[item.itemId] = sprite;
        }
        trans.GetComponent<BackpackItemView>().Bind(item, sprite != null ? sprite : fallbackIcon, portrait, item.itemId == SelectedItemId, ShowItem);
    }
}
