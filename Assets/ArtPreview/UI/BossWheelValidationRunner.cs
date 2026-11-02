#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BossWheelValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder=>"BossWheel";
    protected override string ValidationNote=>"Actual registered BossWheelPanel and Game scene; unscaled runtime animation, real PlayerSkills upgrades and native BossWheelController. Isolated in-memory profile. No WeChat-device or final build-size validation.";
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
    PlayerSkills skills;BossWheelController controller;BossWheelPanel wheel;
    int awards,closed;
    bool recording, stillCapture;
    readonly List<float> frameTimes=new List<float>();
    UnityEngine.Random.State randomState;
    readonly int[] order={0,1,2,3,4,5,6,0,1,3,2,6,4,5,1,0};
    WheelSlotData[] Data()
    {
        var names=new[]{"刀光剑影","回风舞柳","石破天惊","钢筋铁骨","气贯长虹","生生不息","致命一击"};
        var defs=names.Select(n=>skills.skillCatalog.skills.First(d=>d!=null&&d.displayName==n)).ToArray();
        return order.Select(i=>{var d=defs[i];bool active=!d.id.IsPassive();int level=active?skills.GetSkillLevel(d.id):skills.GetPassiveSkillLevel(d.id);return new WheelSlotData{skillId=d.id,def=d,currentLevel=level,targetLevel=level+1,isActive=active,weight=active?3:1};}).ToArray();
    }
    int Level(WheelSlotData data)=>data.isActive?skills.GetSkillLevel(data.skillId):skills.GetPassiveSkillLevel(data.skillId);
    BossWheelPanel Open(WheelSlotData[] data,int[] winners,bool skip=false)
    {
        var payload=new BossWheelOpenPayload{Slots=data,WinningIndices=winners,OnSpinComplete=()=>{awards++;controller.ApplyRewards(data,winners);},OnClose=()=>closed++};
        var panel=UIManager.Instance.Open<BossWheelPanel>(payload,new UiOpenOptions{PauseTime=true,UseUnscaledTime=true,CloseOnBack=false});
        panel.skipAnimationToggle.SetIsOnWithoutNotify(skip);wheel=panel;return panel;
    }
    IEnumerator Until(Func<bool> condition,float limit=9)
    {
        float end=Time.realtimeSinceStartup+limit;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
    }
    IEnumerator QuickCapture(string name)
    {
        stillCapture=true;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;stillCapture=false;
    }
    IEnumerator Still(string name){stillCapture=true;yield return Capture(name);stillCapture=false;}
    IEnumerator MotionFrames()
    {
        Directory.CreateDirectory(Path.Combine(output,"frames"));int frame=0;float began=Time.realtimeSinceStartup;
        while(recording&&frame<120)
        {
            yield return new WaitForEndOfFrame();
            if(!recording)break;
            if(stillCapture){yield return null;continue;}
            frameTimes.Add(Time.realtimeSinceStartup-began);
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"frames","frame-"+frame.ToString("D4")+".png"));frame++;
            yield return new WaitForSecondsRealtime(.08f);
        }
        File.WriteAllText(Path.Combine(output,"frame-times.json"),"["+string.Join(",",frameTimes.Select(t=>t.ToString(System.Globalization.CultureInfo.InvariantCulture)))+"]");
    }
    protected override IEnumerator RunChecks()
    {
        randomState=UnityEngine.Random.state;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return new WaitForSecondsRealtime(.5f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var coordinator in FindObjectsOfType<BattleOutcomeCoordinator>()){coordinator.StopAllCoroutines();coordinator.enabled=false;}
        UIManager.Instance.CloseAllStack();Time.timeScale=1;
        skills=FindObjectOfType<PlayerSkills>();controller=FindObjectOfType<BossWheelController>();
        Record("Game provides the registered wheel, controller and player skills",skills!=null&&controller!=null&&UIManager.Instance!=null);
        var defs=skills.skillCatalog.skills;
        foreach(var name in new[]{"刀光剑影","回风舞柳","石破天惊","钢筋铁骨","气贯长虹","生生不息","致命一击"})
        {
            var def=defs.First(d=>d!=null&&d.displayName==name);if(def.id.IsPassive())skills.TryAddPassive(def.id);else skills.TryAddSkill(def.id);
        }
        if(skills.GetSkillLevel(SkillId.AutoProjectileSword)<2)skills.TryLevelUp(SkillId.AutoProjectileSword);
        // Two additional sword upgrades require its original breakthrough passive at Lv.5.
        var swordDef=skills.skillCatalog.Get(SkillId.AutoProjectileSword);
        if(swordDef.bondedPassiveId!=SkillId.None)skills.TryAddPassive(swordDef.bondedPassiveId);
        var data=Data();var indices=new[]{0,3,11};var before=indices.Select(i=>Level(data[i])).ToArray();
        var panel=Open(data,indices);yield return new WaitForSecondsRealtime(.4f);var view=panel.GetComponent<BossWheelPresentation>();
        var cells=Field<WheelSlotCell[]>(panel,"_wheelSlots");
        Record("Wheel opens paused with sixteen upright real skill slots",Time.timeScale==0&&cells.Length==16&&cells.All(c=>c.icon.sprite!=null&&c.transform.localRotation==Quaternion.identity));
        Record("Closed chest and original skip option appear before draw",view.Stage=="Idle"&&view.chestClosed.alpha==1&&view.chestOpen.alpha==0&&panel.skipAnimationToggle.gameObject.activeInHierarchy);
        Record("Back key cannot dismiss the reward flow",!panel.LastOptions.CloseOnBack);
        panel.confirmButton.onClick.Invoke();Record("Early confirm does not close or grant awards",awards==0&&closed==0&&UIManager.Instance.Top==panel);
        yield return Still("ready-1080x1920");
        recording=true;StartCoroutine(MotionFrames());
        float start=Time.realtimeSinceStartup;panel.spinButton.onClick.Invoke();panel.spinButton.onClick.Invoke();
        yield return Until(()=>view.Stage=="Charge");
        Record("Full draw advances in paused gameplay before granting rewards",view.Stage=="Charge"&&Time.timeScale==0&&awards==0&&!panel.confirmButton.gameObject.activeInHierarchy);
        Record("Exactly the predetermined slots stay selected",cells.Where(c=>c.IsWinner).Count()==3&&indices.All(i=>cells[i].IsWinner));
        yield return new WaitForSecondsRealtime(.18f);yield return QuickCapture("charge-1080x1920");
        yield return Until(()=>view.Stage=="Opening");yield return new WaitForSecondsRealtime(.11f);yield return QuickCapture("opening-1080x1920");
        yield return Until(()=>view.Stage=="Rewards");
        Record("Rewards remain non-interactive until landing completes",!panel.confirmButton.gameObject.activeInHierarchy&&awards==1);
        yield return Until(()=>view.IsComplete);
        float elapsed=Time.realtimeSinceStartup-start;
        Record("Full reveal completes once in bounded unscaled time",view.IsComplete&&elapsed<7&&awards==1,"Seconds: "+elapsed.ToString("F2"));
        Record("Three native skill upgrades match the displayed rewards",indices.Select((idx,i)=>Level(data[idx])==before[i]+1).All(v=>v));
        Record("Opened chest and three visible reward cards persist",view.chestOpen.alpha==1&&view.chestClosed.alpha==0&&panel.rewardCells.All(c=>c.gameObject.activeInHierarchy&&c.GetComponent<CanvasGroup>().alpha==1));
        Record("Continue is enabled only after reveal",panel.confirmButton.gameObject.activeInHierarchy&&panel.confirmButton.interactable);
        var font=panel.GetComponentInChildren<TMP_Text>(true).font;
        Record("All wheel labels share the existing font and single atlas",panel.GetComponentsInChildren<TMP_Text>(true).All(t=>t.font==font)&&AssetDatabase.GetAssetPath(font)=="Assets/Resources/Fonts/msyh SDF.asset"&&font.atlasTextureCount==1);
        Record("Visible wheel text has glyph coverage",panel.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.gameObject.activeInHierarchy).All(t=>font.HasCharacters(t.text)));
        yield return Still("result-three-1080x1920");recording=false;yield return new WaitForSecondsRealtime(.15f);
        changeResolution(1080,1440);yield return null;yield return null;
        Record("Short screen keeps paper and continue inside safe area",Inside(panel.GetComponent<BattleThemeLayout>().panel)&&Inside((RectTransform)panel.confirmButton.transform));yield return Still("result-short-1080x1440");
        changeResolution(1080,2400);yield return null;yield return null;
        Record("Long screen keeps wheel proportions and footer reachable",Inside(panel.GetComponent<BattleThemeLayout>().panel)&&Mathf.Abs(((RectTransform)cells[0].transform).rect.width-100)<.01f);yield return Still("result-long-1080x2400");changeResolution(1080,1920);
        panel.confirmButton.onClick.Invoke();panel.confirmButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
        Record("Repeated continue calls close callback once and release pause",closed==1&&Time.timeScale>0&&!panel.gameObject.activeInHierarchy);
        data=Data();int sword=Level(data[0]);panel=Open(data,new[]{0,7},true);yield return new WaitForSecondsRealtime(.35f);view=panel.GetComponent<BossWheelPresentation>();
        Record("Reused panel clears opened chest, previous cards and winners",view.Stage=="Idle"&&view.chestOpen.alpha==0&&!panel.resultGroup.activeSelf&&Field<WheelSlotCell[]>(panel,"_wheelSlots").All(c=>!c.IsWinner));
        start=Time.realtimeSinceStartup;panel.spinButton.onClick.Invoke();panel.spinButton.onClick.Invoke();yield return Until(()=>view.IsComplete,3);
        Record("Skip retains brief opening and landing with one grant",view.IsComplete&&awards==2&&Time.realtimeSinceStartup-start<1.8f);
        Record("Duplicate skill prizes accumulate levels in order",panel.rewardCells[0].levelText.text=="Lv."+sword+" → Lv."+(sword+1)&&panel.rewardCells[1].levelText.text=="Lv."+(sword+1)+" → Lv."+(sword+2)&&Level(data[0])==sword+2);
        Record("Two rewards hide third slot and stay centered",!panel.rewardCells[2].gameObject.activeSelf&&Mathf.Abs(((RectTransform)panel.rewardCells[0].transform).rect.width-374)<.1f);
        yield return Still("result-two-1080x1920");panel.confirmButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
        data=Data();int grenade=Level(data[2]);panel=Open(data,new[]{2},true);yield return new WaitForSecondsRealtime(.35f);view=panel.GetComponent<BossWheelPresentation>();panel.spinButton.onClick.Invoke();yield return Until(()=>view.IsComplete,3);
        Record("Single reward uses one centered card and a real upgrade",panel.rewardCells[0].gameObject.activeInHierarchy&&!panel.rewardCells[1].gameObject.activeSelf&&!panel.rewardCells[2].gameObject.activeSelf&&Mathf.Abs(((RectTransform)panel.rewardCells[0].transform).rect.width-480)<.1f&&Level(data[2])==grenade+1&&awards==3);
        yield return Still("result-one-1080x1920");panel.confirmButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
        panel=Open(Data(),indices);yield return new WaitForSecondsRealtime(.35f);panel.spinButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);UIManager.Instance.CloseAllStack();yield return new WaitForSecondsRealtime(.4f);
        Record("Closing mid-spin cancels motion without late rewards",awards==3&&!panel.gameObject.activeInHierarchy&&panel.GetComponent<BossWheelPresentation>().flyingCards.All(g=>g.alpha==0));
        int ended=0;Action<CardSelectionEndedEvent> onEnded=e=>ended++;EventBus.Subscribe(onEnded,owner:this);
        controller.StartWheel();yield return new WaitForSecondsRealtime(.35f);panel=UIManager.Instance.Top as BossWheelPanel;
        Record("Native BossWheelController opens the same new prefab",panel!=null&&panel.GetComponent<BossWheelPresentation>()!=null&&Time.timeScale==0);
        if(panel!=null)
        {
            var nativeSlots=Field<WheelSlotData[]>(panel,"_slots");var nativeWinners=Field<int[]>(panel,"_winningIndices");
            Record("Native weighted pool keeps sixteen slots and one to three prizes",nativeSlots.Length==16&&nativeWinners.Length>=1&&nativeWinners.Length<=3&&nativeSlots.All(s=>s.def!=null));
            panel.skipAnimationToggle.SetIsOnWithoutNotify(true);panel.spinButton.onClick.Invoke();view=panel.GetComponent<BossWheelPresentation>();yield return Until(()=>view.IsComplete,3);panel.confirmButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);
            Record("Native continue dispatches one battle-resume event",ended==1&&Time.timeScale>0);
        }
        EventBus.Unsubscribe(onEnded);UnityEngine.Random.state=randomState;Time.timeScale=1;
    }
    static bool Inside(RectTransform rt)
    {
        var corners=new Vector3[4];rt.GetWorldCorners(corners);var canvas=rt.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var safe=Screen.safeArea;
        return corners.All(v=>{var p=RectTransformUtility.WorldToScreenPoint(camera,v);return p.x>=safe.xMin-1&&p.x<=safe.xMax+1&&p.y>=safe.yMin-1&&p.y<=safe.yMax+1;});
    }
    protected override void OnDestroy(){recording=false;Time.timeScale=1;UnityEngine.Random.state=randomState;base.OnDestroy();}
}
#endif
