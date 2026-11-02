#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

/// <summary>Rebuilds presentation inside existing utility prefabs; preserves their GUIDs.</summary>
public static class DialogsUIBuilder
{
    const string Character = "Assets/Res/image/Home/character/";
    [MenuItem("工具/Home UI/Apply Utility Dialogs")]
    private static void Menu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("dialogs");
        BuildLevels(); BuildConfirm(); BuildSettings(); BuildAbout(); BuildTooltip(); BuildToast();
        AssetDatabase.SaveAssets();
        return "Six utility prefabs updated. Backpack uses BackpackUIBuilder.Apply().";
    }
    static GameObject Load(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        foreach (var component in root.GetComponents<Component>())
            if (!(component is RectTransform) && !(component is CanvasRenderer) && !(component is Image) && !(component is CanvasGroup) && !(component is UIPanelBase)
                && !(component is ItemTooltip) && !(component is UiToastPanel)) UnityEngine.Object.DestroyImmediate(component);
        foreach (var child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        U.Fill((RectTransform)root.transform);
        var image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        image.enabled = true; image.sprite = null; image.color = Color.clear; image.raycastTarget = true;
        var panel = root.GetComponent<UIPanelBase>(); if (panel != null) panel.backgroundBlocker = image;
        return root;
    }
    static void Save(GameObject root, string path)
    {
        try { PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static RectTransform Center(Transform parent, string name, float w, float h)
    {
        var rt = U.Node(parent, name, 0, 0, w, h);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero;
        return rt;
    }
    static RectTransform Box(GameObject root, string title, float w, float h, out Button close)
    {
        var rt = Center(root.transform, "Panel", w, h);
        var face = rt.gameObject.AddComponent<Image>(); face.sprite = U.Sprite("dialog_panel_cream_9slice"); face.type = Image.Type.Sliced; face.raycastTarget = true;
        var p = root.AddComponent<DialogThemeLayout>(); p.panel = rt; p.width = w; p.height = h;
        p.title = U.Text(rt, "Title", title, 44, 26, w - 156, 72, 48, U.Ink, true, TextAlignmentOptions.Left);
        var divider = U.Image(rt, "Divider", null, 42, 116, w - 84, 2); divider.color = U.ColorOf("E5D8B7");
        var image = U.Image(rt, "Close", U.Sprite("close_button_9slice"), w - 110, 24, 72, 72);
        close = U.Button(image); close.transition = Selectable.Transition.SpriteSwap;
        var state = close.spriteState; state.pressedSprite = U.Sprite("close_button_pressed_9slice"); close.spriteState = state;
        U.Image(image.transform, "Icon", U.Sprite("icon_close"), 19, 19, 34, 34);
        return rt;
    }
    static ScrollRect Scroll(Transform parent, string name, float top, float bottom, float margin, float contentHeight)
    {
        var view = U.Node(parent, name, 0, 0, 784, 600); U.Fill(view);
        view.offsetMin = new Vector2(margin, bottom); view.offsetMax = new Vector2(-margin, -top);
        view.gameObject.AddComponent<RectMask2D>(); var hit = view.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        var content = U.Node(view, "Content", 0, 0, 0, contentHeight); content.anchorMax = new Vector2(1, 1);
        var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        return scroll;
    }
    static Button Action(Transform parent, string name, float x, float y, float w, float h, string label, bool secondary = false)
    {
        string prefix = secondary ? "growth" : "character_start";
        var face = U.Image(parent, name, U.Sprite(prefix + "_normal_9slice", Character), x, y, w, h); var button = U.Button(face);
        button.transition = Selectable.Transition.SpriteSwap;
        var state = button.spriteState; state.pressedSprite = U.Sprite(prefix + "_pressed_9slice", Character); state.disabledSprite = U.Sprite(prefix + "_disabled_9slice", Character); button.spriteState = state;
        U.Text(face.transform, "Label", label, 0, 0, w, h, 38, U.Ink, true);
        return button;
    }
    static void Bottom(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 0); rt.anchoredPosition = new Vector2(x, y);
    }
    static GameObject LevelCell(bool chapter)
    {
        var root = new GameObject(chapter ? "LevelThemeChapter" : "LevelThemeRow", typeof(RectTransform));
        U.Rect((RectTransform)root.transform, 0, 0, 836, chapter ? 90 : 122);
        root.AddComponent<LayoutElement>().preferredHeight = chapter ? 90 : 122;
        var face = root.AddComponent<Image>(); face.sprite = U.Sprite(chapter ? "home_badge_sand_9slice" : "level_row_normal_9slice"); face.type = Image.Type.Sliced;
        if (chapter)
        {
            var cell = root.AddComponent<LevelSelectChapterHeaderCell>();
            U.Image(root.transform, "Icon", U.Sprite("nav_book"), 24, 24, 42, 42);
            var label = U.Text(root.transform, "Title", "", 84, 0, 730, 90, 37, U.Ink, true, TextAlignmentOptions.Left);
            U.Ref(cell, "titleText", label); U.Ref(cell, "background", face);
            var so = new SerializedObject(cell); so.FindProperty("unlockedColor").colorValue = Color.white; so.FindProperty("lockedColor").colorValue = new Color(1, 1, 1, .65f); so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            var cell = root.AddComponent<LevelSelectLevelRowCell>(); var button = U.Button(face);
            var icon = U.Image(root.transform, "StateIcon", U.Sprite("icon_flag"), 26, 39, 44, 44);
            var title = U.Text(root.transform, "Name", "", 96, 13, 400, 56, 38, U.Ink, true, TextAlignmentOptions.Left);
            var status = U.Text(root.transform, "State", "", 98, 71, 400, 34, 27, U.Muted, false, TextAlignmentOptions.Left);
            var stars = U.Text(root.transform, "Stars", "", 550, 25, 258, 72, 49, U.ColorOf("B88126"), true, TextAlignmentOptions.Right);
            U.Ref(cell, "titleText", title); U.Ref(cell, "starText", stars); U.Ref(cell, "clickButton", button); U.Ref(cell, "background", face); U.Ref(cell, "stateText", status); U.Ref(cell, "stateIcon", icon);
            U.Ref(cell, "availableSprite", U.Sprite("level_row_selected_9slice")); U.Ref(cell, "clearedSprite", U.Sprite("level_row_normal_9slice")); U.Ref(cell, "lockedSprite", U.Sprite("level_row_locked_9slice"));
            U.Ref(cell, "flagIcon", U.Sprite("icon_flag")); U.Ref(cell, "checkIcon", U.Sprite("icon_check")); U.Ref(cell, "lockIcon", U.Sprite("icon_lock"));
        }
        try { return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefab/LevelSelect/" + root.name + ".prefab"); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void BuildLevels()
    {
        var chapter = LevelCell(true); var level = LevelCell(false);
        var template = new GameObject("LevelThemeCell", typeof(RectTransform), typeof(LayoutElement), typeof(LevelSelectThemeCell));
        U.Rect((RectTransform)template.transform, 0, 0, 836, 122); template.GetComponent<LayoutElement>().preferredHeight = 122;
        var themed = template.GetComponent<LevelSelectThemeCell>();
        themed.chapter = UnityEngine.Object.Instantiate(chapter, template.transform, false).GetComponent<LevelSelectChapterHeaderCell>();
        themed.level = UnityEngine.Object.Instantiate(level, template.transform, false).GetComponent<LevelSelectLevelRowCell>();
        U.Fill((RectTransform)themed.chapter.transform); U.Fill((RectTransform)themed.level.transform); themed.chapter.gameObject.SetActive(false);
        var uniform = PrefabUtility.SaveAsPrefabAsset(template, "Assets/Prefab/LevelSelect/LevelThemeCell.prefab"); UnityEngine.Object.DestroyImmediate(template);
        const string path = "Assets/Prefab/LevelSelect/LevelSelectPanel.prefab"; var root = Load(path);
        var panel = Box(root, "选择关卡", 920, 1420, out var close); U.Ref(root.GetComponent<LevelSelectPanel>(), "closeButton", close);
        var standard = Scroll(panel, "LevelsScroll", 146, 88, 42, 900);
        var view = (RectTransform)standard.transform; var content = standard.content; UnityEngine.Object.DestroyImmediate(standard);
        var group = content.gameObject.AddComponent<VerticalLayoutGroup>(); group.spacing = 16; group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = true; group.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var loop = view.gameObject.AddComponent<LoopVerticalScrollRect>(); loop.viewport = view; loop.content = content; loop.horizontal = false; loop.movementType = LoopScrollRectBase.MovementType.Clamped; loop.scrollSensitivity = 45;
        var driver = view.gameObject.AddComponent<LevelSelectLoopScrollDriver>(); U.Ref(driver, "chapterHeaderPrefab", chapter); U.Ref(driver, "levelRowPrefab", level);
        U.Ref(driver, "themedCellPrefab", uniform);
        var hint = U.Text(panel, "Hint", "点击已解锁关卡，确认后进入挑战", 0, 0, 836, 44, 28, U.Muted); Bottom(hint.rectTransform, 0, 28);
        Save(root, path);
    }
    static void BuildConfirm()
    {
        const string path = "Assets/Prefab/PopConfirm/PopConfirmPanel.prefab"; var root = Load(path);
        root.GetComponent<Image>().color = new Color(0, 0, 0, .24f);
        var panel = Box(root, "确认操作", 840, 520, out var close); UnityEngine.Object.DestroyImmediate(close.gameObject); UnityEngine.Object.DestroyImmediate(panel.Find("Divider").gameObject);
        var layout = root.GetComponent<DialogThemeLayout>(); layout.kind = DialogThemeLayout.Kind.Confirm;
        layout.title.alignment = TextAlignmentOptions.Center; U.Rect(layout.title.rectTransform, 56, 26, 728, 72);
        var scroll = Scroll(panel, "MessageScroll", 128, 180, 56, 120); layout.scroll = scroll;
        var text = U.Text(scroll.content, "Message", "", 0, 0, 728, 120, 38, U.Ink, false, TextAlignmentOptions.Center); text.enableWordWrapping = true; U.Fill(text.rectTransform); layout.body = text;
        layout.cancel = Action(panel, "Cancel", 0, 0, 310, 100, "取消", true); Bottom((RectTransform)layout.cancel.transform, -176, 42);
        layout.confirm = Action(panel, "Confirm", 0, 0, 310, 100, "确认"); Bottom((RectTransform)layout.confirm.transform, 176, 42);
        var dialog = root.GetComponent<UiConfirmDialog>(); dialog.titleText = (TextMeshProUGUI)layout.title; dialog.messageText = text; dialog.okButton = layout.confirm; dialog.cancelButton = layout.cancel;
        Save(root, path);
    }
    static Toggle AudioSwitch(Transform parent, string name, float y, bool music, out TextMeshProUGUI status)
    {
        var row = U.Image(parent, name, U.Sprite("home_card_cream_9slice"), 0, y, 780, 138);
        U.Image(row.transform, "Icon", U.Sprite(music ? "icon_audio_music" : "icon_audio_sound"), 30, 37, 64, 64);
        U.Text(row.transform, "Name", music ? "背景音乐" : "音效", 120, 24, 430, 52, 40, U.Ink, true, TextAlignmentOptions.Left);
        status = U.Text(row.transform, "Status", "", 122, 80, 430, 34, 28, U.Muted, false, TextAlignmentOptions.Left);
        var track = U.Image(row.transform, "Switch", U.Sprite("switch_track_9slice"), 610, 35, 132, 68); track.raycastTarget = true;
        var toggle = track.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = track; toggle.transition = Selectable.Transition.None; toggle.isOn = true;
        var knob = U.Image(track.transform, "Knob", U.Sprite("switch_knob"), 0, 0, 60, 60); var krt = knob.rectTransform; krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(.5f, .5f); krt.anchoredPosition = new Vector2(29, 0);
        var theme = track.gameObject.AddComponent<ThemeAudioToggle>(); theme.track = track; theme.knob = krt;
        return toggle;
    }
    static void BuildSettings()
    {
        const string path = "Assets/Prefab/Setting/SettingsPanel.prefab";
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(path); var legacyFont = original.GetComponentInChildren<InputField>(true).textComponent.font;
        var root = Load(path); var panel = Box(root, "设置", 864, 960, out var close); var settings = root.GetComponent<SettingsPanel>();
        U.Ref(settings, "closeButton", close);
        var scroll = Scroll(panel, "SettingsScroll", 146, 146, 42, 660); var content = scroll.content;
        U.Ref(settings, "soundToggle", AudioSwitch(content, "MusicRow", 0, true, out var bgm)); U.Ref(settings, "soundText", bgm);
        U.Ref(settings, "sfxToggle", AudioSwitch(content, "SoundRow", 158, false, out var sfx)); U.Ref(settings, "sfxText", sfx);
        var line = U.Image(content, "SectionDivider", null, 0, 338, 780, 2); line.color = U.ColorOf("E5D8B7");
        U.Image(content, "GiftIcon", U.Sprite("icon_gift_box"), 0, 386, 56, 56);
        U.Text(content, "GiftHeading", "礼包码兑换", 76, 380, 650, 68, 42, U.Ink, true, TextAlignmentOptions.Left);
        var inputFace = U.Image(content, "GiftCodeInput", U.Sprite("home_card_cream_9slice"), 0, 474, 538, 106); inputFace.raycastTarget = true;
        var input = inputFace.gameObject.AddComponent<InputField>(); input.targetGraphic = inputFace; input.transition = Selectable.Transition.None; input.lineType = InputField.LineType.SingleLine; input.characterLimit = 128;
        var inputClip = U.Node(input.transform, "TextViewport", 22, 12, 494, 82); inputClip.gameObject.AddComponent<RectMask2D>();
        var value = U.Node(inputClip, "Text", 0, 0, 494, 82).gameObject.AddComponent<Text>(); value.font = legacyFont; value.fontSize = 34; value.color = U.Ink; value.alignment = TextAnchor.MiddleLeft; value.supportRichText = false; value.raycastTarget = false;
        var placeholder = U.Node(inputClip, "Placeholder", 0, 0, 494, 82).gameObject.AddComponent<Text>(); placeholder.font = legacyFont; placeholder.fontSize = 32; placeholder.color = U.Muted; placeholder.text = "请输入礼包码"; placeholder.alignment = TextAnchor.MiddleLeft; placeholder.raycastTarget = false;
        input.textComponent = value; input.placeholder = placeholder; inputFace.gameObject.AddComponent<WechatInputFieldEnabler>();
        U.Ref(settings, "giftCodeInput", input); U.Ref(settings, "claimButton", Action(content, "Redeem", 560, 474, 220, 106, "兑换"));
        U.Text(content, "GiftHint", "每日限兑换一次", 4, 602, 772, 52, 29, U.Muted, false, TextAlignmentOptions.Left);
        var version = U.Text(panel, "Version", "", 0, 0, 530, 60, 29, U.Muted, false, TextAlignmentOptions.Left); Bottom(version.rectTransform, -127, 45); U.Ref(settings, "versionText", version);
        var about = Action(panel, "About", 0, 0, 210, 84, "关于", true); Bottom((RectTransform)about.transform, 285, 34); U.Ref(settings, "aboutButton", about);
        Save(root, path);
    }
    static void BuildAbout()
    {
        const string path = "Assets/Prefab/Setting/AboutPanel.prefab";
        string credits = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<TextMeshProUGUI>(true).OrderByDescending(t => t.text.Length).First().text;
        var root = Load(path); var panel = Box(root, "关于", 864, 1040, out var close); var about = root.GetComponent<AboutPanel>(); U.Ref(about, "closeButton", close);
        U.Image(panel, "GameIcon", U.Sprite("nav_swords"), 52, 166, 96, 96);
        U.Text(panel, "GameName", "文字割草机", 176, 160, 620, 68, 50, U.Ink, true, TextAlignmentOptions.Left);
        var version = U.Text(panel, "Version", "", 179, 240, 600, 42, 30, U.Muted, false, TextAlignmentOptions.Left); U.Ref(about, "versionText", version);
        var scroll = Scroll(panel, "CreditsScroll", 336, 72, 52, 800);
        var text = U.Text(scroll.content, "Credits", credits, 0, 0, 760, 800, 31, U.Muted, false, TextAlignmentOptions.TopLeft); text.enableWordWrapping = true; U.Fill(text.rectTransform);
        var contentFitter = scroll.content.gameObject.AddComponent<ContentSizeFitter>(); contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = scroll.content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
        Save(root, path);
    }
    static void BuildTooltip()
    {
        const string path = "Assets/Prefab/Common/ItemTooltip.prefab"; var root = Load(path);
        var mask = U.Button(root.GetComponent<Image>());
        var panel = Center(root.transform, "Panel", 620, 300); var face = panel.gameObject.AddComponent<Image>(); face.sprite = U.Sprite("dialog_panel_cream_9slice"); face.type = Image.Type.Sliced; face.raycastTarget = true;
        var name = U.Text(panel, "Name", "", 40, 32, 540, 52, 40, U.Ink, true, TextAlignmentOptions.TopLeft); name.enableWordWrapping = true;
        var scroll = Scroll(panel, "DescriptionScroll", 116, 38, 40, 150);
        var desc = U.Text(scroll.content, "Description", "", 0, 0, 540, 150, 34, U.Muted, false, TextAlignmentOptions.TopLeft); desc.enableWordWrapping = true; U.Fill(desc.rectTransform);
        var tip = root.GetComponent<ItemTooltip>(); U.Ref(tip, "nameText", name); U.Ref(tip, "descText", desc); U.Ref(tip, "dismissMask", mask); U.Ref(tip, "contentPanel", panel); U.Ref(tip, "descriptionScroll", scroll);
        var so = new SerializedObject(tip); so.FindProperty("minWidth").floatValue = 520; so.FindProperty("maxWidth").floatValue = 800; so.FindProperty("maxHeight").floatValue = 640; so.ApplyModifiedPropertiesWithoutUndo();
        Save(root, path);
    }
    static void BuildToast()
    {
        const string path = "Assets/Prefab/Common/UiToastPanel.prefab"; var root = Load(path); var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(880, 120);
        var face = root.GetComponent<Image>(); face.sprite = U.Sprite("dialog_panel_cream_9slice"); face.type = Image.Type.Sliced; face.color = Color.white; face.raycastTarget = false;
        var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        var icon = U.Image(root.transform, "Icon", U.Sprite("icon_notice_info"), 32, 0, 52, 52); icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0, .5f); icon.rectTransform.anchoredPosition = new Vector2(32, 0);
        var message = U.Text(root.transform, "Message", "", 108, 28, 720, 64, 34, U.Ink, false, TextAlignmentOptions.MidlineLeft); message.enableWordWrapping = true; U.Fill(message.rectTransform); message.rectTransform.offsetMin = new Vector2(108, 28); message.rectTransform.offsetMax = new Vector2(-40, -28);
        U.Ref(root.GetComponent<UiToastPanel>(), "messageText", message);
        var theme = root.AddComponent<DialogThemeLayout>(); theme.kind = DialogThemeLayout.Kind.Toast; theme.panel = rt; theme.body = message; theme.width = 880; theme.height = 360;
        Save(root, path);
    }
}
#endif
