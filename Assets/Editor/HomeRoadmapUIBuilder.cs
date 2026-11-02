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

public static class HomeRoadmapUIBuilder
{
    private const string Art = "Assets/Res/image/Home/roadmap/";
    [MenuItem("工具/Home UI/Apply Roadmap")]
    private static void ApplyMenu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before updating Home assets.");
        U.Prepare("home");
        var node = BuildNode(); BuildMap(node); StyleNavPrefab(); StyleScene();
        return "Home roadmap updated; existing scene and prefab GUIDs preserved.";
    }

    [MenuItem("工具/Home UI/Apply Navigation Entrances")]
    public static string ApplyNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefab/Home/RoadmapView.prefab");
        try
        {
            StyleChapterEntrance(root.GetComponent<HomeChapterMapView>());
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefab/Home/RoadmapView.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Home.unity"); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Save Home scene changes before updating navigation.");
            var hub = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HomeHubController>(true)).First();
            var so = new SerializedObject(hub);
            var group = ((GameObject)so.FindProperty("homeButtonGroup").objectReferenceValue).transform;
            var map = hub.GetComponentInChildren<HomeChapterMapView>(true);
            U.Ref(hub, "openLevelSelectButton", map.chapterText.transform.parent.GetComponent<Button>());
            U.Disable(group, "ButtonChapterLevel");
            PositionBackpack((RectTransform)group.Find("ButtonBackpack"));
            foreach (var component in new Component[] { hub, group.Find("ButtonChapterLevel"), group.Find("ButtonBackpack") })
            {
                if (component == null) continue;
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        return "Whole chapter badge opens level selection; old shortcuts removed; backpack moved lower.";
    }

    private static Button StyleChapterEntrance(HomeChapterMapView map)
    {
        var face = map.chapterText.transform.parent.GetComponent<Image>();
        var button = U.Button(face);
        button.transition = Selectable.Transition.ColorTint;
        var colors = ColorBlock.defaultColorBlock;
        colors.pressedColor = U.ColorOf("EBCF94"); colors.highlightedColor = colors.selectedColor = Color.white;
        colors.fadeDuration = .08f; button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        foreach (var graphic in face.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = graphic == face;
        map.locateButton = null;
        var oldLocate = face.transform.parent.Find("LocateCurrent");
        if (oldLocate != null) UnityEngine.Object.DestroyImmediate(oldLocate.gameObject);
        return button;
    }

    private static void PositionBackpack(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(1, .24f); rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-40, 0);
    }

    private static HomeMapNode BuildNode()
    {
        var root = new GameObject("FoodMapNode", typeof(RectTransform));
        try
        {
            U.Rect((RectTransform)root.transform, 0, 0, 206, 132);
            var node = root.AddComponent<HomeMapNode>();
            var hit = U.Image(root.transform, "HitArea", null, 0, 0, 206, 132); hit.color = Color.clear;
            node.button = U.Button(hit);
            node.face = U.Image(root.transform, "Face", U.Sprite("node_locked_9slice", Art), 0, 0, 165, 80);
            node.face.rectTransform.anchorMin = node.face.rectTransform.anchorMax = node.face.rectTransform.pivot = new Vector2(.5f,.5f);
            node.face.rectTransform.anchoredPosition = Vector2.zero;
            node.stateIcon = U.Image(node.face.transform, "StateIcon", U.Sprite("icon_lock"), 18, 20, 42, 42);
            node.label = U.Text(node.face.transform, "Label", "1-1", 58, 0, 100, 80, 38, U.Ink, true);
            node.selectionRing = U.Image(root.transform, "SelectionRing", U.Sprite("node_selection_ring_9slice", Art), 0, 0, 206, 106);
            node.selectionRing.rectTransform.anchorMin = node.selectionRing.rectTransform.anchorMax = node.selectionRing.rectTransform.pivot = new Vector2(.5f,.5f);
            node.selectionRing.rectTransform.anchoredPosition = Vector2.zero;
            node.selectionRing.gameObject.SetActive(false);
            node.stars = new Image[3];
            for (int i = 0; i < 3; i++) node.stars[i] = U.Image(root.transform, "Star" + (i + 1), U.Sprite("star_filled", Art), 46 + i * 36, -18, 36, 36);
            node.lockedFace = U.Sprite("node_locked_9slice", Art); node.availableFace = U.Sprite("node_available_9slice", Art); node.clearedFace = U.Sprite("node_cleared_9slice", Art);
            node.lockIcon = U.Sprite("icon_lock"); node.flagIcon = U.Sprite("icon_flag"); node.checkIcon = U.Sprite("icon_check");
            node.filledStar = U.Sprite("star_filled", Art); node.emptyStar = U.Sprite("star_empty", Art);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefab/Home/FoodMapNode.prefab"); return asset.GetComponent<HomeMapNode>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildMap(HomeMapNode nodePrefab)
    {
        const string path = "Assets/Prefab/Home/RoadmapView.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            U.Disable(root.transform, "Scroll View");
            var mapRoot = U.Node(root.transform, "IllustratedMap", 0, 0, 1080, 1526.4f); U.Fill(mapRoot);
            var map = root.GetComponent<HomeChapterMapView>() ?? root.AddComponent<HomeChapterMapView>();
            var viewport = U.Node(mapRoot, "MapViewport", 0, 0, 1080, 1526.4f); U.Fill(viewport);
            var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            var content = U.Node(viewport, "Content", 0, 0, 1080, 1920);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f,1); content.anchoredPosition = Vector2.zero;
            var world = U.Node(content, "World", 0, 0, 1080, 1920);
            world.anchorMin = world.anchorMax = world.pivot = new Vector2(.5f,1); world.anchoredPosition = Vector2.zero;
            U.Image(world, "MapBackground", U.Sprite("map_food_background_1080x1920.jpg", Art), 0, 0, 1080, 1920).transform.SetAsFirstSibling();
            var landmarks = U.Node(world, "PlannedLandmarks", 0, 0, 1080, 1920);
            Landmark(landmarks, "Knowledge", 167, 806, "icon_landmark", "landmark_common_badge_9slice", "常识馆");
            Landmark(landmarks, "Formula", 956, 388, "icon_calculator", "landmark_formula_badge_9slice", "公式工坊");
            var layer = U.Node(world, "NodeLayer", 0, 0, 1080, 1920);
            for (int i = layer.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(layer.GetChild(i).gameObject);
            map.nodes = new HomeMapNode[8];
            for (int i = 0; i < 8; i++)
            {
                var node = (GameObject)PrefabUtility.InstantiatePrefab(nodePrefab.gameObject, layer);
                node.name = "Stage" + (i + 1).ToString("00");
                var rt = node.GetComponent<RectTransform>(); rt.pivot = new Vector2(.5f,.5f);
                map.nodes[i] = node.GetComponent<HomeMapNode>();
            }
            var hero = U.Node(world, "SelectedHero", 0, 0, 166.32f, 172.8f); hero.pivot = new Vector2(.5f,0);
            var shadow = U.Image(hero, "Shadow", U.Sprite("hero_shadow", Art), 0, 0, 166, 35);
            shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = shadow.rectTransform.pivot = new Vector2(.5f,0);
            shadow.rectTransform.anchoredPosition = Vector2.zero;
            map.heroPortrait = U.Image(hero, "Portrait", null, 0, 0, 166, 173); U.Fill(map.heroPortrait.rectTransform); map.heroPortrait.preserveAspect = true;
            var chapter = U.Image(mapRoot, "ChapterBadge", U.Sprite("surface_pill_cream_9slice"), 270, 13.44f, 540, 74.88f);
            map.chapterText = U.Text(chapter.transform, "Title", "第1章 · 美食篇", 16, 0, 380, 75, 38, U.Ink, true);
            var progress = U.Image(chapter.transform, "ProgressBadge", U.Sprite("inset_progress_9slice"), 380, 16, 100, 42);
            map.progressText = U.Text(progress.transform, "Count", "0 / 8", 0, 0, 100, 42, 29, U.Muted);
            StyleChapterEntrance(map);
            map.scroll = scroll; map.content = content; map.world = world; map.hero = hero; map.plannedLandmarks = landmarks.gameObject;
            map.characterCatalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            U.Ref(root.GetComponent<HomeRoadmapView>(), "illustratedMap", map);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Landmark(Transform parent, string name, float x, float y, string icon, string badge, string title)
    {
        var rt = U.Node(parent, name, x - 95, y - 60, 190, 170);
        var image = U.Image(rt, "Badge", U.Sprite(badge, Art), 43, 0, 104, 104);
        var button = U.Button(image);
        while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
        var label = U.Image(rt, "CaptionBackground", U.Sprite("landmark_label_9slice", Art), 0, 88, 190, 82);
        U.Image(image.transform, "Icon", U.Sprite(icon), 18, 18, 68, 68);
        U.Text(label.transform, "Title", title, 0, 2, 190, 38, 31, U.Ink, true);
        U.Text(label.transform, "Status", "规划中", 0, 38, 190, 34, 28, U.Muted);
        button.interactable = false;
    }

    private static void StyleNavPrefab()
    {
        const string path = "Assets/Prefab/Home/HomeTabBar.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { StyleNav(root.GetComponent<HomeTabBar>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void StyleNav(HomeTabBar tab)
    {
        var rt = (RectTransform)tab.transform; U.RemoveLayout(rt);
        rt.anchorMin = new Vector2(0,0); rt.anchorMax = new Vector2(1,0); rt.pivot = new Vector2(.5f,0); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0,172.8f);
        var bg = tab.GetComponent<Image>() ?? tab.gameObject.AddComponent<Image>(); bg.sprite = U.Sprite("nav_bar_background_9slice"); bg.type = Image.Type.Sliced; bg.color = Color.white; bg.raycastTarget = false;
        string[] ids = { "shop", "lexicon", "battle", "character", "achievement" };
        string[] objects = { "ButtonShop", "ButtonLexicon", "ButtonBattle", "ButtonRole", "ButtonAchievement" };
        string[] labels = { "兑换", "词汇", "战斗", "角色", "成就" };
        string[] icons = { "nav_exchange", "nav_book", "nav_swords", "nav_hero", "nav_trophy" };
        var so = new SerializedObject(tab); var tabs = so.FindProperty("tabs");
        for (int i = 0; i < ids.Length; i++)
        {
            var buttonRT = tab.transform.Find(objects[i]) as RectTransform;
            U.Rect(buttonRT, i * 216, 0, 216, 172.8f);
            var image = buttonRT.GetComponent<Image>() ?? buttonRT.gameObject.AddComponent<Image>(); image.sprite = null; image.color = Color.clear; image.raycastTarget = true;
            var button = buttonRT.GetComponent<Button>(); button.transition = Selectable.Transition.None;
            U.Disable(buttonRT,"Image"); U.Disable(buttonRT,"icon"); U.Disable(buttonRT,"Text (Legacy)");
            var selected = U.Image(buttonRT,"SelectedBackground",U.Sprite("nav_selected_9slice"),40,9,136,150); selected.transform.SetAsFirstSibling();
            U.Image(buttonRT,"Icon",U.Sprite(icons[i]),60,16,96,96);
            U.Text(buttonRT,"Label",labels[i],0,108,216,56,40,U.Ink,true);
            var badge = buttonRT.GetComponentInChildren<RedDotBadge>(true);
            if (badge == null) { var badgeRT = U.Node(buttonRT,"Notification",154,6,24,24); badge = badgeRT.gameObject.AddComponent<RedDotBadge>(); }
            U.Rect((RectTransform)badge.transform,154,6,24,24);
            foreach(var graphic in badge.GetComponentsInChildren<Graphic>(true))
                if(graphic.gameObject==badge.gameObject) graphic.enabled=false; else graphic.gameObject.SetActive(false);
            var dot = U.Image(badge.transform,"Dot",U.Sprite("notify_dot"),0,0,24,24); dot.gameObject.SetActive(true);
            var bs = new SerializedObject(badge); bs.FindProperty("sourceKey").stringValue = ids[i]; bs.FindProperty("badgeRoot").objectReferenceValue = dot.gameObject;
            bs.FindProperty("countText").objectReferenceValue = null; bs.ApplyModifiedPropertiesWithoutUndo(); badge.gameObject.SetActive(true);
            for (int j = 0; j < tabs.arraySize; j++)
            {
                var entry = tabs.GetArrayElementAtIndex(j); if (entry.FindPropertyRelative("tabId").stringValue != ids[i]) continue;
                entry.FindPropertyRelative("button").objectReferenceValue = button;
                entry.FindPropertyRelative("tabImage").objectReferenceValue = selected;
                entry.FindPropertyRelative("selectedScale").floatValue = 1;
                entry.FindPropertyRelative("badge").objectReferenceValue = badge;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StyleScene()
    {
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Home.unity"); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Home scene has unsaved changes; preserve them before applying the layout.");
        try
        {
            var hub = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HomeHubController>(true)).First();
            var root = hub.transform; U.Fill((RectTransform)root);
            if(root.GetComponent<HomeShellLayout>()==null) root.gameObject.AddComponent<HomeShellLayout>();
            var background = root.Find("Image").GetComponent<Image>(); U.Fill(background.rectTransform); background.sprite=null; background.color=U.ColorOf("F5EED8"); background.raycastTarget=false;
            var top = root.Find("TopBar") as RectTransform; U.Rect(top,0,0,1080,220.8f); top.anchorMax=new Vector2(1,1); top.sizeDelta=new Vector2(0,220.8f);
            var gradient = U.Image(top,"HeaderGradient",U.Sprite("top_safe_gradient_strip"),0,0,1080,226); gradient.rectTransform.anchorMax=new Vector2(1,1); gradient.rectTransform.sizeDelta=new Vector2(0,226); gradient.transform.SetAsFirstSibling();
            var oldTopImage=top.GetComponent<Image>(); if(oldTopImage!=null) oldTopImage.enabled=false;
            var settings = top.Find("ButtonSetting").GetComponent<Button>(); StyleUtility(settings,"icon_settings",null,40,113.28f,113.14f,113.14f);
            var gold = StyleCurrency(top.Find("gold"),175.82f,"icon_coins");
            var gem = StyleCurrency(top.Find("gem"),442.58f,"icon_gem");
            var view = root.Find("viewContainer") as RectTransform; U.Fill(view); view.offsetMin=new Vector2(0,172.8f); view.offsetMax=new Vector2(0,-220.8f);
            var tab = root.GetComponentInChildren<HomeTabBar>(true); StyleNav(tab); U.Ref(tab,"viewContainer",view);
            var oldMap = view.GetComponentInChildren<HomeRoadmapView>(true); U.Fill((RectTransform)oldMap.transform); U.Disable(oldMap.transform,"Scroll View");
            var map = oldMap.GetComponent<HomeChapterMapView>();
            var buttons = view.Find("homeButton") ?? root.Find("homeButton"); buttons.SetParent(root,false); U.Fill((RectTransform)buttons);
            foreach(var image in buttons.GetComponents<Image>()) { image.raycastTarget=false; image.enabled=false; }
            var start = buttons.Find("ButtonCome").GetComponent<Button>();
            StyleUtility(start,"icon_swords","开始游戏",234.9f,0,610.2f,126.36f);
            var startRT=(RectTransform)start.transform; startRT.anchorMin=startRT.anchorMax=new Vector2(.5f,0); startRT.pivot=new Vector2(.5f,0); startRT.anchoredPosition=new Vector2(0,194.4f);
            var startFace=start.GetComponent<Image>(); startFace.sprite=U.Sprite("button_primary_normal_9slice");
            start.transition=Selectable.Transition.SpriteSwap; var state=start.spriteState; state.pressedSprite=U.Sprite("button_primary_pressed_9slice"); state.disabledSprite=U.Sprite("button_primary_disabled_9slice"); start.spriteState=state;
            var tip=U.Image(buttons,"SelectedStageTip",U.Sprite("info_pill_cream_9slice"),0,0,308,56.2f);
            tip.rectTransform.anchorMin=tip.rectTransform.anchorMax=tip.rectTransform.pivot=new Vector2(.5f,0); tip.rectTransform.anchoredPosition=new Vector2(0,331.56f);
            map.selectedLevelText=U.Text(tip.transform,"Label","美食篇 · 关卡1-1",0,0,308,56.2f,32,U.Muted);
            var levels=buttons.Find("ButtonChapterLevel").GetComponent<Button>(); StyleUtility(levels,"icon_map","关卡",0,0,115.56f,133.92f);
            var bag=buttons.Find("ButtonBackpack").GetComponent<Button>(); StyleUtility(bag,"icon_backpack","背包",0,0,115.56f,133.92f);
            PositionBackpack((RectTransform)bag.transform); levels.gameObject.SetActive(false);
            map.startButton=start;
            U.Ref(hub,"homeTabBar",tab); U.Ref(hub,"roadmapView",oldMap); U.Ref(hub,"homeButtonGroup",buttons.gameObject);
            U.Ref(hub,"startGameButton",start); U.Ref(hub,"openSettingsButton",settings); U.Ref(hub,"openLevelSelectButton",map.chapterText.transform.parent.GetComponent<Button>()); U.Ref(hub,"openBackpackButton",bag);
            U.Ref(hub,"goldHudText",gold); U.Ref(hub,"diamondHudText",gem);
            var ts=new SerializedObject(tab); var entries=ts.FindProperty("tabs");
            for(int i=0;i<entries.arraySize;i++) if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("tabId").stringValue=="battle")
            { var entry=entries.GetArrayElementAtIndex(i); entry.FindPropertyRelative("mode").enumValueIndex=0; entry.FindPropertyRelative("sceneView").objectReferenceValue=oldMap.gameObject; }
            ts.ApplyModifiedPropertiesWithoutUndo();
            foreach(var component in root.GetComponentsInChildren<Component>(true)) { if(component==null)continue; EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if(opened) EditorSceneManager.CloseScene(scene,true); if(original.IsValid()&&original.isLoaded) SceneManager.SetActiveScene(original); }
    }

    private static TMP_Text StyleCurrency(Transform root,float x,string icon)
    {
        U.Rect((RectTransform)root,x,113.28f,244.08f,82.56f);
        var bg=root.GetComponent<Image>()??root.gameObject.AddComponent<Image>(); bg.enabled=true; bg.sprite=U.Sprite("surface_pill_cream_9slice");bg.type=Image.Type.Sliced;bg.color=Color.white;bg.raycastTarget=false;
        U.Disable(root,"Image");
        var old=root.GetComponentInChildren<TextMeshProUGUI>(true); U.Rect(old.rectTransform,80,0,154,82.56f); U.Style(old,44.28f,U.Ink,true);
        U.Image(root,"CurrencyIcon",U.Sprite(icon),22,13,56,56);
        return old;
    }

    private static void StyleUtility(Button button,string icon,string label,float x,float y,float w,float h)
    {
        var rt=(RectTransform)button.transform; U.Rect(rt,x,y,w,h);
        foreach(var graphic in button.GetComponentsInChildren<Graphic>(true)) if(graphic.gameObject!=button.gameObject) graphic.gameObject.SetActive(false);
        var bg=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();bg.enabled=true;bg.sprite=U.Sprite("surface_square_cream_9slice");bg.type=Image.Type.Sliced;bg.color=Color.white;bg.raycastTarget=true;
        button.targetGraphic=bg;button.transition=Selectable.Transition.None;
        bool start=label=="开始游戏";
        var picture=U.Image(rt,"Icon",U.Sprite(icon),start?42:24,start?(h-64)*.5f:label==null?24:14,start?64:68,start?64:68); picture.gameObject.SetActive(true);
        if(label!=null) { var text=U.Text(rt,"Label",label,start?132:0,start?0:80,start?430:w,start?h:48,start?54:38,U.Ink,true);text.gameObject.SetActive(true); }
    }
}
#endif

