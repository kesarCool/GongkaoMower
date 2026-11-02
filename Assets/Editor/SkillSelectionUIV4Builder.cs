#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reproducible styling of the existing UGUI prefabs, preserving their GUIDs.</summary>
public static class SkillSelectionUIV4Builder
{
    private const string Dir = "Assets/Res/image/Game/select/";
    private const string Prefabs = "Assets/Prefab/CardSelect/";
    private static TMP_FontAsset font;
    private static Material textMaterial;
    private static readonly Color Ink = Hex("493627"), Muted = Hex("76634C");

    [MenuItem("工具/技能选卡/应用已确认的 V4 样式")]
    private static void ApplyMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式再应用 Prefab 样式。");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("当前 Prefab 有未保存编辑，请先保存。");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        if (font == null) throw new InvalidOperationException("未找到工程现有微软雅黑 TMP 字体。");
        ConfigureTextMaterial();
        ImportSprites();
        var slot = BuildSlot(false);
        var bond = BuildSlot(true);
        StyleTemplates();
        StyleCard(bond);
        StylePanel(slot);
        return "V4 已应用：CardView、CardSelectionPanel、两种专用槽位，以及四类模板。";
    }

    private static void ConfigureTextMaterial()
    {
        const string path = Dir + "SkillSelectTextV4.mat";
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (textMaterial == null)
        {
            textMaterial = new Material(font.material) { name = "SkillSelectTextV4" };
            AssetDatabase.CreateAsset(textMaterial, path);
        }
        textMaterial.CopyPropertiesFromMaterial(font.material);
        textMaterial.SetColor("_FaceColor", Color.white);
        foreach (var key in new [] { "_OutlineWidth", "_OutlineSoftness", "_FaceDilate" })
            if (textMaterial.HasProperty(key)) textMaterial.SetFloat(key, 0);
        if (textMaterial.HasProperty("_UnderlayColor")) textMaterial.SetColor("_UnderlayColor", Color.clear);
        foreach (var keyword in new [] { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "GLOW_ON" }) textMaterial.DisableKeyword(keyword);
        EditorUtility.SetDirty(textMaterial); AssetDatabase.SaveAssetIfDirty(textMaterial);
    }

    private static void ImportSprites()
    {
        foreach (var file in Directory.GetFiles(Dir, "*.png"))
        {
            string path = file.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            int border = path.Contains("panel_cream") ? 48 : path.Contains("hud_panel") ? 28 :
                path.Contains("card_") || path.Contains("selection_frame") ? 36 :
                path.Contains("button_refresh") ? 28 : path.Contains("skill_level_badge") ? 12 :
                path.Contains("bonded_slot") ? 18 : path.Contains("bonded_group") || path.Contains("badge_") ? 20 :
                path.Contains("icon_ad") ? 0 : 24;
            importer.spriteBorder = Vector4.one * border;
            importer.SaveAndReimport();
        }
    }

    private static void StyleTemplates()
    {
        string[] names = { "PassiveSkill", "New", "Upgrade", "Breakthrough" };
        string[] kinds = { "passive", "new", "upgrade", "breakthrough" };
        string[] labels = { "被动", "新技能", "升级", "满级" };
        string[] colors = { "315E7B", "335B2C", "754C1E", "7C352C" };
        for (int i = 0; i < names.Length; i++)
        {
            var template = AssetDatabase.LoadAssetAtPath<RoguelikeCardTemplate>("Assets/ScriptableObject/Roguelike/Card Template/Template_" + names[i] + ".asset");
            if (template == null) throw new InvalidOperationException("缺少卡片模板 " + names[i]);
            template.background = Sprite("card_" + kinds[i] + "_9slice");
            template.labelText = labels[i];
            template.labelColor = Hex(colors[i]);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
        }
    }

    private static SkillSlotCell BuildSlot(bool bonded)
    {
        string path = Prefabs + (bonded ? "SkillSelectBondV4.prefab" : "SkillSelectSlotV4.prefab");
        var go = new GameObject(bonded ? "SkillSelectBondV4" : "SkillSelectSlotV4", typeof(RectTransform));
        try
        {
            var root = (RectTransform)go.transform;
            Rect(root, 0, 0, bonded ? 72 : 124, bonded ? 72 : 96);
            var bg = Image(root, "Background", bonded ? "bonded_slot_9slice" : "skill_slot_empty_9slice", 0, 0, bonded ? 72 : 124, bonded ? 72 : 96);
            var icon = Image(root, "Icon", null, bonded ? 4 : 20, bonded ? 4 : 6, bonded ? 64 : 84, bonded ? 64 : 84);
            icon.type = UnityEngine.UI.Image.Type.Simple;
            icon.preserveAspect = true;
            icon.enabled = false;
            var frame = Image(root, "MatchFrame", bonded ? "bonded_slot_highlight_9slice" : "equipped_match_frame_9slice", 0, 0, bonded ? 72 : 124, bonded ? 72 : 96);
            if (bonded) frame.transform.SetSiblingIndex(1);
            frame.gameObject.SetActive(false);
            var badge = Image(root, "LevelBadge", "skill_level_badge_9slice", 60, 64, 64, 32);
            var level = Text(badge.transform, "Level", "Lv.1", 0, 0, 64, 32, 29, Ink, true, TextAlignmentOptions.Center);
            badge.gameObject.SetActive(false);
            var cell = go.AddComponent<SkillSlotCell>();
            var so = new SerializedObject(cell);
            Ref(so, "iconImage", icon); Ref(so, "levelText", level); Ref(so, "slotBackground", bg);
            Ref(so, "levelBadge", badge.gameObject); Ref(so, "highlightFrame", frame);
            Ref(so, "activeSprite", Sprite(bonded ? "bonded_slot_9slice" : "skill_slot_base_9slice"));
            Ref(so, "passiveSprite", Sprite(bonded ? "bonded_slot_9slice" : "skill_slot_passive_9slice"));
            Ref(so, "emptySprite", Sprite(bonded ? "bonded_slot_9slice" : "skill_slot_empty_9slice"));
            so.ApplyModifiedPropertiesWithoutUndo();
            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            return asset.GetComponent<SkillSlotCell>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void StyleCard(SkillSlotCell bond)
    {
        string path = Prefabs + "CardView.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var cv = root.GetComponent<CardView>();
            var rt = (RectTransform)root.transform;
            Rect(rt, 0, 0, 276, 952);
            Rect(cv.background.rectTransform, 0, 0, 276, 800);
            cv.background.sprite = Sprite("card_passive_9slice");
            cv.background.type = UnityEngine.UI.Image.Type.Sliced;
            cv.background.color = Color.white;
            cv.background.raycastTarget = true;
            var oldGlow = root.transform.Find("BorderGlow");
            if (oldGlow != null) oldGlow.gameObject.SetActive(false);
            var oldGroup = root.transform.Find("group");
            if (oldGroup != null) oldGroup.gameObject.SetActive(false);

            cv.badgeImage = cv.labelText.GetComponentInParent<Image>();
            Rect(cv.badgeImage.rectTransform, 62, 24, 152, 56);
            cv.badgeImage.type = UnityEngine.UI.Image.Type.Sliced;
            cv.badgeImage.color = Color.white;
            cv.passiveBadge = Sprite("badge_passive_9slice"); cv.newBadge = Sprite("badge_new_9slice");
            cv.upgradeBadge = Sprite("badge_upgrade_9slice"); cv.breakthroughBadge = Sprite("badge_breakthrough_9slice");
            cv.badgeImage.sprite = cv.passiveBadge;
            Rect(cv.labelText.rectTransform, 0, 0, 152, 56);
            Style(cv.labelText, 32, Hex("315E7B"), true, TextAlignmentOptions.Center);
            cv.labelText.text = "被动";
            Rect(cv.icon.rectTransform, 59, 102, 158, 158);
            cv.icon.preserveAspect = true; cv.icon.type = UnityEngine.UI.Image.Type.Simple;
            Rect(cv.titleText.rectTransform, 12, 274, 252, 58);
            Style(cv.titleText, 44, Ink, true, TextAlignmentOptions.Center);
            cv.titleText.enableAutoSizing = true; cv.titleText.fontSizeMin = 34; cv.titleText.fontSizeMax = 44;
            cv.levelText = Text(rt, "LevelChange", "新获得 · Lv.1", 12, 330, 252, 44, 32, Hex("315E7B"));
            cv.gainLabelText = Text(rt, "GainLabel", "防御提升", 12, 382, 252, 44, 34, Hex("315E7B"), true);
            cv.gainValueText = Text(rt, "GainValue", "+20%", 12, 430, 252, 68, 62, Hex("315E7B"), true);
            cv.gainValueText.enableAutoSizing = true; cv.gainValueText.fontSizeMin = 28; cv.gainValueText.fontSizeMax = 62;
            cv.dividerImage = Image(rt, "Divider", null, 24, 511, 228, 2);
            cv.dividerImage.color = new Color(0.19f, 0.37f, 0.48f, 0.2f);

            var viewport = Node(rt, "DescriptionViewport", 22, 530, 232, 212);
            var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            cv.descText.transform.SetParent(viewport, false);
            Rect(cv.descText.rectTransform, 0, 0, 232, 212);
            Style(cv.descText, 38, Muted, false, TextAlignmentOptions.Top);
            cv.descText.enableWordWrapping = true;
            cv.descText.lineSpacing = 8;
            cv.descText.overflowMode = TextOverflowModes.Overflow;
            scroll.viewport = viewport; scroll.content = cv.descText.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            cv.descriptionScroll = scroll;
            cv.descriptionScrollHint = Text(rt, "ScrollHint", "上滑查看详情", 18, 771, 240, 24, 20, Muted).gameObject;
            cv.descriptionScrollHint.SetActive(false);

            cv.selectionFrame = Image(rt, "SelectionFrameV4", "selection_frame_9slice", 0, 0, 276, 800);
            cv.selectionFrame.gameObject.SetActive(false);
            cv.bondedPanel = Node(rt, "BondedPanelV4", 0, 824, 276, 128);
            var bondBG = cv.bondedPanel.GetComponent<Image>() ?? cv.bondedPanel.gameObject.AddComponent<Image>();
            bondBG.sprite = Sprite("bonded_group_9slice"); bondBG.type = UnityEngine.UI.Image.Type.Sliced;
            bondBG.color = Color.white; bondBG.raycastTarget = true;
            Text(cv.bondedPanel, "Title", "突破组合", 0, 4, 276, 36, 28, Muted, true);
            cv.bondedGroup = Node(cv.bondedPanel, "Icons", 0, 44, 276, 80);
            cv.bondedIconPrefab = bond;
            cv.bondedPanel.gameObject.SetActive(false);
            cv.clickButton.targetGraphic = cv.background;
            cv.clickButton.transition = Selectable.Transition.None;
            var so = new SerializedObject(cv);
            so.FindProperty("clickTargetScale").floatValue = 1.035f;
            so.FindProperty("clickAnimDuration").floatValue = 0.18f;
            so.FindProperty("fadeOutDuration").floatValue = 0.12f;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = graphic == cv.background || graphic == bondBG || graphic == hit;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void StylePanel(SkillSlotCell slot)
    {
        string path = Prefabs + "CardSelectionPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var panel = root.GetComponent<CardSelectionPanel>();
            var rootRT = (RectTransform)root.transform;
            rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;
            var blocker = root.GetComponent<Image>();
            blocker.sprite = null; blocker.color = new Color(0.157f, 0.208f, 0.129f, 0.24f); blocker.raycastTarget = true;
            panel.backgroundBlocker = blocker;
            var design = Node(rootRT, "DesignRootV4", 0, 0, 1080, 1920);
            design.anchorMin = design.anchorMax = design.pivot = new Vector2(0.5f, 0.5f);
            design.anchoredPosition = Vector2.zero;
            panel.panelRoot = design.gameObject;
            var paper = (root.transform.Find("panel") ?? design.Find("panel")) as RectTransform;
            paper.SetParent(design, false); Rect(paper, 54, 138, 972, 1624);
            var paperImage = paper.GetComponent<Image>();
            paperImage.sprite = Sprite("panel_cream_9slice"); paperImage.type = UnityEngine.UI.Image.Type.Sliced;
            paperImage.color = Color.white; paperImage.raycastTarget = false;
            paper.SetAsFirstSibling();

            var title = root.transform.Find("Title") ?? design.Find("Title");
            title.SetParent(design, false); Rect((RectTransform)title, 102, 228, 728, 66);
            var mainTitle = title.GetComponent<TextMeshProUGUI>();
            Style(mainTitle, 54, Ink, true, TextAlignmentOptions.Left); mainTitle.text = "选一招，继续割草";
            Text(design, "Eyebrow", "升级时刻", 102, 182, 580, 44, 32, Muted, false, TextAlignmentOptions.Left);
            var levelBadge = Image(design, "PlayerLevelBadge", "badge_upgrade_9slice", 830, 190, 146, 56);
            panel.playerLevelText = Text(levelBadge.transform, "Level", "升级", 0, 0, 146, 56, 32, Hex("754C1E"), true);

            var hud = Image(design, "EquippedHUDV4", "hud_panel_9slice", 102, 314, 876, 272);
            Text(hud.transform, "ActiveLabel", "主动", 26, 30, 110, 44, 32, Muted, true, TextAlignmentOptions.Left);
            panel.activeCountText = Text(hud.transform, "ActiveCount", "0/5", 26, 76, 110, 42, 32, Muted, false, TextAlignmentOptions.Left);
            Text(hud.transform, "PassiveLabel", "被动", 26, 154, 110, 44, 32, Muted, true, TextAlignmentOptions.Left);
            panel.passiveCountText = Text(hud.transform, "PassiveCount", "0/5", 26, 200, 110, 42, 32, Muted, false, TextAlignmentOptions.Left);
            var so = new SerializedObject(panel);
            var active = (RectTransform)so.FindProperty("skillSlotParent").objectReferenceValue;
            var passive = (RectTransform)so.FindProperty("passiveSlotParent").objectReferenceValue;
            SetupRow(active, hud.transform, 154, 26);
            SetupRow(passive, hud.transform, 154, 150);
            Ref(so, "skillCellPrefab", slot); so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var old in new [] { "Text (TMP)", "Text (TMP) (1)" })
            {
                var t = root.transform.Find(old); if (t != null) t.gameObject.SetActive(false);
            }
            var cards = (root.transform.Find("CardsContainer") ?? design.Find("CardsContainer")) as RectTransform;
            cards.SetParent(design, false); Rect(cards, 102, 610, 876, 952);
            RemoveLayout(cards);
            for (int i = 0; i < panel.cardSlots.Length; i++)
            {
                var card = panel.cardSlots[i];
                Rect((RectTransform)card.transform, i * 300, 0, 276, 952);
                PrefabUtility.RecordPrefabInstancePropertyModifications(card.transform);
            }
            var refreshArea = Node(design, "RefreshAreaV4", 260, 1582, 560, 152);
            var button = panel.refreshButton;
            button.transform.SetParent(refreshArea, false);
            Rect((RectTransform)button.transform, 0, 0, 560, 152);
            var buttonImage = button.GetComponent<Image>();
            buttonImage.sprite = null; buttonImage.color = Color.clear; buttonImage.raycastTarget = true;
            var buttonVisual = Image(button.transform, "VisualV4", "button_refresh_normal_9slice", 0, 8, 560, 113);
            buttonVisual.transform.SetAsFirstSibling(); button.targetGraphic = buttonVisual;
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.pressedSprite = Sprite("button_refresh_pressed_9slice"); state.disabledSprite = Sprite("button_refresh_disabled_9slice");
            button.spriteState = state;
            var refreshLabel = panel.refreshCountText;
            refreshLabel.rectTransform.anchorMin = refreshLabel.rectTransform.anchorMax = refreshLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            refreshLabel.rectTransform.anchoredPosition = new Vector2(0, 11.5f);
            refreshLabel.rectTransform.sizeDelta = new Vector2(480, 84);
            Style(refreshLabel, 38, Ink, true, TextAlignmentOptions.Center);
            refreshLabel.text = "换一组 · 1次";
            var ad = Image(button.transform, "AdVideoIconV4", "icon_ad_video", 82, 42, 48, 36);
            ad.type = UnityEngine.UI.Image.Type.Simple; ad.preserveAspect = true;
            panel.adVideoIcon = ad.gameObject; ad.gameObject.SetActive(false);
            var hint = Text(design, "OutsideHintV4", "点击卡片即可获得", 100, 1794, 880, 48, 30, Hex("596050"));
            var layout = root.GetComponent<SkillSelectionLayout>() ?? root.AddComponent<SkillSelectionLayout>();
            layout.designRoot = design; layout.paper = paper; layout.refreshArea = refreshArea; layout.hint = hint.rectTransform;
            panel.selectionLayout = layout;
            design.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SetupRow(RectTransform row, Transform parent, float x, float y)
    {
        row.SetParent(parent, false); Rect(row, x, y, 684, 96); RemoveLayout(row);
        var image = row.GetComponent<Image>(); if (image != null) { image.enabled = false; image.raycastTarget = false; }
        var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 16; group.childAlignment = TextAnchor.UpperLeft;
        group.childControlHeight = group.childControlWidth = false;
        group.childForceExpandWidth = group.childForceExpandHeight = false;
    }

    private static void RemoveLayout(RectTransform rt)
    {
        foreach (var layout in rt.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(layout);
        var fitter = rt.GetComponent<ContentSizeFitter>(); if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
    }

    private static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
    {
        var found = parent.Find(name) as RectTransform;
        if (found == null)
        {
            found = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            found.SetParent(parent, false);
        }
        Rect(found, x, y, w, h);
        return found;
    }

    private static Image Image(Transform parent, string name, string sprite, float x, float y, float w, float h)
    {
        var rt = Node(parent, name, x, y, w, h);
        var image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? Sprite(sprite) : null;
        image.type = sprite != null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.color = Color.white; image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float x, float y, float w, float h,
        float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = Node(parent, name, x, y, w, h);
        var tmp = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
        Style(tmp, size, color, bold, align); tmp.text = text;
        return tmp;
    }

    private static void Style(TextMeshProUGUI tmp, float size, Color color, bool bold, TextAlignmentOptions align)
    {
        tmp.font = font; tmp.fontSharedMaterial = textMaterial;
        tmp.fontSize = size; tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color; tmp.alignment = align;
        tmp.enableAutoSizing = false; tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow; tmp.raycastTarget = false;
        tmp.margin = Vector4.zero;
    }

    private static Sprite Sprite(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + name + ".png");
        if (sprite == null) throw new InvalidOperationException("缺少选卡素材 " + name);
        return sprite;
    }

    private static void Ref(SerializedObject so, string field, UnityEngine.Object value)
    {
        so.FindProperty(field).objectReferenceValue = value;
    }

    private static void Rect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
    }

    private static Color Hex(string hex)
    {
        Color color; ColorUtility.TryParseHtmlString("#" + hex, out color); return color;
    }
}
#endif
