#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U=HomeUIBuilderUtility;

public static class HomePagesUIBuilder
{
    [MenuItem("工具/Home UI/Apply Exchange Lexicon Achievements")]
    private static void Menu(){Debug.Log(Apply());}
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("home-pages");
        BuildExchange();BuildLexicon();BuildAchievements();
        return "Exchange, lexicon and achievement prefabs updated; existing GUIDs and data services retained.";
    }
    private static GameObject Load(string path)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        foreach(var presentation in root.GetComponents<HomeExchangePresentation>())UnityEngine.Object.DestroyImmediate(presentation);
        foreach(var presentation in root.GetComponents<HomeLexiconPresentation>())UnityEngine.Object.DestroyImmediate(presentation);
        foreach(Transform child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        U.Fill((RectTransform)root.transform);
        var bg=root.GetComponent<Image>()??root.AddComponent<Image>();bg.enabled=true;bg.sprite=null;bg.color=U.ColorOf("F5EED8");bg.raycastTarget=true;
        var panel=root.GetComponent<UIPanelBase>();if(panel!=null)panel.backgroundBlocker=bg;
        return root;
    }
    private static void Header(Transform root,string title,string subtitle,string icon)
    {
        U.Image(root,"PageIcon",U.Sprite(icon),54,36.2f,84,84);
        U.Text(root,"PageTitle",title,158,33.2f,700,91,64,U.Ink,true,TextAlignmentOptions.Left);
        U.Text(root,"PageSubtitle",subtitle,58,119.2f,950,54,32,U.Muted,false,TextAlignmentOptions.Left);
    }
    private static RectTransform Viewport(Transform root,string name,float top,float bottom,float margin)
    {
        var rt=U.Node(root,name,0,0,1080-margin*2,900);U.Fill(rt);rt.offsetMin=new Vector2(margin,bottom);rt.offsetMax=new Vector2(-margin,-top);
        rt.gameObject.AddComponent<RectMask2D>();var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;return rt;
    }
    private static RectTransform Content(RectTransform viewport,string name="Content")
    {
        var rt=U.Node(viewport,name,0,0,972,900);rt.anchorMax=new Vector2(1,1);rt.sizeDelta=new Vector2(0,900);return rt;
    }
    private static void VerticalLayout(RectTransform content,float spacing)
    {
        var group=content.gameObject.AddComponent<VerticalLayoutGroup>();group.spacing=spacing;
        group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=true;group.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
    }
    private static LoopVerticalScrollRect Loop(RectTransform viewport,RectTransform content)
    {
        var loop=viewport.gameObject.AddComponent<LoopVerticalScrollRect>();loop.viewport=viewport;loop.content=content;
        loop.horizontal=false;loop.vertical=true;loop.movementType=LoopScrollRectBase.MovementType.Clamped;loop.scrollSensitivity=45;loop.reverseDirection=false;return loop;
    }
    private static Button ActionButton(Transform parent,string name,float x,float y,float w,float h,string label,bool secondary=false)
    {
        var face=U.Image(parent,name,U.Sprite(secondary?"growth_normal_9slice":"character_start_normal_9slice","Assets/Res/image/Home/character/"),x,y,w,h);
        var button=U.Button(face);button.transition=Selectable.Transition.SpriteSwap;
        var state=button.spriteState;state.pressedSprite=U.Sprite(secondary?"growth_pressed_9slice":"character_start_pressed_9slice","Assets/Res/image/Home/character/");state.disabledSprite=U.Sprite("growth_disabled_9slice","Assets/Res/image/Home/character/");button.spriteState=state;
        U.Text(face.transform,"Label",label,0,0,w,h,38,U.Ink,true);return button;
    }
    private static ShopCell BuildShopCell(bool bonus)
    {
        string name=bonus?"ExchangeBonusCard":"ExchangeFragmentCard";
        var root=new GameObject(name,typeof(RectTransform));
        try
        {
            float w=bonus?474:308,h=bonus?356:378;U.Rect((RectTransform)root.transform,0,0,w,h);
            var face=root.AddComponent<Image>();face.sprite=U.Sprite("home_card_cream_9slice");face.type=Image.Type.Sliced;face.raycastTarget=true;
            var cell=root.AddComponent<ShopCell>();var p=root.AddComponent<ShopCellPresentation>();p.bonus=bonus;p.face=face;
            p.characters=AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            p.normalCard=U.Sprite("home_card_cream_9slice");p.mutedCard=U.Sprite("home_card_muted_9slice");
            p.goldBonusCard=U.Sprite("bonus_gold_card_9slice","Assets/Res/image/Home/exchange/");p.gemBonusCard=U.Sprite("bonus_gem_card_9slice","Assets/Res/image/Home/exchange/");
            p.coinReward=U.Sprite("reward_coins","Assets/Res/image/Home/exchange/");p.gemReward=U.Sprite("reward_gems","Assets/Res/image/Home/exchange/");p.goldIcon=U.Sprite("icon_coins");p.gemIcon=U.Sprite("icon_gem");
            p.primaryButton=U.Sprite("character_start_normal_9slice","Assets/Res/image/Home/character/");p.secondaryButton=U.Sprite("growth_normal_9slice","Assets/Res/image/Home/character/");p.pressedButton=U.Sprite("character_start_pressed_9slice","Assets/Res/image/Home/character/");p.secondaryPressedButton=U.Sprite("growth_pressed_9slice","Assets/Res/image/Home/character/");p.disabledButton=U.Sprite("growth_disabled_9slice","Assets/Res/image/Home/character/");
            p.nameLabel=U.Text(root.transform,"Name","",bonus?214:12,bonus?54:17,bonus?230:w-24,54,bonus?36:37,U.Ink,true,bonus?TextAlignmentOptions.Left:TextAlignmentOptions.Center);
            p.portrait=U.Image(root.transform,"Portrait",null,bonus?29:66,bonus?52:80,bonus?160:176,148);p.portrait.preserveAspect=true;
            if(!bonus)U.Image(root.transform,"FragmentMark",U.Sprite("icon_fragment_piece"),65,183,44,44);
            p.amountLabel=U.Text(root.transform,"Amount","",214,108,230,80,59,U.Ink,true,TextAlignmentOptions.Left);p.amountLabel.gameObject.SetActive(bonus);
            p.limitLabel=U.Text(root.transform,"Limit","",0,bonus?190:232,w,40,29,U.Muted);
            var cta=U.Image(root.transform,"ActionFace",p.primaryButton,bonus?30:21,bonus?250:282,bonus?414:266,bonus?84:78);p.ctaFace=cta;
            p.priceLabel=U.Text(cta.transform,"Price","",bonus?50:40,0,bonus?350:226,bonus?84:78,38,U.Ink,true);
            p.currencyIcon=U.Image(cta.transform,"Currency",p.gemIcon,45,17,44,44);
            p.adIcon=U.Image(cta.transform,"Ad",U.Sprite("icon_ad_video"),55,23,38,38);
            var button=root.AddComponent<Button>();button.targetGraphic=cta;button.transition=Selectable.Transition.SpriteSwap;p.button=button;
            U.Ref(cell,"icon",p.portrait);U.Ref(cell,"textName",p.nameLabel);U.Ref(cell,"textPrice",p.priceLabel);U.Ref(cell,"textLimit",p.limitLabel);U.Ref(cell,"btnBuy",button);
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Shop/"+name+".prefab").GetComponent<ShopCell>();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void BuildExchange()
    {
        var bonus=BuildShopCell(true);var fragment=BuildShopCell(false);
        const string path="Assets/Prefab/Shop/ShowView.prefab";var root=Load(path);
        try
        {
            var view=root.GetComponent<ShopView>();var p=root.AddComponent<HomeExchangePresentation>();p.bonusCellPrefab=bonus;
            Header(root.transform,"兑换","领取补给，收集角色碎片","nav_exchange");
            var badge=U.Image(root.transform,"Countdown",U.Sprite("home_badge_sand_9slice"),616,58.2f,410,69);
            U.Image(badge.transform,"Clock",U.Sprite("icon_refresh_clock"),21,18,33,33);
            var countdown=U.Text(badge.transform,"Label","",72,0,312,69,31,U.Muted,false,TextAlignmentOptions.Right);
            var viewport=Viewport(root.transform,"ShopScroll",195.2f,18,54);var content=Content(viewport);p.content=content;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=50;
            p.bonusContent=U.Node(content,"BonusGrid",0,0,972,356);var bg=p.bonusContent.gameObject.AddComponent<GridLayoutGroup>();bg.cellSize=new Vector2(474,356);bg.spacing=new Vector2(24,24);bg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;bg.constraintCount=2;
            p.fragmentHeading=U.Node(content,"FragmentHeading",0,386,972,58);
            U.Text(p.fragmentHeading,"Title","角色碎片",4,0,550,58,45,U.Ink,true,TextAlignmentOptions.Left);
            U.Text(p.fragmentHeading,"Hint","用于解锁与升阶",550,0,418,58,29,U.Muted,false,TextAlignmentOptions.Right);
            p.fragmentContent=U.Node(content,"FragmentGrid",0,468,972,780);var fg=p.fragmentContent.gameObject.AddComponent<GridLayoutGroup>();fg.cellSize=new Vector2(308,378);fg.spacing=new Vector2(24,24);fg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;fg.constraintCount=3;
            p.footerHint=U.Node(content,"Hint",0,1272,972,36);U.Text(p.footerHint,"Label","商品随倒计时刷新，限购次数同时重置",0,0,972,36,28,U.Muted);
            var empty=U.Text(content,"Empty","暂时没有可兑换的商品",0,60,972,100,36,U.Muted);empty.gameObject.SetActive(false);
            U.Ref(view,"presentation",p);U.Ref(view,"listContent",content);U.Ref(view,"cellPrefab",fragment);U.Ref(view,"groupCountdown",badge.gameObject);U.Ref(view,"textRefreshCountdown",countdown);U.Ref(view,"emptyHint",empty.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static Toggle Filter(Transform parent,string name,bool folder)
    {
        float w=folder?474:168,h=folder?94:84;
        var hit=U.Image(parent,name,null,0,0,w,h);hit.color=Color.clear;hit.raycastTarget=true;
        var toggle=hit.gameObject.AddComponent<Toggle>();toggle.targetGraphic=hit;toggle.transition=Selectable.Transition.None;toggle.isOn=false;
        var p=hit.gameObject.AddComponent<HomeFilterTogglePresentation>();p.folder=folder;
        p.normalSprite=folder?null:U.Sprite("filter_normal_9slice","Assets/Res/image/Home/lexicon/");p.selectedSprite=folder?U.Sprite("tab_selected_folder_9slice","Assets/Res/image/Home/character/"):U.Sprite("filter_selected_9slice","Assets/Res/image/Home/lexicon/");
        p.face=U.Image(hit.transform,"Face",p.selectedSprite,folder?-2:0,0,folder?478:w,h);
        p.label=U.Text(hit.transform,"Label","",folder?140:0,0,folder?270:w,h,folder?44:31,U.Ink,true);
        if(folder)
        {
            p.selectedIcon=U.Image(hit.transform,"SelectedIcon",U.Sprite("nav_book"),99,26,42,42).gameObject;
            p.connector=U.Image(hit.transform,"Connector",U.Sprite("tab_connector_9slice","Assets/Res/image/Home/character/"),472,88,502,8).gameObject;
        }
        hit.gameObject.AddComponent<LayoutElement>().preferredWidth=w;return toggle;
    }
    private static GameObject BuildPairRow()
    {
        var root=new GameObject("LexiconPairRow",typeof(RectTransform));
        try
        {
            U.Rect((RectTransform)root.transform,0,0,892,111);root.AddComponent<LayoutElement>().preferredHeight=111;
            var cell=root.AddComponent<LexiconPreviewEntryRowCell>();
            for(int i=0;i<2;i++)
            {
                var item=U.Node(root.transform,i==0?"LeftItem":"RightItem",i*470,0,422,111);item.anchorMin=new Vector2(0,0);item.anchorMax=new Vector2(0,1);item.sizeDelta=new Vector2(422,0);
                var badge=U.Image(item,"NumberBadge",U.Sprite(i==0?"home_number_green_9slice":"home_number_gold_9slice"),0,19,61,61);badge.rectTransform.anchorMin=badge.rectTransform.anchorMax=new Vector2(0,.5f);badge.rectTransform.pivot=new Vector2(0,.5f);badge.rectTransform.anchoredPosition=Vector2.zero;
                var number=U.Text(badge.transform,"Number","",0,0,61,61,29,U.Muted);
                var label=U.Text(item,"Word","",81,0,341,111,39,U.Ink,false,TextAlignmentOptions.MidlineLeft);label.enableWordWrapping=true;label.rectTransform.anchorMin=new Vector2(0,0);label.rectTransform.anchorMax=new Vector2(0,1);label.rectTransform.sizeDelta=new Vector2(341,0);
                var divider=U.Image(item,"Divider",null,0,0,422,2);divider.color=U.ColorOf("E9E2D0");divider.rectTransform.anchorMin=divider.rectTransform.anchorMax=divider.rectTransform.pivot=new Vector2(0,0);divider.rectTransform.anchoredPosition=new Vector2(0,2);
                U.Ref(cell,i==0?"bodyText":"rightText",label);U.Ref(cell,i==0?"leftIndex":"rightIndex",number);if(i==1)U.Ref(cell,"rightItem",item.gameObject);
            }
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/LexiconPreview/LexiconPairRow.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void BuildLexicon()
    {
        var row=BuildPairRow();const string path="Assets/Prefab/LexiconPreview/LexiconPreviewPanel.prefab";var root=Load(path);
        try
        {
            var panel=root.GetComponent<LexiconPreviewPanel>();var p=root.AddComponent<HomeLexiconPresentation>();
            Header(root.transform,"词汇","看看本篇会遇到哪些文字","nav_book");
            var paper=U.Image(root.transform,"Paper",U.Sprite("character_paper_9slice","Assets/Res/image/Home/character/"),54,285.2f,972,1196);paper.rectTransform.anchorMin=new Vector2(0,0);paper.rectTransform.anchorMax=new Vector2(0,1);paper.rectTransform.offsetMin=new Vector2(54,45.2f);paper.rectTransform.offsetMax=new Vector2(1026,-285.2f);
            // Use fixed-width paper and stretch only its height.
            paper.rectTransform.sizeDelta=new Vector2(972,paper.rectTransform.sizeDelta.y);
            var tabs=U.Node(root.transform,"ThemeTabs",54,193.2f,972,94);var group=tabs.gameObject.AddComponent<HorizontalLayoutGroup>();group.spacing=24;group.childControlWidth=false;group.childControlHeight=false;group.childForceExpandWidth=group.childForceExpandHeight=false;
            tabs.gameObject.AddComponent<ToggleGroup>().allowSwitchOff=false;var theme=Filter(tabs,"ThemeTemplate",true);theme.gameObject.SetActive(false);
            var cats=U.Node(paper.transform,"Categories",34,38,904,180);cats.gameObject.AddComponent<ToggleGroup>().allowSwitchOff=false;
            var grid=cats.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(168,84);grid.spacing=new Vector2(16,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;
            var category=Filter(cats,"CategoryTemplate",false);category.gameObject.SetActive(false);
            U.Image(paper.transform,"CategoryIcon",U.Sprite("icon_locate"),40,250,36,36);
            p.categoryLabel=U.Text(paper.transform,"CategoryName","",92,242,490,54,41,U.Ink,true,TextAlignmentOptions.Left);
            p.countLabel=U.Text(paper.transform,"Count","",550,242,382,54,30,U.Muted,false,TextAlignmentOptions.Right);
            var divider=U.Image(paper.transform,"Divider",null,38,311,896,2);divider.color=U.ColorOf("D7C8A6");
            var viewport=Viewport(paper.transform,"WordsScroll",341,70,40);var content=Content(viewport);VerticalLayout(content,0);
            var loop=Loop(viewport,content);var driver=viewport.gameObject.AddComponent<LexiconPreviewLoopScrollDriver>();U.Ref(driver,"entryRowPrefab",row);var so=new SerializedObject(driver);so.FindProperty("columns").intValue=2;so.ApplyModifiedPropertiesWithoutUndo();
            p.emptyHint=U.Text(viewport,"Empty","这个分类还没有词条",0,0,892,160,36,U.Muted).gameObject;p.emptyHint.SetActive(false);
            var hint=U.Text(paper.transform,"Hint","上下滑动，查看其余词条",0,0,972,36,28,U.Muted);hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=hint.rectTransform.pivot=new Vector2(.5f,0);hint.rectTransform.anchoredPosition=new Vector2(0,16);
            U.Ref(panel,"presentation",p);U.Ref(panel,"closeButton",null);U.Ref(panel,"themeFilterRoot",tabs);U.Ref(panel,"categoryFilterRoot",cats);U.Ref(panel,"themeToggleTemplate",theme);U.Ref(panel,"categoryToggleTemplate",category);U.Ref(panel,"loopScroll",loop);U.Ref(panel,"loopDriver",driver);U.Ref(panel,"listContent",content);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static AchievementCell BuildAchievementCell()
    {
        var root=new GameObject("AchievementThemeCell",typeof(RectTransform));
        try
        {
            U.Rect((RectTransform)root.transform,0,0,972,215);root.AddComponent<LayoutElement>().preferredHeight=215;var cell=root.AddComponent<AchievementCell>();
            var section=U.Text(root.transform,"SectionTitle","",4,0,700,52,36,U.Ink,true,TextAlignmentOptions.Left);U.Ref(cell,"sectionTitle",section);
            var body=U.Image(root.transform,"Card",U.Sprite("home_card_cream_9slice"),0,59,972,156);U.Ref(cell,"cardBody",body.rectTransform);U.Ref(cell,"bgImage",body);
            var desc=U.Text(body.transform,"Description","",40,17,548,54,38,U.Ink,true,TextAlignmentOptions.TopLeft);desc.enableWordWrapping=true;U.Ref(cell,"textDescription",desc);
            var progressText=U.Text(body.transform,"ProgressText","",40,0,548,40,28,U.Muted,false,TextAlignmentOptions.Left);Bottom(progressText.rectTransform,40,44,548,40);U.Ref(cell,"textProgress",progressText);
            var track=U.Image(body.transform,"ProgressTrack",U.Sprite("home_progress_track_9slice"),40,0,548,12);Bottom(track.rectTransform,40,22,548,12);
            var fill=U.Image(track.transform,"Fill",U.Sprite("home_progress_green_9slice"),0,0,548,12);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;U.Ref(cell,"progressFill",fill);
            var reward=U.Image(body.transform,"Reward",null,660,0,100,156);reward.color=Color.clear;reward.rectTransform.anchorMin=new Vector2(0,0);reward.rectTransform.anchorMax=new Vector2(0,1);reward.rectTransform.sizeDelta=new Vector2(100,0);
            var item=reward.gameObject.AddComponent<ItemCell>();var rewardButton=U.Button(reward);U.Ref(item,"clickButton",rewardButton);
            U.Ref(item,"iconImage",U.Image(reward.transform,"Icon",U.Sprite("icon_gem"),0,28,56,56));
            var count=U.Text(reward.transform,"Count","",0,90,100,40,30,U.Muted);Bottom(count.rectTransform,0,26,100,40);U.Ref(cell,"rewardCountText",count);U.Ref(cell,"rewardItemCell",item);
            var claim=ActionButton(body.transform,"Claim",766,36,176,84,"领取");Middle(claim.GetComponent<RectTransform>(),766,176,84);U.Ref(cell,"btnClaim",claim);
            var claimed=U.Node(body.transform,"Claimed",766,0,206,156);Middle(claimed,766,206,156);U.Image(claimed,"Check",U.Sprite("check_badge_green","Assets/Res/image/Home/character/"),0,62,32,32);U.Text(claimed,"Label","已领取",38,45,150,70,31,U.Muted);U.Ref(cell,"objClaimed",claimed.gameObject);
            var pending=U.Text(body.transform,"Pending","进行中",757,45,197,70,32,U.Muted);Middle(pending.rectTransform,757,197,70);U.Ref(cell,"objNotAchieved",pending.gameObject);
            foreach(var entry in new[]{new[]{"normalCard","home_card_cream_9slice"},new[]{"claimableCard","home_card_claimable_9slice"},new[]{"claimedCard","home_card_muted_9slice"},new[]{"greenProgress","home_progress_green_9slice"},new[]{"goldProgress","home_progress_gold_9slice"},new[]{"themeGem","icon_gem"},new[]{"themeGold","icon_coins"}})U.Ref(cell,entry[0],U.Sprite(entry[1]));
            var so=new SerializedObject(cell);so.FindProperty("homeTheme").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Achievement/AchievementThemeCell.prefab").GetComponent<AchievementCell>();
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    private static void BuildAchievements()
    {
        var cell=BuildAchievementCell();const string path="Assets/Prefab/Achievement/AchievementPanel.prefab";var root=Load(path);
        try
        {
            var panel=root.GetComponent<AchievementPanel>();Header(root.transform,"成就","完成挑战，领取钻石奖励","nav_trophy");
            var badge=U.Image(root.transform,"ReadyBadge",U.Sprite("home_badge_mint_9slice"),54,195.2f,972,106);U.Image(badge.transform,"Check",U.Sprite("check_badge_green","Assets/Res/image/Home/character/"),33,31,43,43);
            var ready=U.Text(badge.transform,"ReadyCount","",97,0,600,106,39,U.Ink,true,TextAlignmentOptions.Left);U.Ref(panel,"readyCountText",ready);
            U.Text(badge.transform,"Hint","已完成优先展示",670,0,259,106,28,U.Muted,false,TextAlignmentOptions.Right);
            var viewport=Viewport(root.transform,"AchievementScroll",333.2f,28,54);var content=Content(viewport);VerticalLayout(content,18);var loop=Loop(viewport,content);
            var empty=U.Text(viewport,"Empty","暂时没有成就记录",0,0,972,160,36,U.Muted);empty.gameObject.SetActive(false);
            U.Ref(panel,"loopScroll",loop);U.Ref(panel,"cellPrefab",cell);U.Ref(panel,"emptyHint",empty.gameObject);U.Ref(panel,"closeButton",null);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void Bottom(RectTransform rt,float x,float bottom,float w,float h)
    {rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,0);rt.anchoredPosition=new Vector2(x,bottom);rt.sizeDelta=new Vector2(w,h);}
    private static void Middle(RectTransform rt,float x,float w,float h)
    {rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,.5f);rt.anchoredPosition=new Vector2(x,0);rt.sizeDelta=new Vector2(w,h);}
}
#endif
