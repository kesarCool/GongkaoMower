#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class CharacterProgressionUIBuilder
{
    private const string Art = "Assets/Res/image/Home/character/";
    private const string PrefabPath = "Assets/Prefab/CharacterSelect/CharacterSelectionPanel.prefab";
    private static Sprite S(string name) => U.Sprite(name, Art);
    [MenuItem("工具/Home UI/Apply Character")]
    private static void ApplyMenu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("character");
        var cell = BuildCard(); var row = BuildStatRow();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var panel = root.GetComponent<CharacterSelectionPanel>();
            var old = new SerializedObject(panel);
            var catalog = old.FindProperty("characterCatalog").objectReferenceValue;
            var skillCatalog = old.FindProperty("skillCatalog").objectReferenceValue;
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var rt = (RectTransform)root.transform; U.Fill(rt); rt.sizeDelta = Vector2.zero;
            var background = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            background.sprite = S("character_page_bg"); background.color = Color.white; background.raycastTarget = true;
            panel.backgroundBlocker = background;
            var presentation = root.GetComponent<CharacterPanelPresentation>() ?? root.AddComponent<CharacterPanelPresentation>();
            U.Ref(panel,"presentation",presentation); U.Ref(panel,"characterCatalog",catalog); U.Ref(panel,"skillCatalog",skillCatalog);
            U.Ref(panel,"cellPrefab",cell); U.Ref(panel,"attrRowPrefab",row);
            U.Ref(panel,"closeButton",null); U.Ref(panel,"tipsText",null);
            var viewport = U.Node(rt,"BodyViewport",0,0,1080,1377.6f); U.Fill(viewport); viewport.offsetMin = new Vector2(0,148.8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var body = U.Node(viewport,"Body",0,0,1080,1377.6f);
            var bodyScroll = viewport.gameObject.AddComponent<ScrollRect>();
            bodyScroll.viewport = viewport; bodyScroll.content = body; bodyScroll.horizontal = false; bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            bodyScroll.scrollSensitivity = 70;
            presentation.bodyViewport=viewport; presentation.body=body;
            var hero = U.Node(body,"HeroHeader",0,0,1080,367.2f); presentation.heroGroup=hero;
            U.Image(hero,"Glow",S("hero_glow"),11,8.2f,500,406);
            U.Image(hero,"Platform",S("hero_platform"),89,322.2f,344,58);
            var portrait = U.Image(hero,"Portrait",null,91,10.2f,340,350); portrait.preserveAspect=true;
            U.Ref(panel,"detailPortrait",portrait); U.Ref(panel,"portraitAnim",portrait.gameObject.AddComponent<IdleBreathAnim>());
            U.Ref(panel,"detailPanel",hero.gameObject);
            U.Ref(panel,"detailNameText",U.Text(hero,"Name","",495,48,530,100,68,U.Ink,true,TextAlignmentOptions.Left));
            U.Ref(panel,"detailLevelText",U.Text(hero,"Level","",495,150,530,64,45,U.Ink,true,TextAlignmentOptions.Left));
            presentation.weaponText=U.Text(hero,"Weapon","",495,214,530,60,35,U.Muted,false,TextAlignmentOptions.Left);
            presentation.equippedMark=U.Image(hero,"EquippedMark",S("check_badge_green"),494,297.2f,32,32);
            presentation.equippedText=U.Text(hero,"EquippedLabel","",539,283.2f,360,60,32,U.Muted,false,TextAlignmentOptions.Left);
            var shadow=U.Image(body,"PaperShadow",S("character_paper_shadow_9slice"),54,475.2f,972,618); presentation.paperShadow=shadow.rectTransform;
            var paper=U.Image(body,"Paper",S("character_paper_9slice"),54,469.2f,972,618); presentation.paper=paper.rectTransform;
            var stats=U.Node(paper.transform,"StatsView",0,0,972,618);U.Fill(stats);
            var skills=U.Node(paper.transform,"SkillView",0,0,972,618);U.Fill(skills);
            U.Ref(panel,"statsView",stats.gameObject); U.Ref(panel,"skillView",skills.gameObject);
            U.Text(stats,"Title","成长预览",42,30,430,82,43,U.Ink,true,TextAlignmentOptions.Left);
            U.Ref(panel,"upgradeCostText",U.Text(stats,"UpgradeCost","",458,42.5f,265,90,29,U.Muted,false,TextAlignmentOptions.Right));
            U.Ref(panel,"upgradeButton",GrowthButton(stats,"UpgradeButton","升级"));
            U.Ref(panel,"upgradeRedPoint",null);
            U.Text(stats,"AttributeHeading","属性",42,151,400,45,32,U.Muted,false,TextAlignmentOptions.Left);
            U.Text(stats,"CurrentHeading","当前",475,151,200,45,32,U.Muted);
            U.Text(stats,"NextHeading","升级后",762,151,200,45,32,U.Muted);
            U.Image(stats,"HeaderDivider",S("stat_header_divider"),42,191,892,2);
            RectTransform attrContent;
            var attrViewport=MakeScroll(stats,"Attributes",42,201,892,396,out attrContent);
            var attrs=attrContent.gameObject.AddComponent<VerticalLayoutGroup>();
            attrs.childControlWidth=true;attrs.childControlHeight=true;attrs.childForceExpandWidth=true;attrs.childForceExpandHeight=false;
            AddVerticalFitter(attrContent);
            U.Ref(panel,"attrRowsContainer",attrContent); presentation.statsViewport=attrViewport;presentation.statsContent=attrContent;presentation.statLayout=attrs;
            U.Image(skills,"SkillIconBack",S("skill_icon_back_9slice"),44,54,66,66);
            U.Ref(panel,"detailSkillIcon",U.Image(skills,"SkillIcon",null,47,57,60,60));
            presentation.skillTitle=U.Text(skills,"SkillTitle","",132,30,400,82,42,U.Ink,true,TextAlignmentOptions.Left);
            U.Ref(panel,"promoteFragmentCostText",U.Text(skills,"PromoteCost","",500,42.5f,223,90,29,U.Muted,false,TextAlignmentOptions.Right));
            U.Ref(panel,"promoteButton",GrowthButton(skills,"PromoteButton","升阶")); U.Ref(panel,"promoteRedPoint",null);
            RectTransform skillContent;
            var skillViewport=MakeScroll(skills,"Descriptions",42,144,892,453,out skillContent);
            var skillLayout=skillContent.gameObject.AddComponent<VerticalLayoutGroup>();
            skillLayout.childControlHeight=true;skillLayout.childControlWidth=true;skillLayout.childForceExpandHeight=false;skillLayout.childForceExpandWidth=true;skillLayout.spacing=24;
            skillLayout.padding=new RectOffset(0,0,0,20);AddVerticalFitter(skillContent);
            var desc=U.Text(skillContent,"Description","",0,0,892,120,34,U.Muted,false,TextAlignmentOptions.TopLeft);desc.enableWordWrapping=true;
            U.Ref(panel,"detailSkillText",desc);
            presentation.skillViewport=skillViewport;presentation.skillContent=skillContent;presentation.skillLayout=skillLayout;
            presentation.tierLabels=new TMP_Text[2];presentation.tierStates=new TMP_Text[2];presentation.tierMarks=new Image[2];
            presentation.tierLocked=S("skill_progress_locked");presentation.tierUnlocked=S("skill_progress_unlocked");
            for(int i=0;i<2;i++)
            {
                var tier=U.Node(skillContent,"Tier"+(i+1),0,0,892,140);
                var tierLayout=tier.gameObject.AddComponent<VerticalLayoutGroup>();tierLayout.childControlWidth=true;tierLayout.childControlHeight=true;tierLayout.childForceExpandHeight=false;tierLayout.spacing=14;
                var heading=U.Node(tier,"Heading",0,0,892,44);heading.gameObject.AddComponent<LayoutElement>().preferredHeight=44;
                presentation.tierMarks[i]=U.Image(heading,"StateMark",presentation.tierLocked,0,4,40,40);
                U.Image(presentation.tierMarks[i].transform,"Lock",U.Sprite("icon_lock"),9,8,22,22);
                U.Image(presentation.tierMarks[i].transform,"Check",U.Sprite("icon_check"),9,8,22,22).gameObject.SetActive(false);
                presentation.tierLabels[i]=U.Text(heading,"Title","",60,0,530,48,36,U.Ink,true,TextAlignmentOptions.Left);
                presentation.tierStates[i]=U.Text(heading,"State","",590,0,302,48,28,U.Muted,false,TextAlignmentOptions.Right);
                var text=U.Text(tier,"Body","",0,0,892,100,33,U.Muted,false,TextAlignmentOptions.TopLeft);text.enableWordWrapping=true;text.margin=new Vector4(60,0,0,0);
                U.Ref(panel,i==0?"promoteRareDescText":"promoteLegendDescText",text);
            }
            var tabRoot=U.Node(body,"Tabs",0,367.2f,1080,113);presentation.tabsRoot=tabRoot;
            var group=tabRoot.gameObject.AddComponent<ToggleGroup>();group.allowSwitchOff=false;
            var statTab=MakeTab(tabRoot,"StatsTab","基础属性",54,true,group);
            var skillTab=MakeTab(tabRoot,"SkillTab","专属技能",552,false,group);
            U.Ref(panel,"statsTabToggle",statTab);U.Ref(panel,"skillTabToggle",skillTab);
            U.Ref(panel,"statsToggleBadge",MakeDot(statTab.transform,"Notification",394,20));
            U.Ref(panel,"skillToggleBadge",MakeDot(skillTab.transform,"Notification",394,20));
            skills.gameObject.SetActive(false);
            var cards=U.Node(body,"CharacterStrip",54,1100,972,257.2f);presentation.cards=cards;
            U.Text(cards,"Title","角色选择",0,0,480,52,36,U.Ink,true,TextAlignmentOptions.Left);
            presentation.unlockedCount=U.Text(cards,"UnlockedCount","",562,0,410,52,32,U.Muted,false,TextAlignmentOptions.Right);
            RectTransform cardContent;var cardViewport=MakeScroll(cards,"Cards",0,49.2f,972,214,out cardContent,true);
            var cardLayout=cardContent.gameObject.AddComponent<HorizontalLayoutGroup>();cardLayout.spacing=12;cardLayout.padding=new RectOffset(2,2,2,2);
            cardLayout.childControlWidth=true;cardLayout.childControlHeight=true;cardLayout.childForceExpandWidth=false;cardLayout.childForceExpandHeight=false;
            var cardFitter=cardContent.gameObject.AddComponent<ContentSizeFitter>();cardFitter.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
            U.Ref(panel,"listContent",cardContent);
            var start=MainButton(rt,"StartButton","开始游戏");var confirm=MainButton(rt,"EquipButton","换将上阵");
            U.Ref(panel,"startGameButton",start);U.Ref(panel,"confirmButton",confirm);confirm.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "Character progression page updated with independent card and attribute prefabs.";
    }
    private static CharacterSelectionElement BuildCard()
    {
        var root=new GameObject("CharacterCard",typeof(RectTransform));
        try
        {
            U.Rect((RectTransform)root.transform,0,0,184,208);
            var face=root.AddComponent<Image>();face.sprite=S("hero_card_normal_9slice");face.type=Image.Type.Sliced;
            var button=U.Button(face);button.transition=Selectable.Transition.SpriteSwap;
            var cell=root.AddComponent<CharacterSelectionElement>();
            var layout=root.AddComponent<LayoutElement>();layout.preferredWidth=184;layout.preferredHeight=208;
            U.Image(root.transform,"Shadow",S("portrait_shadow"),28,108,128,24);
            var portrait=U.Image(root.transform,"Portrait",null,33,4,118,126);portrait.preserveAspect=true;
            U.Ref(cell,"avatarImage",portrait);U.Ref(cell,"nameText",U.Text(root.transform,"Name","",4,132,176,41,34,U.Ink,true));
            U.Ref(cell,"levelText",U.Text(root.transform,"Level","",4,172,176,34,30,U.Muted));
            U.Ref(cell,"conditionText",U.Text(root.transform,"Condition","",2,171,180,36,28,U.Muted));
            U.Ref(cell,"lockedOverlay",U.Image(root.transform,"Lock",U.Sprite("icon_lock"),77,87,30,30).gameObject);
            U.Ref(cell,"selectedCheckmark",U.Image(root.transform,"EquippedMark",S("check_badge_green"),146,-3,32,32).gameObject);
            U.Ref(cell,"clickButton",button);U.Ref(cell,"highlightBorder",face);
            U.Ref(cell,"normalCard",S("hero_card_normal_9slice"));U.Ref(cell,"selectedCard",S("hero_card_selected_9slice"));U.Ref(cell,"lockedCard",S("hero_card_locked_9slice"));
            U.Ref(cell,"pressedCard",S("hero_card_pressed_9slice"));U.Ref(cell,"selectedPressedCard",S("hero_card_selected_pressed_9slice"));
            U.Ref(cell,"badgeRoot",MakeDot(root.transform,"Notification",174,4));
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/CharacterSelect/CharacterCard.prefab").GetComponent<CharacterSelectionElement>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static GameObject BuildStatRow()
    {
        var root=new GameObject("CharacterStatRow",typeof(RectTransform));
        try
        {
            U.Rect((RectTransform)root.transform,0,0,892,44);var bg=root.AddComponent<Image>();bg.sprite=S("stat_row_alternate_9slice");bg.type=Image.Type.Sliced;bg.raycastTarget=false;
            root.AddComponent<LayoutElement>().preferredHeight=44;
            var row=root.AddComponent<UpgradeAttrRow>();
            U.Ref(row,"nameText",U.Text(root.transform,"Attribute","",7,0,440,44,33,U.Ink,false,TextAlignmentOptions.Left));
            U.Ref(row,"curText",U.Text(root.transform,"Current","",433,0,200,44,36,U.Ink));
            U.Ref(row,"nextText",U.Text(root.transform,"Next","",720,0,200,44,36,U.ColorOf("3D7047"),true));
            U.Ref(row,"arrow",U.Image(root.transform,"CompareArrow",S("stat_compare_arrow"),654,12,36,21).gameObject);
            var so=new SerializedObject(row);so.FindProperty("homeTheme").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/CharacterSelect/CharacterStatRow.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static Button GrowthButton(Transform parent,string name,string label)
    {
        var face=U.Image(parent,name,S("growth_normal_9slice"),737.5f,42.5f,208,90);
        var button=U.Button(face);button.transition=Selectable.Transition.SpriteSwap;
        var state=button.spriteState;state.pressedSprite=S("growth_pressed_9slice");state.disabledSprite=S("growth_disabled_9slice");button.spriteState=state;
        U.Text(face.transform,"Label",label,0,0,208,90,42,U.ColorOf("48603C"),true);return button;
    }
    private static Button MainButton(Transform parent,string name,string label)
    {
        var face=U.Image(parent,name,S("character_start_normal_9slice"),0,0,610.2f,126.36f);var rt=face.rectTransform;
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,0);rt.anchoredPosition=new Vector2(0,21.6f);
        var button=U.Button(face);button.transition=Selectable.Transition.SpriteSwap;
        var state=button.spriteState;state.pressedSprite=S("character_start_pressed_9slice");state.disabledSprite=S("character_start_disabled_9slice");button.spriteState=state;
        U.Image(rt,"Icon",U.Sprite("icon_swords"),46,31,64,64);U.Text(rt,"Label",label,142,0,416,126.36f,54,U.Ink,true);
        if(name=="EquipButton") StyleEquipButton(button);
        return button;
    }
    // Also used to patch the existing prefab without rebuilding the character page.
    public static void StyleEquipButton(Button button)
    {
        var face=button.GetComponent<Image>();
        face.sprite=S("character_equip_normal_9slice");face.color=Color.white;face.type=Image.Type.Sliced;
        button.targetGraphic=face;button.transition=Selectable.Transition.SpriteSwap;
        var state=button.spriteState;
        state.highlightedSprite=null;state.selectedSprite=null;
        state.pressedSprite=S("character_equip_pressed_9slice");state.disabledSprite=S("character_equip_disabled_9slice");
        button.spriteState=state;
        var icon=button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite=S("icon_character_swap");icon.color=Color.white;icon.preserveAspect=true;icon.raycastTarget=false;
        button.transform.Find("Label").GetComponent<TMP_Text>().color=U.ColorOf("315B51");
    }
    private static Toggle MakeTab(Transform parent,string name,string label,float x,bool selected,ToggleGroup group)
    {
        var hit=U.Image(parent,name,null,x,0,474,113);hit.color=Color.clear;hit.raycastTarget=true;
        var toggle=hit.gameObject.AddComponent<Toggle>();toggle.transition=Selectable.Transition.None;toggle.targetGraphic=hit;toggle.group=group;toggle.isOn=selected;
        var presentation=hit.gameObject.AddComponent<CharacterTabPresentation>();
        presentation.selectedBackground=U.Image(hit.transform,"SelectedBackground",S("tab_selected_folder_9slice"),-2,10,478,94).gameObject;
        presentation.checkmark=U.Image(hit.transform,"SelectedMark",S("check_badge_brown"),95,34,38,38).gameObject;
        presentation.label=U.Text(hit.transform,"Label",label,140,4,270,99,selected?44:40,selected?U.Ink:U.Muted,selected);
        presentation.connector=U.Image(hit.transform,"Connector",S("tab_connector_9slice"),x<100?472:-500,98,502,8).gameObject;
        presentation.selectedBackground.SetActive(selected);presentation.checkmark.SetActive(selected);presentation.connector.SetActive(selected);
        return toggle;
    }
    private static GameObject MakeDot(Transform parent,string name,float x,float y)
    { return U.Image(parent,name,U.Sprite("notify_dot"),x,y,20,20).gameObject; }
    private static RectTransform MakeScroll(Transform parent,string name,float x,float y,float w,float h,out RectTransform content,bool horizontal=false)
    {
        var viewport=U.Node(parent,name,x,y,w,h);viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        content=U.Node(viewport,"Content",0,0,w,h);
        if(horizontal){content.anchorMin=Vector2.zero;content.anchorMax=new Vector2(0,1);content.sizeDelta=new Vector2(w,0);}
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=horizontal;scroll.vertical=!horizontal;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        return viewport;
    }
    private static void AddVerticalFitter(RectTransform rt)
    { rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize; }
}
#endif

