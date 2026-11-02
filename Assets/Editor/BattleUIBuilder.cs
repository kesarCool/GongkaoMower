#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class BattleUIBuilder
{
    const string Folder = "Assets/Res/image/Game/battle/";
    const string Character = "Assets/Res/image/Home/character/";
    const string Select = "Assets/Res/image/Game/select/";
    static GameObject damageRow, passiveCard, rewardCell;
    [MenuItem("工具/Battle UI/Apply Approved V2")]
    static void Menu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("battle");
        damageRow = BuildDamageRow(); passiveCard = BuildPassiveCard(); rewardCell = BuildRewardCell();
        BuildHud(); BuildPauseButton(); BuildPause(); BuildResult(); BuildRevive();
        Edit("Assets/Prefab/Energy.prefab", root => {
            var renderer = root.GetComponent<SpriteRenderer>();
            float oldWidth = root.GetComponent<CircleCollider2D>().radius * 2;
            renderer.sprite = S("energy_orb"); renderer.color = Color.white;
            // Keep collider and pickup parameters intact, including the visible core's old footprint.
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "energy_orb.png");
            importer.spritePixelsPerUnit = 26f / Mathf.Max(.08f, oldWidth); importer.SaveAndReimport();
        });
        AssetDatabase.SaveAssets();
        return "Battle HUD, pause, victory/defeat, revive and energy visuals updated. Existing six prefab GUIDs retained.";
    }
    static Sprite S(string name) { return U.Sprite(name, Folder); }
    static void Edit(string path, Action<GameObject> action)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void Clear(GameObject root)
    {
        foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach (var old in root.GetComponents<BattleThemeLayout>()) UnityEngine.Object.DestroyImmediate(old);
        foreach (var old in root.GetComponents<BattlePanelPresentation>()) UnityEngine.Object.DestroyImmediate(old);
    }
    static RectTransform Paper(GameObject root, float w, float h)
    {
        Clear(root); U.Fill((RectTransform)root.transform);
        var mask = root.GetComponent<Image>() ?? root.AddComponent<Image>(); mask.enabled = true; mask.sprite = null; mask.color = new Color32(73,54,39,125); mask.raycastTarget = true;
        root.GetComponent<UIPanelBase>().backgroundBlocker = mask;
        var paper = U.Image(root.transform, "Panel", U.Sprite("dialog_panel_cream_9slice"), 0, 0, w, h); paper.raycastTarget = true;
        var fit = root.AddComponent<BattleThemeLayout>(); fit.panel = paper.rectTransform; fit.width = w; fit.height = h;
        fit.Refresh(); return paper.rectTransform;
    }
    static TextMeshProUGUI T(Transform parent, string name, string value, float x,float y,float w,float h,float size=34,bool bold=false, TextAlignmentOptions align=TextAlignmentOptions.Left)
        { return U.Text(parent,name,value,x,y,w,h,size,U.Ink,bold,align); }
    static void Line(Transform p, string name,float x,float y,float w) { U.Image(p,name,null,x,y,w,2).color=U.ColorOf("D7C8A6"); }
    static Button Action(Transform p,string name,float x,float y,float w,float h,string value,bool primary=true)
    {
        var face=U.Image(p,name,U.Sprite(primary?"character_start_normal_9slice":"home_card_cream_9slice",primary?Character:U.Common),x,y,w,h);
        var button=U.Button(face);
        if(primary){button.transition=Selectable.Transition.SpriteSwap;var state=button.spriteState;state.pressedSprite=U.Sprite("character_start_pressed_9slice",Character);state.disabledSprite=U.Sprite("character_start_disabled_9slice",Character);button.spriteState=state;}
        var label=T(face.transform,"Label",value,12,0,w-24,h,value.Length>5?34:40,true,TextAlignmentOptions.Center);
        label.enableAutoSizing=true;label.fontSizeMin=28;label.fontSizeMax=40;return button;
    }
    static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,bool horizontal=false)
    {
        var view=U.Node(parent,name,x,y,w,h);view.gameObject.AddComponent<RectMask2D>();
        var hit=view.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        var content=U.Node(view,"Content",0,0,w,0);
        if(!horizontal) { content.anchorMax=new Vector2(1,1);content.sizeDelta=Vector2.zero; }
        else content.sizeDelta=new Vector2(0,h);
        var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=content;scroll.horizontal=horizontal;scroll.vertical=!horizontal;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        HorizontalOrVerticalLayoutGroup group=horizontal?(HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<HorizontalLayoutGroup>():content.gameObject.AddComponent<VerticalLayoutGroup>();
        group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=!horizontal;group.childForceExpandHeight=false;group.spacing=horizontal?20:9;
        var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.horizontalFit=horizontal?ContentSizeFitter.FitMode.PreferredSize:ContentSizeFitter.FitMode.Unconstrained;fit.verticalFit=horizontal?ContentSizeFitter.FitMode.Unconstrained:ContentSizeFitter.FitMode.PreferredSize;
        return scroll;
    }
    static Slider Bar(Transform parent,string name,float x,float y,float w,float h,bool boss=false)
    {
        var root=U.Node(parent,name,x,y,w,h);var bar=root.gameObject.AddComponent<Slider>();bar.transition=Selectable.Transition.None;bar.interactable=false;
        var bg=U.Image(root,"Track",S("surface_soft_9slice"),0,0,w,h);U.Fill(bg.rectTransform);bg.color=U.ColorOf("DDD7BF");
        var area=U.Node(root,"FillArea",0,0,w,h);U.Fill(area);area.offsetMin=new Vector2(2,2);area.offsetMax=new Vector2(-2,-2);
        var fill=U.Image(area,"Fill",S("surface_soft_9slice"),0,0,w-4,h-4);U.Fill(fill.rectTransform);
        fill.color=U.ColorOf(boss?"C97159":"4F8C38");bar.fillRect=fill.rectTransform;bar.minValue=0;bar.maxValue=1;bar.value=.6f;return bar;
    }
    static void BuildHud()
    {
        Edit("Assets/Prefab/Battle/GameLayer.prefab",root=>{
            Clear(root);U.Fill((RectTransform)root.transform);var bg=root.GetComponent<Image>();bg.enabled=false;bg.raycastTarget=false;
            var fit=root.AddComponent<BattleThemeLayout>();fit.hud=true;
            var top=U.Node(root.transform,"HudTop",0,0,1080,390);fit.top=top;
            U.Image(top,"TopGradient",S("hud_top_gradient"),0,0,1080,390).color=new Color(1,1,1,.82f);
            var hud=root.GetComponent<GameLayer>();
            var hudSettings=new SerializedObject(hud);hudSettings.FindProperty("compactTimer").boolValue=true;hudSettings.ApplyModifiedPropertiesWithoutUndo();
            hud.levelText=T(top,"Level","美食篇 · 关卡1-2",40,116,465,58,36,true);hud.levelText.enableAutoSizing=true;hud.levelText.fontSizeMin=25;hud.levelText.fontSizeMax=36;
            U.Image(top,"TimerFace",U.Sprite("home_card_cream_9slice"),530,106,226,76);U.Image(top,"TimerIcon",S("icon_timer"),548,124,38,38);
            hud.timerText=T(top,"Timer","00:00",586,106,155,76,34,true,TextAlignmentOptions.Center);
            U.Image(top,"WaveFace",U.Sprite("home_card_cream_9slice"),40,212,262,64);hud.waveText=T(top,"Wave","波次：1/7",48,212,246,64,32,false,TextAlignmentOptions.Center);
            U.Image(top,"KillsFace",U.Sprite("home_card_cream_9slice"),326,212,468,64);hud.killText=T(top,"Kills","击杀:0/620",338,212,444,64,32,false,TextAlignmentOptions.Center);
            var energy=U.Node(top,"EnergyGroup",0,0,1080,390);
            U.Image(energy,"LevelFace",S("surface_soft_9slice"),40,303,128,60).color=U.ColorOf("F6D38C");
            hud.energyProgressText=T(energy,"LevelValue","Lv.1",40,303,128,60,30,true,TextAlignmentOptions.Center);
            hud.energyProgressSlider=Bar(energy,"Energy",190,322,736,24);
            var boss=U.Node(top,"BossGroup",0,0,1080,390);
            hud.bossHpNameText=T(boss,"BossName","首领",40,303,195,60,29,true);hud.bossHpNameText.enableAutoSizing=true;hud.bossHpNameText.fontSizeMin=20;hud.bossHpNameText.fontSizeMax=29;
            hud.bossHpSlider=Bar(boss,"BossHp",246,314,680,28,true);hud.bossHpValueText=T(boss,"BossValue","0 / 0",246,344,680,38,25,false,TextAlignmentOptions.Center);
            // Level badge is hidden together with its label when Boss HP replaces energy.
            hud.energyLevelBadge=energy.Find("LevelFace").gameObject;
            var map=U.Node(root.transform,"MiniMap",40,1630,236,236);fit.minimap=map;
            var raw=U.Node(map,"MapView",6,6,224,224).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;
            var dot=U.Image(raw.transform,"PlayerDot",U.Sprite("notify_dot"),0,0,14,14);dot.color=U.ColorOf("DE6E4D");dot.rectTransform.anchorMin=dot.rectTransform.anchorMax=Vector2.zero;dot.rectTransform.pivot=new Vector2(.5f,.5f);
            U.Image(map,"Frame",S("minimap_frame_9slice"),0,0,236,236);
            var mini=root.GetComponent<MiniMapUI>();U.Ref(mini,"mapImage",raw);U.Ref(mini,"playerDot",dot.rectTransform);
            var so=new SerializedObject(mini);so.FindProperty("usePrefabLayout").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();fit.Refresh();
        });
    }
    static void BuildPauseButton()
    {
        Edit("Assets/Prefab/Battle/PauseButton.prefab",root=>{
            foreach(Transform child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            var face=root.GetComponent<Image>();face.sprite=U.Sprite("surface_square_cream_9slice");face.type=Image.Type.Sliced;face.color=Color.white;U.Button(face);
            U.Image(root.transform,"Icon",S("icon_pause"),20,20,52,52);
            var fit=root.GetComponent<BattleThemeLayout>()??root.AddComponent<BattleThemeLayout>();fit.pauseButtonOnly=true;
            var rt=(RectTransform)root.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(1,1);rt.anchoredPosition=new Vector2(-40,-210);rt.sizeDelta=new Vector2(92,92);
        });
    }
    static BattlePanelPresentation Presentation(GameObject root,RectTransform panel,bool result)
    {
        var p=root.AddComponent<BattlePanelPresentation>();
        float x=result?48:42,w=result?856:852,y=result?376:140;
        var stats=U.Image(panel,"Stats",S("surface_soft_9slice"),x,y,w,result?112:92);stats.color=U.ColorOf("EDE9D5");
        var labels=new[]{result?"完成波次":"当前波次","本局时长","击杀数量"};TextMeshProUGUI[] values=new TextMeshProUGUI[3];
        for(int i=0;i<3;i++) {float cw=w/3;T(stats.transform,"Label"+i,labels[i],cw*i,5,cw,34,27,false,TextAlignmentOptions.Center).color=U.Muted;values[i]=T(stats.transform,"Value"+i,"—",cw*i,36,cw,result?66:52,42,true,TextAlignmentOptions.Center);if(i>0)U.Image(stats.transform,"Divider"+i,null,cw*i,20,2,result?72:52).color=U.ColorOf("D7C8A6");}
        p.wave=values[0];p.duration=values[1];p.kills=values[2];
        float activeTitleY=result?509:350,activeY=result?569:426;
        T(panel,"ActiveTitle","输出技能",x,activeTitleY,w,60,38,true);p.activeCount=T(panel,"ActiveCount","0 / 5",x+190,activeTitleY,w-190,60,28);p.activeCount.color=U.Muted;
        T(panel,"DamageTitle","累计伤害",x,activeTitleY,w,60,29,false,TextAlignmentOptions.Right).color=U.Muted;
        p.activeScroll=Scroll(panel,"ActiveSkills",x,activeY,w,result?372:465);p.activeContent=p.activeScroll.content;
        p.emptyActive=T(panel,"EmptyActive","暂无输出技能",x,activeY+30,w,80,30,false,TextAlignmentOptions.Center);p.emptyActive.color=U.Muted;
        float passiveY=result?1009:954,passiveTitle=result?944:895;
        T(panel,"PassiveTitle","被动技能",x,passiveTitle,w,60,36,true);p.passiveCount=T(panel,"PassiveCount","0 / 5",x,passiveTitle,w,60,28,false,TextAlignmentOptions.Right);p.passiveCount.color=U.Muted;
        var content=U.Node(panel,"PassiveSkills",x,passiveY,w,148);p.passiveContent=content;p.passivePrefab=passiveCard;
        var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2((w-60)/5,148);grid.spacing=new Vector2(15,0);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;
        return p;
    }
    static Toggle Audio(Transform parent,string name,float x,string label,string icon,out TextMeshProUGUI text)
    {
        var hit=U.Image(parent,name,null,x,260,390,78);hit.color=Color.clear;hit.raycastTarget=true;
        var toggle=hit.gameObject.AddComponent<Toggle>();toggle.targetGraphic=hit;toggle.transition=Selectable.Transition.None;
        U.Image(hit.transform,"Icon",U.Sprite(icon),0,13,48,48);text=T(hit.transform,"Label",label,66,0,190,78,32,true);text.color=U.Muted;
        var track=U.Image(hit.transform,"Switch",U.Sprite("switch_track_9slice"),258,13,104,52);track.color=U.ColorOf("93B077");
        var knob=U.Image(track.transform,"Knob",U.Sprite("switch_knob"),0,0,44,44);knob.rectTransform.anchorMin=knob.rectTransform.anchorMax=knob.rectTransform.pivot=new Vector2(.5f,.5f);knob.rectTransform.anchoredPosition=new Vector2(29,0);
        var render=hit.gameObject.AddComponent<ThemeAudioToggle>();render.track=track;render.knob=knob.rectTransform;toggle.isOn=true;return toggle;
    }
    static void BuildPause()
    {
        Edit("Assets/Prefab/Result/GamePausePanel.prefab",root=>{
            var panel=Paper(root,936,1430);var logic=root.GetComponent<GamePausePanel>();var p=Presentation(root,panel,false);
            U.Image(panel,"PauseIcon",S("icon_pause"),42,41,44,44);T(panel,"Title","游戏暂停",104,35,470,64,52,true);
            p.level=T(panel,"Level","",480,32,412,60,29,false,TextAlignmentOptions.Right);p.level.color=U.Muted;Line(panel,"TitleLine",42,120,852);
            logic.bgmToggle=Audio(panel,"Music",48,"背景音乐","icon_audio_music",out logic.bgmLabel);logic.sfxToggle=Audio(panel,"Sound",492,"游戏音效","icon_audio_sound",out logic.sfxLabel);Line(panel,"AudioLine",42,350,852);
            logic.skillListParent=p.activeContent;logic.skillRowPrefab=damageRow;logic.passiveSkillListParent=p.passiveContent;logic.passiveSkillRowPrefab=passiveCard;
            logic.quitButton=Action(panel,"Quit",42,1260,310,112,"退出关卡",false);logic.resumeButton=Action(panel,"Resume",380,1260,514,112,"继续战斗");
            BattleDecorationToggleBuilder.BuildInto(root);
        });
    }
    static void BuildResult()
    {
        Edit("Assets/Prefab/Result/GameResultPanel.prefab",root=>{
            var panel=Paper(root,952,1570);var logic=root.GetComponent<GameResultPanel>();var p=Presentation(root,panel,true);
            p.level=T(panel,"Level","",48,28,856,58,32,false,TextAlignmentOptions.Center);p.level.color=U.Muted;
            p.outcomeIcon=U.Image(panel,"OutcomeIcon",U.Sprite("nav_trophy"),425,92,102,96);p.victoryIcon=p.outcomeIcon.sprite;p.defeatIcon=U.Sprite("icon_flag");
            var title=T(panel,"Title","战斗胜利",48,196,856,76,68,true,TextAlignmentOptions.Center);p.title=title;U.Ref(logic,"textBanner",title);
            p.subtitle=T(panel,"Subtitle","调整搭配，再来一局",48,284,856,60,33,false,TextAlignmentOptions.Center);p.subtitle.color=U.Muted;
            var stars=U.Node(panel,"Stars",360,284,232,64);p.stars=stars.gameObject;var starSprite=U.Sprite("star_filled","Assets/Res/image/Home/roadmap/");
            for(int i=0;i<3;i++)U.Ref(logic,"star"+(i+1),U.Image(stars,"Star"+i,starSprite,i*84,0,64,64));U.Ref(logic,"starSprite",starSprite);
            var starSettings=new SerializedObject(logic);starSettings.FindProperty("starActiveColor").colorValue=Color.white;starSettings.FindProperty("starDimColor").colorValue=new Color(1,1,1,.25f);starSettings.ApplyModifiedPropertiesWithoutUndo();
            U.Ref(logic,"textWave",p.wave);U.Ref(logic,"_textTime",p.duration);U.Ref(logic,"_textKillNum",p.kills);U.Ref(logic,"_textLevel",p.level);
            U.Ref(logic,"_scrollContent",p.activeContent);U.Ref(logic,"skillDamageRowPrefab",damageRow);U.Ref(logic,"passiveSkillListParent",p.passiveContent);U.Ref(logic,"passiveSkillRowPrefab",passiveCard);
            T(panel,"RewardsTitle","获得奖励",48,1158,856,54,36,true);p.rewardScroll=Scroll(panel,"Rewards",48,1225,856,96,true);
            U.Ref(logic,"scrollViewRewardContent",p.rewardScroll.content);U.Ref(logic,"itemCellPrefab",rewardCell);
            p.emptyReward=T(panel,"EmptyReward","本局暂无奖励",48,1225,856,96,29);p.emptyReward.color=U.Muted;
            var unlock=U.Image(panel,"UnlockBanner",U.Sprite("home_badge_mint_9slice"),48,1338,856,66);
            var unlockLabel=T(unlock.transform,"Label","",22,0,610,66,30,true);unlockLabel.color=U.ColorOf("4D7651");unlockLabel.enableAutoSizing=true;unlockLabel.fontSizeMin=18;unlockLabel.fontSizeMax=30;unlockLabel.overflowMode=TextOverflowModes.Ellipsis;
            var go=Action(unlock.transform,"Go",648,8,192,50,"前往查看",false);U.Ref(logic,"unlockLabel",unlockLabel);U.Ref(logic,"unlockGoButton",go);
            var goLabel=go.GetComponentInChildren<TMP_Text>();goLabel.fontSize=goLabel.fontSizeMax=28;goLabel.fontSizeMin=24;
            // The banner follows the label, so a run without unlocks leaves no empty colored row.
            var visibility=unlock.gameObject.AddComponent<BattleUnlockVisibility>();visibility.label=unlockLabel;
            U.Ref(logic,"goldEarnedText",null);U.Ref(logic,"goldBalanceText",null);
            U.Ref(logic,"_btnExit",Action(panel,"Home",48,1426,322,110,"返回主界面",false));U.Ref(logic,"_btnAgain",Action(panel,"Retry",394,1426,510,110,"再试一次"));U.Ref(logic,"_btnNext",Action(panel,"Next",394,1426,510,110,"下一关"));
        });
    }
    static void BuildRevive()
    {
        Edit("Assets/Prefab/Result/GameRevivePanel.prefab",root=>{
            var panel=Paper(root,888,988);var logic=root.GetComponent<GameRevivePanel>();var p=root.AddComponent<BattlePanelPresentation>();
            T(panel,"Title","再战一次？",42,36,804,92,60,true,TextAlignmentOptions.Center);
            p.level=T(panel,"Level","",42,143,804,48,30,false,TextAlignmentOptions.Center);p.level.color=U.Muted;
            T(panel,"Description","满血复活，继续当前关卡",42,212,804,58,36,false,TextAlignmentOptions.Center).color=U.Muted;
            var track=U.Image(panel,"CountdownTrack",S("countdown_ring"),300,306,288,288);track.color=U.ColorOf("E2D8BA");
            var ring=U.Image(panel,"CountdownFill",S("countdown_ring"),300,306,288,288);ring.color=U.ColorOf("E8B04D");ring.type=Image.Type.Filled;ring.fillMethod=Image.FillMethod.Radial360;ring.fillOrigin=(int)Image.Origin360.Top;ring.fillClockwise=true;
            U.Ref(logic,"_countdownFillImage",ring);U.Ref(logic,"_textTime",T(panel,"Seconds","10",300,374,288,134,100,true,TextAlignmentOptions.Center));
            T(panel,"SecondsUnit","秒",300,503,288,42,28,false,TextAlignmentOptions.Center).color=U.Muted;T(panel,"CountdownCaption","复活倒计时",42,590,804,58,29,false,TextAlignmentOptions.Center).color=U.Muted;
            var context=U.Image(panel,"Context",U.Sprite("home_card_cream_9slice"),68,656,752,64);
            p.wave=T(context.transform,"Wave","",8,0,246,64,26,false,TextAlignmentOptions.Center);p.kills=T(context.transform,"Kills","",254,0,240,64,26,false,TextAlignmentOptions.Center);p.duration=T(context.transform,"Duration","",494,0,250,64,26,false,TextAlignmentOptions.Center);
            // Context headings remain static, values are rendered into compact separate lines.
            p.wave.rectTransform.sizeDelta=new Vector2(246,36);p.wave.rectTransform.anchoredPosition=new Vector2(8,-25);p.kills.rectTransform.sizeDelta=new Vector2(240,36);p.kills.rectTransform.anchoredPosition=new Vector2(254,-25);p.duration.rectTransform.sizeDelta=new Vector2(250,36);p.duration.rectTransform.anchoredPosition=new Vector2(494,-25);
            T(context.transform,"WaveCaption","波次",8,0,246,28,20,false,TextAlignmentOptions.Center);T(context.transform,"KillsCaption","击杀",254,0,240,28,20,false,TextAlignmentOptions.Center);T(context.transform,"TimeCaption","时长",494,0,250,28,20,false,TextAlignmentOptions.Center);
            var revive=Action(panel,"Revive",44,758,800,108,"看广告复活");U.Image(revive.transform,"AdIcon",U.Sprite("icon_ad_video"),240,34,40,40);var label=revive.transform.Find("Label") as RectTransform;label.anchoredPosition=new Vector2(55,0);label.sizeDelta=new Vector2(732,108);U.Ref(logic,"_btnRevive",revive);
            var exit=Action(panel,"GiveUp",44,873,800,58,"结束本局",false);exit.GetComponent<Image>().color=Color.clear;U.Ref(logic,"_btnExit",exit);
            T(panel,"TimeoutHint","倒计时结束将进入结算",44,936,800,32,27,false,TextAlignmentOptions.Center).color=U.Muted;
        });
    }
    static GameObject BuildDamageRow()
    {
        var root=new GameObject("BattleDamageRow",typeof(RectTransform));U.Rect((RectTransform)root.transform,0,0,856,84);
        try
        {
            var layout=root.AddComponent<LayoutElement>();layout.preferredHeight=84;
            var cell=root.AddComponent<GameResultSkillDamageCell>();var face=U.Image(root.transform,"Face",S("surface_soft_9slice"),0,0,856,84);face.color=U.ColorOf("FFFAED");U.Fill(face.rectTransform);
            U.Ref(cell,"icon",U.Image(root.transform,"Icon",null,14,12,60,60));
            U.Ref(cell,"nameText",T(root.transform,"Name","",101,1,466,44,34,true));U.Ref(cell,"levelText",T(root.transform,"Level","",101,46,78,32,26));
            var damage=T(root.transform,"Damage","",560,0,278,50,37,true,TextAlignmentOptions.Right);U.Ref(cell,"damageText",damage);
            var track=U.Image(root.transform,"DamageTrack",S("surface_soft_9slice"),290,64,542,10);track.color=U.ColorOf("E7E0CE");
            var share=U.Image(track.transform,"Fill",S("surface_soft_9slice"),0,0,542,10);U.Fill(share.rectTransform);share.type=Image.Type.Filled;share.fillMethod=Image.FillMethod.Horizontal;U.Ref(cell,"damageShare",share);
            var badge=U.Image(root.transform,"Breakthrough",S("surface_soft_9slice"),183,50,76,31);badge.color=U.ColorOf("E8DEEF");T(badge.transform,"Label","突破",0,0,76,31,23,false,TextAlignmentOptions.Center);U.Ref(cell,"breakthroughBadge",badge.gameObject);badge.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Result/BattleDamageRow.prefab");
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static GameObject BuildPassiveCard()
    {
        var root=new GameObject("BattlePassiveCard",typeof(RectTransform));U.Rect((RectTransform)root.transform,0,0,159,148);
        try
        {
            var cell=root.AddComponent<GamePassiveSkillCell>();var face=root.AddComponent<Image>();face.sprite=S("surface_soft_9slice");face.type=Image.Type.Sliced;face.raycastTarget=false;face.color=U.ColorOf("EFF1DF");
            U.Ref(cell,"iconImage",U.Image(root.transform,"Icon",null,46,10,67,67));
            var name=T(root.transform,"Name","",2,85,155,37,26,true,TextAlignmentOptions.Center);name.enableAutoSizing=true;name.fontSizeMin=20;name.fontSizeMax=26;U.Ref(cell,"nameText",name);
            U.Ref(cell,"levelText",T(root.transform,"Level","",2,119,155,27,24,false,TextAlignmentOptions.Center));
            var empty=T(root.transform,"Empty","—",40,10,79,70,28,false,TextAlignmentOptions.Center);empty.color=U.Muted;U.Ref(cell,"emptyMark",empty.gameObject);
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Result/BattlePassiveCard.prefab");
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static GameObject BuildRewardCell()
    {
        var root=new GameObject("BattleRewardCell",typeof(RectTransform));U.Rect((RectTransform)root.transform,0,0,310,96);
        try
        {
            var le=root.AddComponent<LayoutElement>();le.preferredWidth=310;le.preferredHeight=96;
            var face=root.AddComponent<Image>();face.sprite=U.Sprite("home_card_cream_9slice");face.type=Image.Type.Sliced;face.raycastTarget=false;
            var cell=root.AddComponent<BattleRewardCell>();cell.coin=U.Sprite("icon_coins");cell.gem=U.Sprite("icon_gem");cell.fallback=U.Sprite("icon_notice_info");cell.characters=AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
            cell.icon=U.Image(root.transform,"Icon",cell.coin,15,17,62,62);cell.icon.preserveAspect=true;cell.fragment=U.Image(root.transform,"Fragment",U.Sprite("icon_fragment_piece"),8,58,28,28);
            cell.label=T(root.transform,"Name","",88,3,209,36,26);cell.label.enableAutoSizing=true;cell.label.fontSizeMin=18;cell.label.fontSizeMax=26;cell.label.color=U.Muted;
            cell.amount=T(root.transform,"Amount","",88,39,209,50,36,true);
            return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Result/BattleRewardCell.prefab");
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
