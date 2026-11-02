#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U=HomeUIBuilderUtility;

public static class BossWheelUIBuilder
{
    const string Folder="Assets/Res/image/Game/bosswheel/";
    const string Prefabs="Assets/Prefab/CardSelect/";
    [MenuItem("工具/Battle UI/Apply Boss Wheel V2")]
    static void Menu(){Debug.Log(Apply());}
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        U.Prepare("boss-wheel");BuildSlot();BuildReward();
        Edit("BossWheelPanel",BuildPanel);AssetDatabase.SaveAssets();
        return "Boss wheel V2 applied. Three existing prefab GUIDs retained; closed/open chest reveal, real skill icons and unscaled motion. No new font or atlas.";
    }
    static Sprite S(string name)=>U.Sprite(name,Folder);
    static void Edit(string name,Action<GameObject> action)
    {
        string path=Prefabs+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{action(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void Clear(GameObject root)
    {
        foreach(var child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        U.RemoveLayout((RectTransform)root.transform);
    }
    static Image Image(Transform p,string name,string sprite,float x,float y,float w,float h)=>U.Image(p,name,sprite==null?null:S(sprite),x,y,w,h);
    static TextMeshProUGUI T(Transform p,string name,string value,float x,float y,float w,float h,float size=32,bool bold=false,TextAlignmentOptions align=TextAlignmentOptions.Center)
        =>U.Text(p,name,value,x,y,w,h,size,U.Ink,bold,align);
    static void BuildSlot()
    {
        Edit("WheelSlotCell",root=>{
            Clear(root);var rt=(RectTransform)root.transform;U.Rect(rt,0,0,100,108);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=Vector2.zero;
            var cell=root.GetComponent<WheelSlotCell>();var face=root.GetComponent<Image>()??root.AddComponent<Image>();
            face.sprite=S("slot_active_9slice");face.type=UnityEngine.UI.Image.Type.Sliced;face.color=Color.white;face.raycastTarget=false;
            cell.face=face;cell.activeFace=face.sprite;cell.passiveFace=S("slot_passive_9slice");cell.winnerFace=S("slot_winner_9slice");
            var cursor=Image(root.transform,"Cursor","cursor_9slice",-3,-3,106,114);cell.highlightMark=cursor.gameObject;cursor.gameObject.SetActive(false);
            cell.icon=Image(root.transform,"Icon",null,12,7,76,70);cell.icon.preserveAspect=true;
            var badge=U.Image(root.transform,"LevelFace",U.Sprite("surface_soft_9slice","Assets/Res/image/Game/battle/"),13,77,74,26);badge.color=U.ColorOf("F6EEDB");
            cell.levelText=T(root.transform,"Level","Lv.2",4,76,92,30,25,true);
            var check=Image(root.transform,"WinnerCheck","winner_check",80,-12,32,32);cell.winnerMark=check.gameObject;check.gameObject.SetActive(false);
        });
    }
    static void BuildReward()
    {
        Edit("WheelRewardSkillCell",root=>{
            Clear(root);U.Rect((RectTransform)root.transform,0,0,264,190);
            var cell=root.GetComponent<WheelRewardSkillCell>();var face=root.GetComponent<Image>()??root.AddComponent<Image>();
            face.sprite=S("reward_active_9slice");face.type=UnityEngine.UI.Image.Type.Sliced;face.color=Color.white;face.raycastTarget=false;
            cell.face=face;cell.activeFace=face.sprite;cell.passiveFace=S("reward_passive_9slice");
            cell.icon=Image(root.transform,"Icon",null,0,10,82,76);cell.icon.preserveAspect=true;
            var icon=cell.icon.rectTransform;icon.anchorMin=icon.anchorMax=icon.pivot=new Vector2(.5f,1);icon.anchoredPosition=new Vector2(0,-10);
            cell.nameText=T(root.transform,"Name","",10,94,244,44,32,true);StretchText(cell.nameText,94,44);cell.nameText.enableAutoSizing=true;cell.nameText.fontSizeMin=24;cell.nameText.fontSizeMax=32;
            cell.levelText=T(root.transform,"Level","",10,137,244,44,30,true);StretchText(cell.levelText,137,44);cell.levelText.color=U.ColorOf("4D7651");
            var group=root.GetComponent<CanvasGroup>();if(group==null)group=root.AddComponent<CanvasGroup>();group.alpha=0;group.interactable=group.blocksRaycasts=false;
        });
    }
    static void StretchText(TMP_Text text,float y,float h)
    {
        var rt=text.rectTransform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=new Vector2(1,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-y);rt.sizeDelta=new Vector2(-20,h);
    }
    // Screen-space design coordinates are converted into the centered 960 x 1596 frame.
    static Image I(Transform p,string name,Sprite sprite,float x,float y,float w,float h)=>U.Image(p,name,sprite,x-60,y-162,w,h);
    static TextMeshProUGUI Label(Transform p,string name,string value,float x,float y,float w,float h,float size=32,bool bold=false,TextAlignmentOptions align=TextAlignmentOptions.Center)
        =>T(p,name,value,x-60,y-162,w,h,size,bold,align);
    static CanvasGroup Group(Image image)
    {
        var group=image.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.interactable=group.blocksRaycasts=false;return group;
    }
    static Image Effect(Transform p,string name,Sprite sprite,float size)
    {
        var image=U.Image(p,name,sprite,0,0,size,size);var rt=image.rectTransform;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=Vector2.zero;return image;
    }
    static Button Action(Transform p,string name,string value)
    {
        const string chars="Assets/Res/image/Home/character/";
        var face=I(p,name,U.Sprite("character_start_normal_9slice",chars),215,1561,650,112);var button=U.Button(face);
        button.transition=Selectable.Transition.SpriteSwap;var state=button.spriteState;state.pressedSprite=U.Sprite("character_start_pressed_9slice",chars);state.disabledSprite=U.Sprite("character_start_disabled_9slice",chars);button.spriteState=state;
        T(face.transform,"Label",value,12,0,626,112,45,true);return button;
    }
    static void BuildPanel(GameObject root)
    {
        Clear(root);U.Fill((RectTransform)root.transform);
        foreach(var old in root.GetComponents<BattleThemeLayout>())UnityEngine.Object.DestroyImmediate(old);
        foreach(var old in root.GetComponents<BossWheelPresentation>())UnityEngine.Object.DestroyImmediate(old);
        var panel=root.GetComponent<BossWheelPanel>();var mask=root.GetComponent<Image>();mask.sprite=null;mask.type=UnityEngine.UI.Image.Type.Simple;mask.color=new Color32(38,60,44,156);mask.raycastTarget=true;panel.backgroundBlocker=mask;
        var frame=U.Node(root.transform,"Frame",0,0,960,1596);
        var fit=root.AddComponent<BattleThemeLayout>();fit.panel=frame;fit.width=960;fit.height=1596;
        I(frame,"Paper",U.Sprite("dialog_panel_cream_9slice"),60,206,960,1540).raycastTarget=true;
        var badge=I(frame,"BossDefeated",S("reward_passive_9slice"),365,178,350,58);T(badge.transform,"Label","首领已击败",0,0,350,58,29,true).color=U.ColorOf("4D7651");
        var leftStar=I(frame,"TitleSparkLeft",S("sparkle"),268,272,36,36);leftStar.color=U.ColorOf("F6B94E");
        var rightStar=I(frame,"TitleSparkRight",S("sparkle"),776,272,36,36);rightStar.color=leftStar.color;
        Label(frame,"Title","首领奖励",320,247,440,78,64,true);
        Label(frame,"Subtitle","随机获得 1–3 次技能升级",200,325,680,46,32).color=U.Muted;
        I(frame,"Wheel",S("wheel_base"),108,378,864,864);
        var view=root.AddComponent<BossWheelPresentation>();U.Ref(panel,"presentation",view);
        view.warmGlow=Group(I(frame,"WarmGlow",S("warm_glow"),315,559,450,450));view.warmGlow.GetComponent<Image>().color=U.ColorOf("FFE9A0");
        view.revealRays=Group(I(frame,"RevealRays",S("reveal_rays"),306,550,468,468));view.revealRays.GetComponent<Image>().color=U.ColorOf("FFE39A");
        var motion=U.Node(frame,"ChestMotion",260,358,440,440);
        motion.pivot=new Vector2(.5f,65f/440f);motion.anchoredPosition=new Vector2(480,-733);view.chestMotion=motion;
        var closed=U.Image(motion,"Closed",S("chest_closed"),0,0,440,440);U.Fill(closed.rectTransform);view.chestClosed=Group(closed);view.chestClosed.alpha=1;
        var opened=U.Image(motion,"Open",S("chest_open"),0,0,440,440);U.Fill(opened.rectTransform);view.chestOpen=Group(opened);
        view.revealFlash=Group(I(frame,"RevealFlash",S("warm_glow"),340,590,400,400));view.revealFlash.GetComponent<Image>().color=U.ColorOf("FFFCEE");
        view.centerTitle=Label(frame,"CenterTitle","幸运转一转",306,929,468,58,40,true);
        view.centerSubtitle=Label(frame,"CenterSubtitle","闭合的宝箱，藏着新的力量",295,987,490,44,28);view.centerSubtitle.color=U.Muted;
        var container=U.Node(frame,"Slots",480,648,0,0);container.pivot=new Vector2(.5f,.5f);U.Ref(panel,"slotContainer",container);
        U.Ref(panel,"slotPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"WheelSlotCell.prefab").GetComponent<WheelSlotCell>());
        var so=new SerializedObject(panel);so.FindProperty("wheelRadius").floatValue=356;so.FindProperty("spinDuration").floatValue=1.6f;so.FindProperty("stopDuration").floatValue=.10f;so.FindProperty("extraLapsBeforeWinner").intValue=0;so.ApplyModifiedPropertiesWithoutUndo();
        var instructions=U.Node(frame,"Instructions",0,0,960,1596);view.instructions=instructions.gameObject;
        I(instructions,"Rule",null,176,1302,728,2).color=U.ColorOf("D7C8A6");
        I(instructions,"ActiveDot",S("warm_glow"),339,1335,20,20).color=U.ColorOf("C99E45");Label(instructions,"ActiveLegend","输出技能",374,1328,160,44,29,false,TextAlignmentOptions.Left).color=U.Muted;
        I(instructions,"PassiveDot",S("warm_glow"),603,1335,20,20).color=U.ColorOf("8BA66B");Label(instructions,"PassiveLegend","被动增益",638,1328,160,44,29,false,TextAlignmentOptions.Left).color=U.Muted;
        Label(instructions,"Hint","亮起的技能，每次提升 1 级",184,1380,712,48,34,true);
        Label(instructions,"PoolHint","仅从本局已拥有的可升级技能中抽取",130,1430,820,42,27).color=U.Muted;
        var toggleNode=U.Node(frame,"SkipAnimation",349,1332,382,40);var toggle=toggleNode.gameObject.AddComponent<Toggle>();toggle.transition=Selectable.Transition.None;
        var check=U.Image(toggleNode,"Box",U.Sprite("check_square_grey"),0,0,32,32);check.raycastTarget=true;toggle.targetGraphic=check;
        var tick=U.Image(check.transform,"Check",S("winner_check"),0,0,32,32);toggle.graphic=tick;
        T(toggleNode,"Label","跳过动画",50,-4,270,40,28,false,TextAlignmentOptions.Left).color=U.Muted;panel.skipAnimationToggle=toggle;
        panel.spinButton=Action(frame,"Start","开始转盘");panel.spinButtonText=panel.spinButton.GetComponentInChildren<TextMeshProUGUI>();
        panel.confirmButton=Action(frame,"Continue","继续战斗");panel.confirmButton.gameObject.SetActive(false);
        view.status=Label(frame,"Status","",180,1591,720,58,34,true);view.status.color=U.Muted;
        view.footer=Label(frame,"Footer","击败首领奖励 · 无需消耗",160,1688,760,44,27);view.footer.color=U.Muted;
        var result=U.Node(frame,"Result",0,0,960,1596);panel.resultGroup=result.gameObject;
        Label(result,"Heading","本次强化",108,1244,460,48,32,true,TextAlignmentOptions.Left);
        view.resultCount=Label(result,"Count","3 次升级",700,1244,272,48,27,false,TextAlignmentOptions.Right);view.resultCount.color=U.ColorOf("4D7651");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"WheelRewardSkillCell.prefab");panel.rewardCells=new WheelRewardSkillCell[3];
        for(int i=0;i<3;i++)
        {
            var child=(GameObject)PrefabUtility.InstantiatePrefab(prefab,result);child.name="Reward"+i;U.Rect((RectTransform)child.transform,48+i*300,1138,264,190);panel.rewardCells[i]=child.GetComponent<WheelRewardSkillCell>();
        }
        result.gameObject.SetActive(false);
        var fx=U.Node(frame,"MotionLayer",0,0,960,1596);view.effectsLayer=fx;
        view.sparkles=new Image[15];for(int i=0;i<15;i++){var spark=Effect(fx,"Spark"+i,S("sparkle"),14+(i%3)*6);view.sparkles[i]=spark;spark.gameObject.SetActive(false);}
        view.energyOrbs=new Image[3];for(int i=0;i<3;i++){view.energyOrbs[i]=Effect(fx,"Energy"+i,S("warm_glow"),40);view.energyOrbs[i].gameObject.SetActive(false);}
        view.flyingCards=new CanvasGroup[3];view.flyingIcons=new Image[3];
        for(int i=0;i<3;i++)
        {
            var face=Effect(fx,"FlyingCard"+i,S("slot_winner_9slice"),112);view.flyingCards[i]=Group(face);
            view.flyingIcons[i]=U.Image(face.transform,"Icon",null,12,12,88,88);view.flyingIcons[i].preserveAspect=true;
        }
        fit.Refresh();
    }
}
#endif
