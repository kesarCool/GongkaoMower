#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class BackpackValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "Backpack";
    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    BackPackPanel panel;
    BackpackPresentation presentation;
    PlayerSaveData Data => PlayerProfileService.Instance.Data;
    BackpackCategoryCatalog originalCatalog, temporaryCatalog;
    void SetSample()
    {
        Data.gold = 832; Data.diamond = 325;
        Data.itemIds = new[] { 1, 2, 101, 102, 103, 104, 105 };
        Data.itemCounts = new[] { 999, 999, 8, 20, 3, 5, 2 };
        Data.characterFragmentKeys = new[] { presentation.characterCatalog.characters.First(c => c.fragmentItemId == 102).characterId };
        Data.characterFragmentValues = new[] { 20 };
    }
    void Reopen() { panel.OnOpen(null); }
    protected override IEnumerator RunChecks()
    {
        fixture.SetProgress(1); hub.OpenBackpack(); yield return new WaitForSecondsRealtime(.4f);
        panel = FindObjectOfType<BackPackPanel>(); presentation = panel.GetComponent<BackpackPresentation>(); originalCatalog = presentation.categoryCatalog;
        Record("Existing Home backpack button opens themed prefab", panel != null && presentation != null);
        Record("Wallet balances appear without both currency inventory records", presentation.VisibleItems.Any(i => i.itemId == 2 && i.count == 325));
        SetSample(); string before = JsonUtility.ToJson(Data); Reopen(); yield return null; yield return null;
        Record("Default tab contains seven item kinds without empty placeholders", presentation.SelectedCategoryId == "all" && presentation.VisibleItems.Count == 7 && presentation.grid.totalCount == 7);
        Record("New category labels have font glyphs", "全部货币材料角色碎片".All(c => BattleChineseFontRuntime.LoadedFont.HasCharacter(c)));
        Record("Balances ignore stale legacy inventory currency values", presentation.VisibleItems.First(i => i.itemId == 1).count == 832 && presentation.VisibleItems.First(i => i.itemId == 2).count == 325);
        Record("Fragment recorded in both stores appears once", presentation.VisibleItems.Count(i => i.itemId == 102) == 1 && presentation.VisibleItems.First(i => i.itemId == 102).count == 20);
        var gridLayout = presentation.grid.content.GetComponent<GridLayoutGroup>();
        Record("Grid keeps four columns and reference card dimensions", gridLayout.constraintCount == 4 && gridLayout.cellSize == new Vector2(194, 250));
        var cards = presentation.grid.content.GetComponentsInChildren<BackpackItemView>();
        Record("Every visible card has an icon and explicit quantity", cards.Length == 7 && cards.All(c => c.icon.sprite != null && c.countLabel.text.StartsWith("× ")));
        yield return Capture("backpack-all-1080x1920");
        var categoryTabs = presentation.categoryContent.GetComponentsInChildren<BackpackCategoryTab>();
        categoryTabs[1].button.onClick.Invoke(); yield return null;
        Record("Currency tab filters by table type", presentation.SelectedCategoryId == "currency" && presentation.VisibleItems.Count == 2 && presentation.VisibleItems.All(i => i.type == 1));
        Record("Only selected tab has a button face", categoryTabs.Count(t => t.face.enabled) == 1 && categoryTabs[1].face.enabled);
        cards = presentation.grid.content.GetComponentsInChildren<BackpackItemView>();
        Record("Recycled currency cards clear fragment badges", cards.All(c => !c.fragmentMark.gameObject.activeSelf));
        yield return Capture("backpack-currency-1080x1920");
        categoryTabs[2].button.onClick.Invoke(); yield return null;
        Record("Materials tab contains five existing fragment kinds", presentation.VisibleItems.Count == 5 && presentation.VisibleItems.All(i => i.type == 2));
        Record("Category count uses kinds rather than quantities", presentation.countLabel.text == "共 5 种");
        yield return Capture("backpack-materials-1080x1920");
        var knife = presentation.grid.content.GetComponentsInChildren<BackpackItemView>().First(c => c.Item.itemId == 102); knife.button.onClick.Invoke(); yield return null;
        var tip = UIManager.Instance.GetOrCreateItemTooltip();
        Record("Item click opens tooltip and marks selected card", tip.gameObject.activeSelf && presentation.SelectedItemId == 102 && knife.face.sprite == knife.selectedSprite);
        Record("Tooltip retains table description and current count", Field<TMPro.TextMeshProUGUI>(tip, "descText").text.Contains("收集可解锁/进阶飞刀娃") && Field<TMPro.TextMeshProUGUI>(tip, "descText").text.Contains("当前拥有：20"));
        yield return Capture("backpack-tooltip-1080x1920");
        Field<Button>(tip, "dismissMask").onClick.Invoke(); yield return null; yield return null;
        Record("Outside dismissal clears selected card", !tip.gameObject.activeSelf && presentation.SelectedItemId == 0 && knife.face.sprite == knife.normalSprite);
        knife.button.onClick.Invoke(); yield return null; presentation.SelectCategory("currency"); yield return null;
        Record("Category switch dismisses tooltip and selection", !tip.gameObject.activeSelf && presentation.SelectedItemId == 0);
        Data.itemIds = new[] { 102 }; Data.itemCounts = new[] { 1 }; Data.characterFragmentKeys = new string[0]; Data.characterFragmentValues = new int[0];
        Reopen(); presentation.SelectCategory("materials"); yield return null;
        Record("Single item still shows quantity one", presentation.grid.content.GetComponentsInChildren<BackpackItemView>().Single().countLabel.text == "× 1");
        Data.itemIds = new int[0]; Data.itemCounts = new int[0]; Data.characterFragmentKeys = new[] { presentation.characterCatalog.characters.First(c => c.fragmentItemId == 102).characterId }; Data.characterFragmentValues = new[] { 13 };
        Reopen(); presentation.SelectCategory("materials"); yield return null;
        Record("Legacy fragment-only storage remains visible", presentation.VisibleItems.Count == 1 && presentation.VisibleItems[0].itemId == 102 && presentation.VisibleItems[0].count == 13);
        Data.characterFragmentKeys = new string[0]; Data.characterFragmentValues = new int[0]; Reopen(); presentation.SelectCategory("materials"); yield return null;
        Record("Empty category keeps tabs and category-specific message", presentation.grid.totalCount == 0 && presentation.emptyState.activeSelf && presentation.emptyTitle.text == "暂时没有材料" && presentation.categoryContent.GetComponentsInChildren<BackpackCategoryTab>().Length == 3);
        yield return Capture("backpack-materials-empty-1080x1920");
        Data.gold = 0; Data.diamond = 0; Reopen(); yield return null;
        Record("Empty inventory contains no padded cells", presentation.VisibleItems.Count == 0 && presentation.grid.content.childCount == 0 && presentation.emptyTitle.text == "背包空空的");
        yield return Capture("backpack-empty-1080x1920");
        var simulated = new List<BackPackItemInfo>();
        for (int i = 0; i < 36; i++) simulated.Add(new BackPackItemInfo { itemId = 1000 + i, itemName = "测试道具" + i, type = 3, count = i + 1, description = "扩展类型测试", grade = 2 });
        simulated.Add(new BackPackItemInfo { itemId = 1, itemName = "金币", type = 1, count = 832 });
        temporaryCatalog = Instantiate(originalCatalog);
        var extra = new List<BackpackCategoryCatalog.Category>(originalCatalog.categories);
        for (int i = 3; i <= 9; i++) extra.Add(new BackpackCategoryCatalog.Category { id = "test-" + i, label = i == 9 ? "其他物品类型" : "类型" + i, itemTypes = new[] { i }, icon = presentation.fallbackIcon });
        temporaryCatalog.categories = extra.ToArray(); presentation.categoryCatalog = temporaryCatalog; presentation.Present(simulated); yield return null; yield return null;
        Record("Unknown future types remain visible in All", presentation.VisibleItems.Count == 37);
        Record("Category strip scrolls as types expand", presentation.categoryContent.rect.width > presentation.categoryScroll.viewport.rect.width && presentation.categoryScroll.horizontal && !presentation.categoryScroll.vertical);
        presentation.SelectCategory("test-9"); yield return null;
        Record("Selected overflow tab is brought into view", presentation.categoryScroll.horizontalNormalizedPosition > .8f);
        presentation.SelectCategory("test-3"); yield return null; yield return null;
        Record("Configured future category filters without code changes", presentation.VisibleItems.Count == 36 && presentation.VisibleItems.All(i => i.type == 3));
        Record("Long inventory uses a bounded cell pool", presentation.grid.content.childCount > 0 && presentation.grid.content.childCount < 36);
        presentation.grid.RefillCells(32); yield return null; yield return null;
        Record("Grid reaches last simulated item", presentation.grid.content.GetComponentsInChildren<BackpackItemView>().Any(c => c.Item.itemId == 1035));
        presentation.SelectCategory("currency"); yield return null;
        Record("Changing filter resets grid to top", presentation.grid.content.GetComponentsInChildren<BackpackItemView>().Single().Item.itemId == 1 && Mathf.Abs(presentation.grid.content.anchoredPosition.y) < 1);
        presentation.categoryCatalog = originalCatalog; SetSample(); Reopen(); yield return null;
        Record("Filtering and tooltips did not mutate sample inventory", before == JsonUtility.ToJson(Data));
        changeResolution(1080, 2400); yield return Capture("backpack-long-1080x2400");
        changeResolution(1080, 960); yield return null; yield return null;
        var fit = panel.GetComponent<DialogThemeLayout>();
        Record("Short viewport clamps modal and allows vertical scrolling", fit.panel.rect.height <= 800.1f && presentation.grid.content.rect.height > presentation.grid.viewport.rect.height);
        yield return Capture("backpack-short-1080x960");
        Data.itemIds = new int[0]; Data.itemCounts = new int[0]; Data.characterFragmentKeys = new string[0]; Data.characterFragmentValues = new int[0]; Data.gold = Data.diamond = 0; Reopen(); yield return null; yield return null;
        Record("Short empty state scales inside content area", presentation.emptyVisuals.localScale.y * presentation.emptyVisuals.rect.height <= ((RectTransform)presentation.emptyState.transform).rect.height + 1);
        yield return Capture("backpack-empty-short-1080x960");
        SetSample(); Reopen(); changeResolution(360, 640); yield return Capture("backpack-phone-360x640"); changeResolution(1080, 1920);
        presentation.SelectCategory("materials"); yield return null;
        presentation.grid.content.GetComponentsInChildren<BackpackItemView>()[0].button.onClick.Invoke(); yield return null;
        Field<Button>(panel, "closeButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
        Record("Close returns to roadmap and dismisses tooltip", !panel.gameObject.activeInHierarchy && !tip.gameObject.activeSelf && map.gameObject.activeInHierarchy);
        hub.OpenBackpack(); yield return new WaitForSecondsRealtime(.3f);
        Record("Reopening has one set of tabs and default All filter", presentation.categoryContent.GetComponentsInChildren<BackpackCategoryTab>().Length == 3 && presentation.SelectedCategoryId == "all");
        UIManager.Instance.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        Destroy(temporaryCatalog); temporaryCatalog = null;
    }
    protected override void OnDestroy()
    {
        if (presentation != null && originalCatalog != null) presentation.categoryCatalog = originalCatalog;
        if (temporaryCatalog != null) Destroy(temporaryCatalog);
        base.OnDestroy();
    }
}
#endif
