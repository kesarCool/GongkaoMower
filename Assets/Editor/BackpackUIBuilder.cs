#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class BackpackUIBuilder
{
    const string Folder = "Assets/Res/image/Home/backpack/";
    [MenuItem("工具/Home UI/Apply Backpack")]
    private static void Menu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("backpack");
        var categories = Categories(); var card = BuildCard();
        const string path = "Assets/Prefab/BackPack/BackPackPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var c in root.GetComponents<Component>())
                if (!(c is RectTransform) && !(c is CanvasRenderer) && !(c is Image) && !(c is CanvasGroup) && !(c is BackPackPanel)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            U.Fill((RectTransform)root.transform);
            var mask = root.GetComponent<Image>() ?? root.AddComponent<Image>(); mask.enabled = true; mask.sprite = null; mask.color = Color.clear; mask.raycastTarget = true;
            var logic = root.GetComponent<BackPackPanel>(); logic.backgroundBlocker = mask;
            var presentation = root.AddComponent<BackpackPresentation>(); presentation.categoryCatalog = categories; presentation.cardPrefab = card;
            presentation.characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            presentation.coinIcon = U.Sprite("icon_coins"); presentation.gemIcon = U.Sprite("icon_gem"); presentation.fallbackIcon = U.Sprite("icon_notice_info");
            var face = U.Image(root.transform, "Panel", U.Sprite("dialog_panel_cream_9slice"), 0, 0, 920, 1040); var panel = face.rectTransform; face.raycastTarget = true;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = Vector2.zero;
            var fit = root.AddComponent<DialogThemeLayout>(); fit.panel = panel; fit.width = 920; fit.height = 1040;
            U.Image(panel, "BagIcon", U.Sprite("icon_backpack"), 42, 32, 60, 60);
            U.Text(panel, "Title", "背包", 122, 26, 620, 72, 48, U.Ink, true, TextAlignmentOptions.Left);
            var closeFace = U.Image(panel, "Close", U.Sprite("close_button_9slice"), 810, 24, 72, 72); var close = U.Button(closeFace);
            U.Image(close.transform, "Icon", U.Sprite("icon_close"), 19, 19, 34, 34);
            var closeSprites = close.spriteState; closeSprites.pressedSprite = U.Sprite("close_button_pressed_9slice"); close.spriteState = closeSprites; close.transition = Selectable.Transition.SpriteSwap;
            Line(panel, "HeaderDivider", 116); Line(panel, "TabsDivider", 254);
            var categoryView = U.Node(panel, "CategoryScroll", 42, 144, 836, 92); categoryView.gameObject.AddComponent<RectMask2D>();
            var tabHit = categoryView.gameObject.AddComponent<Image>(); tabHit.color = Color.clear; tabHit.raycastTarget = true;
            var categoryContent = U.Node(categoryView, "Content", 0, 4, 0, 80);
            var group = categoryContent.gameObject.AddComponent<HorizontalLayoutGroup>(); group.spacing = 24; group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = group.childForceExpandHeight = false;
            var size = categoryContent.gameObject.AddComponent<ContentSizeFitter>(); size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var categoryScroll = categoryView.gameObject.AddComponent<ScrollRect>(); categoryScroll.viewport = categoryView; categoryScroll.content = categoryContent; categoryScroll.horizontal = true; categoryScroll.vertical = false; categoryScroll.movementType = ScrollRect.MovementType.Clamped; categoryScroll.scrollSensitivity = 45;
            presentation.categoryScroll = categoryScroll; presentation.categoryContent = categoryContent; presentation.tabTemplate = BuildTab(categoryContent); presentation.tabTemplate.gameObject.SetActive(false);
            presentation.heading = U.Text(panel, "Heading", "全部物品", 42, 276, 600, 52, 34, U.Ink, true, TextAlignmentOptions.Left);
            presentation.countLabel = U.Text(panel, "Count", "", 650, 276, 228, 52, 29, U.Muted, false, TextAlignmentOptions.Right);
            var view = U.Node(panel, "ItemsScroll", 0, 0, 836, 580); U.Fill(view); view.offsetMin = new Vector2(42, 114); view.offsetMax = new Vector2(-42, -346); view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var content = U.Node(view, "Content", 0, 0, 0, 522); content.anchorMax = new Vector2(1, 1);
            var gridLayout = content.gameObject.AddComponent<GridLayoutGroup>(); gridLayout.cellSize = new Vector2(194, 250); gridLayout.spacing = new Vector2(20, 22); gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gridLayout.constraintCount = 4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var loop = view.gameObject.AddComponent<LoopVerticalScrollRect>(); loop.viewport = view; loop.content = content; loop.horizontal = false; loop.vertical = true; loop.movementType = LoopScrollRectBase.MovementType.Clamped; loop.scrollSensitivity = 45; presentation.grid = loop;
            var empty = U.Node(panel, "EmptyState", 42, 346, 836, 580); empty.anchorMin = new Vector2(0, 0); empty.anchorMax = new Vector2(0, 1); empty.offsetMin = new Vector2(42, 114); empty.offsetMax = new Vector2(878, -346);
            var emptyVisuals = U.Node(empty, "Visuals", 0, 0, 836, 470); Center(emptyVisuals, 0, 0); presentation.emptyVisuals = emptyVisuals;
            var emptyGraphic = U.Image(emptyVisuals, "Halo", U.Sprite("empty_backpack_glow", Folder), 0, 0, 292, 232); Center(emptyGraphic.rectTransform, 0, 42);
            var emptyBag = U.Image(emptyVisuals, "Bag", U.Sprite("icon_backpack"), 0, 0, 160, 160); Center(emptyBag.rectTransform, 0, 46);
            presentation.emptyTitle = U.Text(emptyVisuals, "Title", "背包空空的", 0, 0, 836, 68, 40, U.Ink, true); Center(presentation.emptyTitle.rectTransform, 0, -117);
            presentation.emptyDescription = U.Text(emptyVisuals, "Description", "", 0, 0, 836, 68, 29, U.Muted); presentation.emptyDescription.enableWordWrapping = true; Center(presentation.emptyDescription.rectTransform, 0, -182);
            presentation.emptyState = empty.gameObject; empty.gameObject.SetActive(false);
            var hint = U.Text(panel, "Hint", "点击物品查看说明", 0, 0, 836, 44, 29, U.Muted); hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(.5f, 0); hint.rectTransform.anchoredPosition = new Vector2(0, 25);
            U.Ref(logic, "presentation", presentation); U.Ref(logic, "closeButton", close); U.Ref(logic, "loopScroll", null); U.Ref(logic, "itemCellPrefab", null); U.Ref(logic, "emptyHint", empty.gameObject);
            var so = new SerializedObject(logic); so.FindProperty("minRows").intValue = 0; so.FindProperty("columnsPerRow").intValue = 4; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssetIfDirty(categories);
            return "Backpack updated: configurable category tabs, four-column pooled grid, item tooltip and empty states. Existing prefab GUID retained.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Center(RectTransform rt, float x, float y) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(x, y); }
    static void Line(Transform parent, string name, float y) { var line = U.Image(parent, name, null, 42, y, 836, 2); line.color = U.ColorOf("E5D8B7"); }
    static BackpackCategoryCatalog Categories()
    {
        const string path = "Assets/ScriptableObject/UI/BackpackCategories.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<BackpackCategoryCatalog>(path); if (catalog != null) return catalog;
        Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh();
        catalog = ScriptableObject.CreateInstance<BackpackCategoryCatalog>();
        catalog.categories = new[] {
            new BackpackCategoryCatalog.Category { id = "all", label = "全部", allItems = true, icon = U.Sprite("icon_backpack") },
            new BackpackCategoryCatalog.Category { id = "currency", label = "货币", heading = "货币", itemTypes = new[] { 1 }, icon = U.Sprite("icon_coins") },
            new BackpackCategoryCatalog.Category { id = "materials", label = "材料", heading = "材料 · 角色碎片", itemTypes = new[] { 2 }, icon = U.Sprite("icon_fragment_piece") }
        };
        AssetDatabase.CreateAsset(catalog, path); return catalog;
    }
    static BackpackCategoryTab BuildTab(Transform parent)
    {
        var hit = U.Image(parent, "TabTemplate", null, 0, 0, 230, 80); hit.color = Color.clear;
        var p = hit.gameObject.AddComponent<BackpackCategoryTab>(); p.button = U.Button(hit);
        var layout = hit.gameObject.AddComponent<LayoutElement>(); layout.preferredWidth = 230; layout.preferredHeight = 80;
        p.face = U.Image(hit.transform, "Face", U.Sprite("filter_selected_9slice", "Assets/Res/image/Home/lexicon/"), 0, 0, 230, 80);
        U.Fill(p.face.rectTransform);
        p.icon = U.Image(hit.transform, "Icon", U.Sprite("icon_backpack"), 42, 20, 40, 40);
        p.label = U.Text(hit.transform, "Label", "", 100, 0, 115, 80, 36, U.Ink, true, TextAlignmentOptions.Left); U.Fill(p.label.rectTransform); p.label.rectTransform.offsetMin = new Vector2(100, 0); p.label.rectTransform.offsetMax = new Vector2(-16, 0); return p;
    }
    static BackpackItemView BuildCard()
    {
        var root = new GameObject("BackpackItemCard", typeof(RectTransform)); U.Rect((RectTransform)root.transform, 0, 0, 194, 250);
        try
        {
            var card = root.AddComponent<BackpackItemView>(); var face = root.AddComponent<Image>(); face.sprite = U.Sprite("item_card_normal_9slice", Folder); face.type = Image.Type.Sliced; face.raycastTarget = true; card.face = face; card.button = U.Button(face);
            card.normalSprite = face.sprite; card.selectedSprite = U.Sprite("item_card_selected_9slice", Folder);
            card.iconSurface = U.Image(root.transform, "IconSurface", U.Sprite("item_icon_surface_9slice", Folder), 18, 18, 158, 138);
            card.icon = U.Image(card.iconSurface.transform, "Icon", null, 0, 0, 104, 125); card.icon.preserveAspect = true; Center(card.icon.rectTransform, 0, 0);
            card.fragmentMark = U.Image(root.transform, "Fragment", U.Sprite("icon_fragment_piece"), 25, 111, 38, 38);
            card.gradeDot = U.Image(root.transform, "Grade", U.Sprite("item_icon_surface_9slice", Folder), 151, 29, 10, 10);
            card.nameLabel = U.Text(root.transform, "Name", "", 8, 166, 178, 43, 28, U.Ink, true); card.nameLabel.enableWordWrapping = true; card.nameLabel.enableAutoSizing = true; card.nameLabel.fontSizeMin = 22; card.nameLabel.fontSizeMax = 28; card.nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            card.countLabel = U.Text(root.transform, "Count", "", 8, 209, 178, 36, 29, U.Muted);
            return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefab/BackPack/BackpackItemCard.prefab").GetComponent<BackpackItemView>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
