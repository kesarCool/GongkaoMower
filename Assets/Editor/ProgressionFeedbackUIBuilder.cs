#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

/// <summary>Builds only the new reward panel and registers it in Home. Existing pages remain intact.</summary>
public static class ProgressionFeedbackUIBuilder
{
    const string Folder = "Assets/Prefab/RewardReveal/";
    const string Wheel = "Assets/Res/image/Game/bosswheel/";
    const string Home = "Assets/Scenes/Home.unity";
    static readonly Color Ink = new Color32(73, 54, 39, 255);
    [MenuItem("Tools/Home UI/Apply Progression Feedback")]
    static void Menu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        U.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        U.TextMaterial = AssetDatabase.LoadAssetAtPath<Material>(U.Common + "home_ui_text.mat");
        if (U.Font == null || U.TextMaterial == null) throw new InvalidOperationException("Existing Home font or text material is missing.");
        var card = BuildCard(); var panel = BuildPanel(card); Register(panel);
        AssetDatabase.SaveAssets();
        return "Reward reveal panel registered in Home. Existing Home theme, font and icons reused; no new texture or font asset.";
    }
    static GameObject Root(string name) { return new GameObject(name, typeof(RectTransform)); }
    static TMP_Text Text(Transform p, string name, string value, float x, float y, float w, float h, float size, bool bold = false)
        => U.Text(p, name, value, x, y, w, h, size, Ink, bold);
    static RewardRevealCell BuildCard()
    {
        var root = Root("RewardRevealCard");
        try
        {
            var cell = root.AddComponent<RewardRevealCell>(); cell.group = root.AddComponent<CanvasGroup>();
            var motion = U.Node(root.transform, "Motion", 0, 0, 228, 286); U.Fill(motion); cell.motion = motion;
            var face = U.Image(motion, "Face", U.Sprite("home_card_cream_9slice"), 0, 0, 228, 286); U.Fill(face.rectTransform);
            cell.surface = U.Image(motion, "IconSurface", U.Sprite("home_badge_sand_9slice"), 20, 16, 188, 176);
            cell.surface.rectTransform.anchorMin = cell.surface.rectTransform.anchorMax = new Vector2(.5f, 1);
            cell.surface.rectTransform.pivot = new Vector2(.5f, 1); cell.surface.rectTransform.anchoredPosition = new Vector2(0, -16);
            cell.icon = U.Image(cell.surface.transform, "Icon", null, 0, 0, 130, 130);
            Center(cell.icon.rectTransform, 0, 0);
            cell.fragmentMark = U.Image(cell.surface.transform, "Fragment", U.Sprite("icon_fragment_piece"), 15, 121, 40, 40);
            cell.gradeMark = U.Image(motion, "GradeMark", U.Sprite("home_number_gold_9slice"), 24, 184, 180, 4);
            StretchHorizontal(cell.gradeMark.rectTransform, 24, 24, 4, 192);
            cell.nameLabel = Text(motion, "Name", "", 10, 200, 208, 44, 32, true);
            StretchHorizontal(cell.nameLabel.rectTransform, 10, 10, 48, 200); cell.nameLabel.enableAutoSizing = true;
            cell.nameLabel.fontSizeMin = 22; cell.nameLabel.fontSizeMax = 32; cell.nameLabel.enableWordWrapping = true;
            cell.nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            cell.amountLabel = Text(motion, "Amount", "", 8, 248, 212, 42, 34, true);
            var amount = cell.amountLabel.rectTransform;
            amount.anchorMin = new Vector2(0, 0); amount.anchorMax = new Vector2(1, 0); amount.pivot = new Vector2(.5f, 0);
            amount.anchoredPosition = new Vector2(0, 10); amount.sizeDelta = new Vector2(-16, 42);
            cell.amountLabel.enableAutoSizing = true; cell.amountLabel.fontSizeMin = 24; cell.amountLabel.fontSizeMax = 34;
            cell.coin = U.Sprite("reward_coins", "Assets/Res/image/Home/exchange/");
            cell.gem = U.Sprite("reward_gems", "Assets/Res/image/Home/exchange/");
            cell.fallback = U.Sprite("icon_gift_box");
            cell.characters = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "RewardRevealCard.prefab").GetComponent<RewardRevealCell>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static GameObject BuildPanel(RewardRevealCell card)
    {
        var root = Root("RewardRevealPanel");
        try
        {
            U.Fill((RectTransform)root.transform);
            var view = root.AddComponent<RewardRevealPanel>();
            var mask = root.AddComponent<Image>(); mask.color = new Color32(42, 47, 40, 170); mask.raycastTarget = true;
            view.backgroundBlocker = mask; view.skipButton = U.Button(mask);
            var paper = U.Image(root.transform, "Panel", U.Sprite("dialog_panel_cream_9slice"), 0, 0, 920, 920);
            paper.raycastTarget = true; view.panel = paper.rectTransform; Center(view.panel, 0, 0);
            var fit = root.AddComponent<BattleThemeLayout>(); fit.panel = view.panel; fit.width = fit.height = 920;
            view.glow = U.Image(view.panel, "Glow", U.Sprite("warm_glow", Wheel), 65, 145, 790, 560);
            view.rays = U.Image(view.panel, "Rays", U.Sprite("reveal_rays", Wheel), 100, 180, 720, 540);
            Center(view.rays.rectTransform, 0, -10);
            var emblem = U.Image(view.panel, "GiftEmblem", U.Sprite("icon_gift_box"), 420, 35, 80, 80); emblem.preserveAspect = true;
            view.heading = Text(view.panel, "Title", "恭喜获得", 80, 118, 760, 86, 62, true);
            view.subtitle = Text(view.panel, "Subtitle", "奖励已放入背包", 80, 202, 760, 50, 31);
            view.subtitle.color = U.Muted;
            var viewport = U.Node(view.panel, "RewardsViewport", 56, 275, 808, 374);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var content = U.Node(viewport, "Content", 0, 0, 784, 0); view.content = content;
            view.grid = content.gameObject.AddComponent<GridLayoutGroup>(); view.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            view.grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.scroll = viewport.gameObject.AddComponent<ScrollRect>(); view.scroll.viewport = viewport; view.scroll.content = content;
            view.scroll.horizontal = false; view.scroll.vertical = true; view.scroll.movementType = ScrollRect.MovementType.Clamped; view.scroll.scrollSensitivity = 50;
            var divider = U.Image(view.panel, "Divider", null, 80, 682, 760, 2); divider.color = U.ColorOf("E5D8B7");
            view.hint = Text(view.panel, "Hint", "点击加速揭晓", 70, 706, 780, 52, 29); view.hint.color = U.Muted;
            var buttonFace = U.Image(view.panel, "Collect", U.Sprite("button_primary_normal_9slice"), 264, 783, 392, 96);
            view.collectButton = U.Button(buttonFace); view.collectButton.transition = Selectable.Transition.SpriteSwap;
            var states = view.collectButton.spriteState; states.pressedSprite = U.Sprite("button_primary_pressed_9slice");
            states.disabledSprite = U.Sprite("button_primary_disabled_9slice"); view.collectButton.spriteState = states;
            view.buttonLabel = Text(buttonFace.transform, "Label", "收下", 20, 0, 352, 96, 43, true);
            view.cardPrefab = card; view.characters = card.characters;
            return PrefabUtility.SaveAsPrefabAsset(root, Folder + "RewardRevealPanel.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void Center(RectTransform rt, float x, float y)
    { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(x, y); }
    static void StretchHorizontal(RectTransform rt, float left, float right, float h, float top)
    { rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1); rt.sizeDelta = new Vector2(-left-right, h); rt.anchoredPosition = new Vector2((left-right)/2, -top); }
    static void Register(GameObject prefab)
    {
        Scene scene = SceneManager.GetSceneByPath(Home); bool owned = !scene.IsValid() || !scene.isLoaded;
        if (owned) scene = EditorSceneManager.OpenScene(Home, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Home scene has unsaved changes. Registration did not save over them.");
            var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).Single();
            var so = new SerializedObject(manager); var list = so.FindProperty("panelPrefabs");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab) present = true;
            if (!present)
            {
                int index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
        }
        finally { if (owned) EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
