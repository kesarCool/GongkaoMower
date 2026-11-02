#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleUIValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "Battle";
    protected override string ValidationNote => "Unity Editor, actual Game scene and registered prefabs; isolated profile and injected ad callbacks; current terrain retained. No WeChat-device, real-ad-SDK or final build-size validation.";
    bool audioSaved, bgm, sfx;
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object target,string name) { return (T)target.GetType().GetField(name,Fields).GetValue(target); }
    static void Set(object target,string name,object value) { target.GetType().GetField(name,Fields).SetValue(target,value); }
    static void Call(object target,string name,params object[] args) { target.GetType().GetMethod(name,Fields).Invoke(target,args); }
    protected override IEnumerator RunChecks()
    {
        yield return SceneManager.UnloadSceneAsync("Home");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null;yield return null;yield return new WaitForSecondsRealtime(.5f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines();spawner.enabled=false; }
        foreach(var coordinator in FindObjectsOfType<BattleOutcomeCoordinator>()) {coordinator.StopAllCoroutines();coordinator.enabled=false;}
        Time.timeScale=0;
        var hud=FindObjectOfType<GameLayer>();var skills=FindObjectOfType<PlayerSkills>();var energy=FindObjectOfType<PlayerEnergy>();
        Record("Actual Game scene provides HUD, player, input and UI manager",hud!=null && skills!=null && FindObjectOfType<TouchLayer>()!=null && UIManager.Instance!=null);
        Set(hud,"_timeElapsed",106f);Set(hud,"_currentWave",4);Set(hud,"_totalWaves",7);hud.SetKills(257);Call(hud,"RefreshTimerText");Call(hud,"RefreshWaveText");
        Set(energy,"_triggerCount",13);energy.energy=Mathf.Max(1,energy.EnergyRequiredForNextCard/2);energy.OnEnergyChanged.Invoke(energy.energy);
        Record("Energy event updates progress and level",hud.energyProgressSlider.value>0 && hud.energyProgressText.text.Contains("Lv"));
        Record("Energy fill width stays proportional to track",Mathf.Abs(hud.energyProgressSlider.fillRect.rect.width / ((RectTransform)hud.energyProgressSlider.fillRect.parent).rect.width-hud.energyProgressSlider.value)<.01f);
        Record("No persistent equipped-skill HUD or opaque bottom bar",hud.transform.Find("EquippedSkills")==null && hud.GetComponent<Image>().enabled==false && hud.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget));
        Record("Minimap uses the bound prefab view",hud.transform.Find("MiniMap/MapView").GetComponent<RawImage>().texture!=null);
        var orbAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Energy.prefab");var orb=orbAsset.GetComponent<SpriteRenderer>();
        Record("Pickup now references mint orb rather than currency",AssetDatabase.GetAssetPath(orb.sprite).EndsWith("Game/battle/energy_orb.png") && orbAsset.GetComponent<EnergyPickup>().amount==1 && Mathf.Abs(orbAsset.GetComponent<CircleCollider2D>().radius-.16f)<.001f);
        var pauseBridge=FindObjectOfType<PauseButtonBridge>();
        var orbSamples=new List<GameObject>();
        var camera=Camera.main;
        foreach(var pos in new[]{new Vector2(.3f,.4f),new Vector2(.7f,.52f),new Vector2(.4f,.62f),new Vector2(.8f,.3f)})
        { var world=camera.ViewportToWorldPoint(new Vector3(pos.x,pos.y,Mathf.Abs(camera.transform.position.z)));world.z=0;orbSamples.Add(Instantiate(orbAsset,world,Quaternion.identity)); }
        yield return Capture("gameplay-1080x1920");
        var hitPointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(Screen.width*.8f,Screen.height*.15f)};
        var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(hitPointer,hits);
        Record("Lower operation area raycasts reach TouchLayer",hits.Count>0 && hits[0].gameObject.GetComponent<TouchLayer>()!=null);
        var pauseRect=(RectTransform)pauseBridge.transform;hitPointer.position=RectTransformUtility.WorldToScreenPoint(null,pauseRect.TransformPoint(pauseRect.rect.center));hits.Clear();UnityEngine.EventSystems.EventSystem.current.RaycastAll(hitPointer,hits);
        Record("Pause button remains reachable above TouchLayer",hits.Count>0 && hits[0].gameObject.GetComponentInParent<PauseButtonBridge>()==pauseBridge);
        Call(hud,"ShowBossHp","字灵首领");yield return null;
        Record("Boss HP replaces energy and Lv badge",hud.bossHpSlider.gameObject.activeInHierarchy && !hud.energyProgressSlider.gameObject.activeInHierarchy && !hud.energyLevelBadge.activeSelf);
        yield return Capture("gameplay-boss-1080x1920");Call(hud,"HideBossHp");
        foreach(var sample in orbSamples)Destroy(sample);
        Record("Energy HUD restored after Boss",hud.energyProgressSlider.gameObject.activeInHierarchy && !hud.bossHpSlider.gameObject.activeInHierarchy && hud.energyLevelBadge.activeSelf);
        // Fixture skills exercise real catalog icons, maximum capacity and breakthrough state.
        var active=new[]{SkillId.AutoProjectileSword,SkillId.OrbitingBlades,SkillId.ThrowGrenade,SkillId.LineBeam,SkillId.FieldGenerator};
        foreach(var id in active)skills.TryAddSkill(id);
        var passives=skills.skillCatalog.skills.Where(d=>d!=null && d.id.IsPassive()).Take(5).ToArray();
        foreach(var def in passives)skills.TryAddPassive(def.id);
        for(int i=0;i<4;i++)skills.TryLevelUp(SkillId.AutoProjectileSword);
        var ids=new List<SkillId>();skills.GetEquippedSkillIdsOrdered(ids);BattleRunMetrics.BeginBattle();for(int i=0;i<ids.Count;i++)BattleRunMetrics.AddSkillDamage(ids[i],i==0?35242:7129-i*500);
        Time.timeScale=1;pauseBridge.GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
        var pause=UIManager.Instance.Top as GamePausePanel;var p=pause.GetComponent<BattlePanelPresentation>();
        Record("Pause bridge opens themed panel with pause lock",pause!=null && Time.timeScale==0);
        Record("Pause preserves all five active and passive slots",p.activeContent.childCount==ids.Count && p.passiveContent.childCount==5);
        Record("Breakthrough uses badge without recoloring skill art",p.activeContent.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Breakthrough" && t.gameObject.activeSelf));
        bgm=AudioService.Instance.BgmMute;sfx=AudioService.Instance.SfxMute;audioSaved=true;
        pause.bgmToggle.isOn=!pause.bgmToggle.isOn;pause.sfxToggle.isOn=!pause.sfxToggle.isOn;
        Record("Existing audio toggle callbacks still work",AudioService.Instance.BgmMute!=bgm && AudioService.Instance.SfxMute!=sfx);
        pause.bgmToggle.isOn=!bgm;pause.sfxToggle.isOn=!sfx;
        yield return Capture("pause-1080x1920");
        pause.quitButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);var confirm=FindObjectOfType<UiConfirmDialog>();
        Record("Quit retains second confirmation",confirm!=null && confirm.gameObject.activeInHierarchy && confirm.messageText.text.Contains("不会保存"));
        confirm.cancelButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        Record("Cancel confirmation preserves paused run",UIManager.Instance.Top==pause && Time.timeScale==0);
        pause.resumeButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        Record("Resume releases pause lock",!pause.gameObject.activeInHierarchy && Time.timeScale>0);
        var model=new GameResultViewModel{victory=false,completedWaves=3,totalWaves=7,battleDurationUnscaled=118,killCount=257,stars=0,rewardItems=new List<RewardItemEntry>{new RewardItemEntry{itemId=1,itemName="金币",count=60}}};
        var result=UIManager.Instance.Open<GameResultPanel>(model);yield return new WaitForSecondsRealtime(.4f);var r=result.GetComponent<BattlePanelPresentation>();
        Record("Defeat has retry, no stars, and real reward payload",!r.stars.activeSelf && Field<Button>(result,"_btnAgain").gameObject.activeSelf && !Field<Button>(result,"_btnNext").gameObject.activeSelf && r.rewardScroll.content.GetComponentInChildren<BattleRewardCell>().amount.text=="+60");
        Record("Result stats come from payload",r.wave.text=="3 / 7" && r.duration.text=="01:58" && r.kills.text=="257");
        Record("Fifth active row remains scrollable",r.activeContent.childCount==ids.Count && (ids.Count<5 || r.activeContent.GetComponent<RectTransform>().rect.height>r.activeScroll.viewport.rect.height));
        Record("Active viewport width is not doubled by stretch anchors",Mathf.Abs(((RectTransform)r.activeContent).rect.width-r.activeScroll.viewport.rect.width)<1);
        Record("Reward chips retain their background height",((RectTransform)r.rewardScroll.content.GetChild(0)).rect.height>=95);
        r.activeScroll.verticalNormalizedPosition=0;yield return null;
        var lastRow=(RectTransform)r.activeContent.GetChild(r.activeContent.childCount-1);
        Record("Active scrolling reaches the last equipped skill",r.activeScroll.viewport.rect.Overlaps(new Rect(r.activeScroll.viewport.InverseTransformPoint(lastRow.position),lastRow.rect.size)));
        r.activeScroll.verticalNormalizedPosition=1;
        yield return Capture("result-defeat-1080x1920");
        UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        model.victory=true;model.stars=2;model.completedWaves=7;model.unlockedCharacters=new List<string>{"爆爆"};model.rewardItems.Add(new RewardItemEntry{itemId=102,itemName="飞刀娃(碎片)",count=2});
        result=UIManager.Instance.Open<GameResultPanel>(model);yield return new WaitForSecondsRealtime(.4f);
        Record("Victory restores stars, next-level and unlock controls",r.stars.activeSelf && !Field<Button>(result,"_btnAgain").gameObject.activeSelf && Field<Button>(result,"_btnNext").gameObject.activeSelf && Field<Button>(result,"unlockGoButton").gameObject.activeSelf);
        Record("Rewards use readable chips with fragment portrait",r.rewardScroll.content.childCount==2 && r.rewardScroll.content.GetComponentsInChildren<BattleRewardCell>().Last().fragment.gameObject.activeSelf);
        Record("Next action stays disabled until actual next level is unlocked",!Field<Button>(result,"_btnNext").interactable);
        yield return Capture("result-victory-1080x1920");
        changeResolution(1080,1440);yield return null;yield return null;
        Record("Short screen keeps result paper and footer inside safe bounds",Inside(result.GetComponent<BattleThemeLayout>().panel));yield return Capture("result-short-1080x1440");
        changeResolution(1080,2400);yield return Capture("result-long-1080x2400");changeResolution(1080,1920);
        UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        fixture.SetProgress(2);SelectedLevelContext.Set(1,102);result=UIManager.Instance.Open<GameResultPanel>(model);yield return new WaitForSecondsRealtime(.4f);
        Record("Next action enables when progression unlocks the next level",Field<Button>(result,"_btnNext").interactable);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        model.victory=false;model.unlockedCharacters=null;model.rewardItems.Clear();result=UIManager.Instance.Open<GameResultPanel>(model);yield return new WaitForSecondsRealtime(.4f);
        Record("Reuse clears unlock, stars and old rewards",!Field<Button>(result,"unlockGoButton").gameObject.activeSelf && !r.stars.activeSelf && r.rewardScroll.content.childCount==0 && r.emptyReward.gameObject.activeSelf);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        int revived=0,gaveUp=0;var provider=new TestAd();
        var payload=new GameRevivePanelPayload{countdownSeconds=10,adProvider=provider,onRevived=()=>revived++,onGiveUp=()=>gaveUp++};
        var revive=UIManager.Instance.Open<GameRevivePanel>(payload);yield return new WaitForSecondsRealtime(1.3f);
        var fill=Field<Image>(revive,"_countdownFillImage");var seconds=Field<TextMeshProUGUI>(revive,"_textTime");
        Record("Revive countdown advances while gameplay is paused",Time.timeScale==0 && int.Parse(seconds.text)<10 && fill.fillAmount<1 && fill.fillMethod==Image.FillMethod.Radial360);
        Record("Revive has centered countdown and no portrait",Mathf.Abs(seconds.rectTransform.anchoredPosition.x+seconds.rectTransform.rect.width/2-444)<1 && revive.GetComponentsInChildren<Image>(true).All(i=>i.name!="Portrait"));
        yield return Capture("revive-1080x1920");
        Field<Button>(revive,"_btnRevive").onClick.Invoke();Field<Button>(revive,"_btnRevive").onClick.Invoke();
        Record("Ad request debounced and buttons disabled",provider.requests==1 && !Field<Button>(revive,"_btnRevive").interactable);provider.callback(true);
        Record("Ad success dispatches existing revive callback",revived==1 && gaveUp==0);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        payload.countdownSeconds=.2f;revive=UIManager.Instance.Open<GameRevivePanel>(payload);yield return new WaitForSecondsRealtime(.8f);
        Record("Unscaled timeout dispatches settlement once",gaveUp==1);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        payload.countdownSeconds=10;revive=UIManager.Instance.Open<GameRevivePanel>(payload);yield return new WaitForSecondsRealtime(.3f);Field<Button>(revive,"_btnExit").onClick.Invoke();
        Record("End run dispatches existing give-up callback",gaveUp==2);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        revive=UIManager.Instance.Open<GameRevivePanel>(payload);yield return new WaitForSecondsRealtime(.3f);Field<Button>(revive,"_btnRevive").onClick.Invoke();provider.callback(false);
        Record("Ad failure dispatches existing give-up callback",gaveUp==3);UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        pauseBridge.GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);changeResolution(1080,1440);yield return null;yield return null;
        Record("Short pause fits paper without hiding footer",Inside(pause.GetComponent<BattleThemeLayout>().panel));yield return Capture("pause-short-1080x1440");UIManager.Instance.CloseTop();yield return new WaitForSecondsRealtime(.3f);
        changeResolution(1080,2400);yield return Capture("gameplay-long-1080x2400");changeResolution(1080,1920);
        RestoreAudio();Time.timeScale=1;
    }
    static bool Inside(RectTransform rt)
    {
        var corners=new Vector3[4];rt.GetWorldCorners(corners);var canvas=rt.GetComponentInParent<Canvas>();var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        return corners.All(v=>{var point=RectTransformUtility.WorldToScreenPoint(cam,v);return point.x>=-1 && point.y>=-1 && point.x<=Screen.width+1 && point.y<=Screen.height+1;});
    }
    void RestoreAudio(){if(audioSaved && AudioService.Instance!=null){AudioService.Instance.BgmMute=bgm;AudioService.Instance.SfxMute=sfx;audioSaved=false;}}
    protected override void OnDestroy(){RestoreAudio();base.OnDestroy();}
    sealed class TestAd:IReviveAdProvider{public int requests;public Action<bool> callback;public void RequestReviveAd(Action<bool> complete){requests++;callback=complete;}}
}
#endif
