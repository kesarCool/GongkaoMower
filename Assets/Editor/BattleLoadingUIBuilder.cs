#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class BattleLoadingUIBuilder
{
    public const string PrefabPath = "Assets/Prefab/Home/BattleLoadingPage.prefab";
    [MenuItem("工具/Home UI/Apply Battle Loading Page")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save edited scenes before applying loading UI.");
        U.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        U.TextMaterial = AssetDatabase.LoadAssetAtPath<Material>(U.Common + "home_ui_text.mat");
        BuildPrefab();
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/BattleLoading.unity"); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/BattleLoading.unity", OpenSceneMode.Additive);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleLoadingSceneController>(true)).First();
            var existing = controller.GetComponentInChildren<BattleLoadingPresentation>(true);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), controller.transform);
            var page = instance.GetComponent<BattleLoadingPresentation>();
            U.Ref(controller, "presentation", page); U.Ref(controller, "statusText", page.status);
            U.Ref(controller, "progressSlider", page.progress); U.Ref(controller, "retryButton", page.retry);
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()))
            { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = U.ColorOf("FFF3D8"); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        return "BattleLoading page installed using existing chapter art, hero portraits, common UI and the existing font.";
    }

    private static void BuildPrefab()
    {
        const string Road = "Assets/Res/image/Home/roadmap/", Hero = "Assets/Res/image/Home/character/";
        var root = new GameObject("BattleLoadingPage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        try
        {
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
            var page = root.AddComponent<BattleLoadingPresentation>(); page.canvasGroup = root.GetComponent<CanvasGroup>();
            var block = U.Image(root.transform, "BlockInput", null, 0, 0, 1080, 1920); U.Fill(block.rectTransform); block.color = U.ColorOf("FFF3D8"); block.raycastTarget = true;
            page.chapterBackgrounds = new[] { U.Sprite("map_food_background_1080x1920.jpg", Road), U.Sprite("map_autumn_academy_720x1280.jpg", Road), U.Sprite("map_winter_town_720x1280.jpg", Road) };
            page.background = U.Image(root.transform, "ChapterBackdrop", page.chapterBackgrounds[0], 0, 0, 1080, 1920);
            var wash = U.Image(root.transform, "WarmWash", null, 0, 0, 1080, 1920); U.Fill(wash.rectTransform); wash.color = new Color(1, .96f, .85f, .09f);
            var top = U.Image(root.transform, "TopVeil", U.Sprite("top_safe_gradient_strip"), 0, 0, 1080, 610);
            top.gameObject.AddComponent<BattleLoadingVeilFade>();
            top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.sizeDelta = new Vector2(0, 610);
            var bottom = U.Image(root.transform, "BottomVeil", U.Sprite("bottom_veil", "Assets/Res/image/Home/login/"), 0, 0, 1080, 990);
            bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0); bottom.rectTransform.pivot = new Vector2(.5f, 0);
            bottom.rectTransform.anchoredPosition = Vector2.zero; bottom.rectTransform.sizeDelta = new Vector2(0, 990);
            page.design = U.Node(root.transform, "SafeDesign", 0, 0, 1080, 1920);
            var logo = U.Image(page.design, "GameTitle", U.Sprite("game_title", "Assets/Res/image/Home/login/"), 205, 120, 670, 162); logo.preserveAspect = true;
            var chapterFace = U.Image(page.design, "ChapterBadge", U.Sprite("surface_pill_cream_9slice"), 240, 320, 600, 82);
            page.chapter = U.Text(chapterFace.transform, "Name", "准备下一站", 16, 0, 568, 82, 36, U.Ink, true);
            var glow = U.Image(page.design, "HeroGlow", U.Sprite("hero_glow", Hero), 300, 684, 480, 430); glow.color = new Color(1, 1, 1, .85f);
            U.Image(page.design, "HeroPlatform", U.Sprite("hero_platform", Hero), 360, 1018, 360, 110);
            page.hero = U.Node(page.design, "Hero", 395, 760, 290, 320);
            page.heroPortrait = U.Image(page.hero, "Portrait", null, 0, 0, 290, 320); page.heroPortrait.preserveAspect = true;
            page.characters = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            var paper = U.Image(page.design, "LoadingPanel", U.Sprite("dialog_panel_cream_9slice"), 72, 1250, 936, 520);
            U.Text(paper.transform, "Heading", "准备出发", 42, 30, 600, 62, 38, U.Ink, true, TextAlignmentOptions.Left);
            page.percent = U.Text(paper.transform, "Percent", "0%", 710, 30, 184, 62, 40, U.ColorOf("9A691F"), true, TextAlignmentOptions.Right);
            page.level = U.Text(paper.transform, "StageName", "正在准备关卡", 42, 104, 852, 76, 45, U.Ink, true, TextAlignmentOptions.Left);
            page.level.enableAutoSizing = true; page.level.fontSizeMin = 30; page.level.fontSizeMax = 45;
            page.status = U.Text(paper.transform, "Status", "正在整理行囊…", 42, 186, 852, 48, 30, U.Muted, false, TextAlignmentOptions.Left);
            var track = U.Image(paper.transform, "ProgressTrack", U.Sprite("home_progress_track_9slice"), 42, 254, 852, 54);
            page.progress = track.gameObject.AddComponent<Slider>(); page.progress.minValue = 0; page.progress.maxValue = 1;
            page.progress.interactable = false; page.progress.transition = Selectable.Transition.None;
            var fillArea = U.Node(track.transform, "FillArea", 8, 8, 836, 38);
            var fill = U.Image(fillArea, "Fill", U.Sprite("home_progress_gold_9slice"), 0, 0, 836, 38); U.Fill(fill.rectTransform);
            page.progress.fillRect = fill.rectTransform; page.progress.targetGraphic = track;
            fill.gameObject.AddComponent<RectMask2D>();
            var streak = U.Image(fill.transform, "Shine", null, 0, 0, 54, 38); streak.color = new Color(1, 1, 1, .28f);
            streak.rectTransform.anchorMin = streak.rectTransform.anchorMax = streak.rectTransform.pivot = new Vector2(0, .5f); page.shine = streak.rectTransform;
            var line = U.Image(paper.transform, "TipDivider", null, 42, 344, 852, 2); line.color = U.ColorOf("E5D8B7");
            var tips = U.Node(paper.transform, "Tip", 42, 375, 852, 94); page.tipRoot = tips.gameObject;
            U.Image(tips, "Icon", U.Sprite("nav_book"), 4, 14, 46, 46);
            page.tip = U.Text(tips, "Text", "边走边躲，技能会自动攻击。", 72, 0, 776, 92, 30, U.Muted, false, TextAlignmentOptions.MidlineLeft);
            page.tip.enableWordWrapping = true;
            var retry = U.Image(paper.transform, "Retry", U.Sprite("button_primary_normal_9slice"), 280, 372, 376, 100);
            page.retry = U.Button(retry); page.retry.transition = Selectable.Transition.SpriteSwap;
            var states = page.retry.spriteState; states.pressedSprite = U.Sprite("button_primary_pressed_9slice"); page.retry.spriteState = states;
            U.Text(retry.transform, "Label", "重新加载", 0, 0, 376, 100, 38, U.Ink, true); retry.gameObject.SetActive(false);
            page.progress.SetValueWithoutNotify(0);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
