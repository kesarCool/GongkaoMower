#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BossCloneValidationRunner : UIValidationRunnerBase
{
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    bool Baseline=>SessionState.GetBool("BossCloneBaseline",false);
    protected override string OutputFolder {get {var custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return custom==""?"BossCloneV1/"+(Baseline?"Before":"After"):custom;}}
    protected override string ValidationNote=>"Actual Game scene and CloneModule.Instantiate flow. Clone BossBrain.Start and inherited modules stay enabled. Memory-backed save. Before reproduces old inheritance/pool issues; after verifies the repaired contracts. Capture timestamps use timeScale 1. No summon/revive visual redesign or WeChat device-performance claim.";
    readonly Vector2 center=new Vector2(15,15);
    readonly List<GameObject> owners=new List<GameObject>();
    readonly List<object> snapshots=new List<object>();
    GameObject prefab;EnemyCatalog catalog;Transform player;PlayerHealth health;
    UnityEngine.Random.State originalRandom;float originalScale;bool saved,originalBackground;
    void Awake(){originalBackground=SessionState.GetBool("BossCloneValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    object Read(object o,string name){if(o==null)return null;var type=o.GetType();var p=type.GetProperty(name,Members);if(p!=null)return p.GetValue(o,null);for(;type!=null;type=type.BaseType){var f=type.GetField(name,Members);if(f!=null)return f.GetValue(o);}return null;}
    void Call(object o,string name,params object[] args){var m=o?.GetType().GetMethod(name,Members);if(m==null)throw new MissingMethodException(name);m.Invoke(o,args);}
    Component View(GameObject go)=>go==null?null:go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null&&c.GetType().Name=="BossClonePresentation");
    List<BossSkillModule> Modules(GameObject go)=>(List<BossSkillModule>)Read(go.GetComponent<BossBrain>(),"_modules");
    string[] Types(GameObject go)=>go.GetComponent<BossBrain>()==null?new string[0]:Modules(go).Select(m=>m.moduleType).OrderBy(s=>s).ToArray();
    CloneMarker[] Clones()=>FindObjectsOfType<CloneMarker>();
    void Clear()
    {
        foreach(var owner in owners)if(owner!=null&&owner.activeSelf)GameObjectPool.Release(owner);owners.Clear();
        foreach(var clone in Clones())Destroy(clone.gameObject);
        foreach(var bullet in FindObjectsOfType<BossBullet>())GameObjectPool.Release(bullet.gameObject);
    }
    GameObject Owner(int id)
    {
        var go=GameObjectPool.Get(prefab,center,Quaternion.identity);owners.Add(go);var brain=go.GetComponent<BossBrain>();brain.enabled=true;brain.StopAllCoroutines();brain.IsBusy=false;
        EnemyDefinition definition;catalog.TryGet(id,out definition);var enemy=go.GetComponent<EnemyBase>();enemy.ShowFromRevive();enemy.preventPoolDeath=false;enemy.InitFromDefinition(definition);enemy.ApplyTableStats(25,10000,1.5f);
        var label=go.GetComponentInChildren<EnemyWordLabel>();label.SetWord(definition.enemyName);
        var ai=go.GetComponent<EnemyAI>();if(ai!=null){ai.enabled=true;ai.moveSpeed=0;}
        var body=go.GetComponent<Rigidbody2D>();body.position=center;body.velocity=Vector2.zero;body.gravityScale=0;
        player.position=center+new Vector2(0,-6);health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();
        Camera.main.orthographicSize=13;Camera.main.transform.position=new Vector3(center.x,center.y-1,-10);Physics2D.SyncTransforms();return go;
    }
    IEnumerator Snap(string name){var scale=Time.timeScale;Time.timeScale=0;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=scale;}
    IEnumerator Wait(Func<bool> condition,float seconds=6){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    CloneModule CloneSkill(GameObject owner){foreach(var mod in Modules(owner))mod.cooldown=9999;return Modules(owner).OfType<CloneModule>().Single();}
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;Time.timeScale=1;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();foreach(var enemy in FindObjectsOfType<EnemyBase>())Destroy(enemy.gameObject);yield return null;CombatTargetRegistry.ClearAll();
        var skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.enabled=false;skills.ClearAllSkills();health=player.GetComponent<PlayerHealth>();player.GetComponent<PlayerController>().enabled=false;
        catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Boss/boss_common.prefab");
        Record("Fixture loads real Boss prefab, catalog and player",prefab!=null&&catalog!=null&&health!=null);
        yield return Inheritance(31041,"dash","dash");yield return Inheritance(32011,"bladeBurst","blade");yield return Inheritance(31082,"homingKnife","knife");
        if(!Baseline)
        {
            yield return Inheritance(32012,"dash","mixed-dash");
            yield return Inheritance(32032,"bladeBurst","mixed-blade");
            yield return LifecycleChecks();
            yield return HeadCrestChecks();
        }
        File.WriteAllText(Path.Combine(output,"inheritance-snapshots.json"),Newtonsoft.Json.JsonConvert.SerializeObject(snapshots,Newtonsoft.Json.Formatting.Indented));
        yield return Motion("clone-motion",32011,Baseline?3:17);
        Clear();yield return null;yield return new WaitForSeconds(.4f);yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle exit removes all real clones and leased boss projectiles",Clones().Length==0&&FindObjectsOfType<BossBullet>().Length==0);
        Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator Inheritance(int id,string expected,string name)
    {
        Clear();yield return null;var owner=Owner(id);yield return null;var module=CloneSkill(owner);
        Record(name+": original table clone parameters remain",Mathf.Approximately(module.interval,10)&&Mathf.Approximately((float)Read(module,"_hpPercent"),.35f)&&(int)Read(module,"_cloneCount")==2&&Mathf.Approximately((float)Read(module,"_lifetime"),15)&&Mathf.Approximately((float)Read(module,"_chargeTime"),.35f));
        Time.timeScale=.25f;module.Execute();yield return new WaitForSeconds(.12f);Record(name+": no clone or early movement during original charge",Clones().Length==0&&owner.GetComponent<BossBrain>().IsBusy);yield return Snap(name+"-charge");
        yield return new WaitForSeconds(.25f);yield return null;yield return null;var clones=Clones();
        Record(name+": actual module creates two real clones at 35 percent HP",clones.Length==2&&clones.All(c=>Mathf.Approximately(c.GetComponent<EnemyBase>().MaxHp,3500)));
        var types=clones.Select(c=>Types(c.gameObject)).ToArray();bool whitelist=types.All(t=>t.SequenceEqual(new[]{expected}));
        Record(name+(Baseline?": old Start rebuild overwrites intended whitelist (reproduced)":": whitelist survives real Start"),Baseline?!whitelist:whitelist,string.Join(" / ",types.Select(t=>string.Join(",",t))));
        bool contaminated=clones.Any(c=>c.GetComponent<PooledObject>()!=null&&c.GetComponent<PooledObject>().sourcePrefabId!=0);
        Record(name+(Baseline?": old clones copy the owner pool identity (reproduced)":": clones cannot enter the owner pool"),Baseline?contaminated:!contaminated);
        Record(name+": clone has no final-boss marker",clones.All(c=>c.GetComponent<LastWaveBossMarker>()==null));
        if(!Baseline)
        {
            foreach(var clone in clones){clone.GetComponent<BossBrain>().OnEnemyDataReady();}
            Record(name+": whitelist survives a later data-ready rebuild",clones.All(c=>Types(c.gameObject).SequenceEqual(new[]{expected})));
            yield return null;var alpha=clones.SelectMany(c=>c.GetComponentsInChildren<TMP_Text>()).Select(t=>t.color.a).ToArray();
            Record(name+": actual TMP glyphs are translucent and readable",alpha.Length>0&&alpha.All(a=>a>.35f&&a<.8f));
            Record(name+": original and clone identity meshes are visible",View(owner)!=null&&clones.All(c=>View(c.gameObject)!=null));
        }
        snapshots.Add(new{caseName=name,id,expected,actual=types,poolContamination=contaminated,baseline=Baseline});yield return Snap(name+"-birth");Time.timeScale=1;Clear();yield return null;
    }
    IEnumerator LifecycleChecks()
    {
        yield return PolicyCase(32081,"*",new[]{"bladeBurst","homingKnife"},"wildcard");
        yield return PolicyCase(31041,"",new string[0],"blank");
        yield return PolicyCase(32022,"dash|HOMINGKNIFE|clone|revive",new[]{"dash","homingKnife"},"mixed-case");
        foreach(string mode in new[]{"disable","pool","hidden-death"})
        {
            Clear();yield return null;var owner=Owner(31041);yield return null;var module=CloneSkill(owner);Time.timeScale=.25f;module.Execute();yield return new WaitForSeconds(.10f);
            Record(mode+": cancellation starts during real clone preparation",Clones().Length==0&&owner.GetComponent<BossBrain>().IsBusy);
            if(mode=="disable")owner.SetActive(false);else if(mode=="pool")GameObjectPool.Release(owner);else{owner.GetComponent<EnemyBase>().preventPoolDeath=true;owner.GetComponent<EnemyBase>().TakeDamage(20000);}
            yield return new WaitForSeconds(.4f);Record(mode+": interrupted charge cannot create delayed clones",Clones().Length==0&&!owner.GetComponent<BossBrain>().IsBusy&&(int)Read(View(owner),"Phase")==0);
            if(mode=="disable"){owner.SetActive(true);GameObjectPool.Release(owner);}if(mode=="hidden-death"){owner.GetComponent<EnemyBase>().preventPoolDeath=false;owner.GetComponent<EnemyBase>().ShowFromRevive();GameObjectPool.Release(owner);}Time.timeScale=1;Clear();yield return null;
        }
        Clear();yield return null;var main=Owner(32011);yield return null;var skill=CloneSkill(main);skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);
        var group=Clones();Record("Real clones exist before pause",group.Length==2);
        float remaining=(float)Read(group[0],"RemainingSeconds");var positions=group.Select(c=>c.transform.position).ToArray();Time.timeScale=0;yield return new WaitForEndOfFrame();yield return new WaitForSecondsRealtime(.2f);
        Record("Pause freezes clone lifetime and actual movement",Mathf.Abs((float)Read(group[0],"RemainingSeconds")-remaining)<.001f&&positions.SequenceEqual(group.Select(c=>c.transform.position)));
        Time.timeScale=1;main.GetComponent<EnemyBase>().TakeDamage(20000);yield return null;yield return null;
        Record("True owner death clears every owned real clone",Clones().Length==0);Clear();yield return null;
        main=Owner(32011);yield return null;skill=CloneSkill(main);skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);Record("Owned clones exist before actual pool return",Clones().Length==2);
        GameObjectPool.Release(main);yield return null;yield return null;Record("Owner pool return clears active clone bodies",Clones().Length==0);
        var reused=GameObjectPool.Get(prefab,center+Vector2.right*6,Quaternion.identity);yield return new WaitForFixedUpdate();
        Record("Raw owner reuse never returns a CloneMarker or old inherited policy",reused==main&&reused.GetComponent<CloneMarker>()==null&&!reused.GetComponent<BossBrain>().IsBusy);GameObjectPool.Release(reused);Clear();yield return null;
        main=Owner(32011);yield return null;skill=CloneSkill(main);skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);var disabledClone=Clones().FirstOrDefault();
        Record("Real clone exists before its own disable",disabledClone!=null);if(disabledClone!=null)disabledClone.gameObject.SetActive(false);yield return null;yield return null;
        Record("Disabling an unpooled clone removes its body instead of leaking it inactive",disabledClone==null&&Clones().Length==1);Clear();yield return null;
        // Real old ReviveModule, deliberately registered after CloneModule's death listener.
        main=Owner(31041);yield return null;skill=CloneSkill(main);var revive=new ReviveModule();revive.Init("0.5,0.5,1",main.GetComponent<BossBrain>());skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);
        var identities=Clones().Select(c=>c.GetInstanceID()).OrderBy(i=>i).ToArray();main.GetComponent<EnemyBase>().TakeDamage(20000);yield return null;yield return null;
        Record("Later revive listener preserves existing clones through owner fake death",main.GetComponent<EnemyBase>().preventPoolDeath&&identities.SequenceEqual(Clones().Select(c=>c.GetInstanceID()).OrderBy(i=>i)));
        yield return new WaitForSeconds(1.25f);Record("Owner recovery keeps the same clone instances",main.GetComponent<EnemyBase>().Hp>0&&identities.SequenceEqual(Clones().Select(c=>c.GetInstanceID()).OrderBy(i=>i)));revive.OnBossDestroyed();Clear();yield return null;
        main=Owner(32011);yield return null;skill=CloneSkill(main);skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);int meshes=(int)Read(View(main),"RuntimeMeshCount");
        foreach(var clone in Clones())Call(clone,"Despawn",false);yield return null;skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.45f);
        Record("Repeated clone charges reuse their glyph meshes",meshes>1&&(int)Read(View(main),"RuntimeMeshCount")==meshes);
        var limiter=SpawnLimiter.Instance;var config=limiter!=null?limiter.configs.FirstOrDefault(c=>c!=null&&c.key==CombatVfxSpawner.SpawnLimiterKey):null;
        Record("Game provides its real cosmetic limiter",config!=null);
        if(config!=null){int alive=config.currentAlive,frame=config.spawnThisFrame;config.currentAlive=config.maxAlive;config.spawnThisFrame=Mathf.Max(config.spawnPerFrame,1);yield return null;Record("Cosmetic saturation preserves all clone identity marks",Clones().All(c=>(bool)Read(View(c.gameObject),"HasVisibleGeometry")));config.currentAlive=alive;config.spawnThisFrame=frame;}
        Clear();yield return null;
    }
    IEnumerator HeadCrestChecks()
    {
        Clear();yield return null;var owner=Owner(31041);yield return null;CloneSkill(owner);
        var crest=owner.GetComponentInChildren<BossHeadCrestPresentation>();
        Record("Head crest replaces the two projectile-shaped ornaments",crest!=null&&!owner.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="LeftHorn"||t.name=="RightHorn"));
        if(crest==null)yield break;
        var label=owner.GetComponentInChildren<EnemyWordLabel>();TextMeshPro text;label.TryGetWorldTextForShatter(out text);
        foreach(var word in new[]{"影武者","战场老兵","霜刃统领"})
        {
            label.SetWord(word);text.ForceMeshUpdate();yield return null;crest.RenderNow();
            var bounds=crest.GetComponent<MeshRenderer>().bounds;
            Record("Head crest follows visible glyph top: "+word,crest.HasVisibleGeometry&&bounds.min.y>text.renderer.bounds.max.y-.05f&&bounds.size.x<1);
        }
        label.SetWord("影武者");text.ForceMeshUpdate();yield return null;yield return Snap("head-crest-owner");
        var ownedMesh=crest.GetComponent<MeshFilter>().sharedMesh;
        var socket=new GameObject("FutureBodyHeadSocket");socket.transform.SetParent(owner.transform,false);socket.transform.localPosition=new Vector3(.4f,1.7f,0);
        text.renderer.enabled=false;crest.BindBody(socket.transform,null);crest.RenderNow();
        Record("Explicit animated head socket works with the TMP renderer hidden",crest.HasVisibleGeometry&&Vector2.Distance(crest.HeadPosition,socket.transform.position)<.001f);
        socket.transform.localPosition+=new Vector3(.3f,.2f,0);crest.RenderNow();
        Record("Moving head sockets reuse the same compact mesh",Vector2.Distance(crest.HeadPosition,socket.transform.position)<.001f&&crest.GetComponent<MeshFilter>().sharedMesh==ownedMesh);
        socket.SetActive(false);crest.RenderNow();Record("Hidden animated head socket hides its crest",!crest.HasVisibleGeometry);
        crest.BindBody(null,null);text.renderer.enabled=true;Destroy(socket);yield return null;
        owner.GetComponent<EnemyBase>().HideForRevive();crest.RenderNow();Record("Revive hiding cannot leave a floating crown",!crest.HasVisibleGeometry);
        owner.GetComponent<EnemyBase>().ShowFromRevive();yield return null;crest.RenderNow();Record("Revive recovery restores the same head crest mesh",crest.HasVisibleGeometry&&crest.GetComponent<MeshFilter>().sharedMesh==ownedMesh);
        var skill=CloneSkill(owner);skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.5f);
        Record("Real clones replace the crown with paired hollow marks",Clones().Length==2&&Clones().All(c=>{var v=c.GetComponentInChildren<BossHeadCrestPresentation>();return v!=null&&v.IsClone&&v.HasVisibleGeometry;}));
        yield return Snap("head-crest-clones");Clear();yield return null;
    }
    IEnumerator PolicyCase(int id,string policy,string[] expected,string name)
    {
        Clear();yield return null;var owner=Owner(id);yield return null;foreach(var mod in Modules(owner))mod.cooldown=9999;
        var marker=owner.AddComponent<LastWaveBossMarker>();marker.isFinalBoss=true;
        var module=new CloneModule();module.Init("10,0.35,2,15,"+policy,owner.GetComponent<BossBrain>());module.Execute();module.cooldown=9999;yield return new WaitForSeconds(.45f);var clones=Clones();
        Record(name+": true Start builds only the requested permitted skills",clones.Length==2&&clones.All(c=>Types(c.gameObject).SequenceEqual(expected)),string.Join(" / ",clones.Select(c=>string.Join(",",Types(c.gameObject)))));
        Record(name+": no clone or revive module can initialize",clones.All(c=>!Types(c.gameObject).Contains("clone")&&!Types(c.gameObject).Contains("revive")&&!c.GetComponent<EnemyBase>().preventPoolDeath));
        Record(name+": copied final-boss markers are removed",clones.All(c=>c.GetComponent<LastWaveBossMarker>()==null));
        foreach(var clone in clones)clone.GetComponent<BossBrain>().OnEnemyDataReady();Record(name+": subsequent rebuild preserves policy",clones.All(c=>Types(c.gameObject).SequenceEqual(expected)));
        int bossesAlive=BattleVictoryBossTracker.BossesAlive,finalAlive=BattleVictoryBossTracker.FinalBossesAlive,reward=-1;var killed=clones[0];Action<EnemyDiedEvent> listener=e=>{if(e.enemy==killed.GetComponent<EnemyBase>())reward=e.rewardKillCount;};EventBus.Subscribe(listener);
        killed.GetComponent<EnemyBase>().TakeDamage(20000);EventBus.Unsubscribe(listener);yield return null;yield return null;
        Record(name+": clone death grants no kill reward or boss victory",reward==0&&BattleVictoryBossTracker.BossesAlive==bossesAlive&&BattleVictoryBossTracker.FinalBossesAlive==finalAlive);
        yield return new WaitForSeconds(.6f);Record(name+": killed clone cannot revive or enter the main pool",killed==null&&Clones().Length==1);
        module.OnOwnerDisposed();Destroy(marker);Clear();yield return null;
    }
    IEnumerator Motion(string name,int id,float seconds)
    {
        Clear();yield return null;changeResolution?.Invoke(540,960);yield return new WaitForSecondsRealtime(.2f);UnityEngine.Random.InitState(1387);var owner=Owner(id);yield return null;var module=CloneSkill(owner);Time.timeScale=1;
        var folder=Path.Combine(output,name);Directory.CreateDirectory(Path.Combine(folder,"frames"));var times=new List<float>();float began=Time.realtimeSinceStartup;bool second=false;int peak=0,peakDirections=0,peakBlades=0;bool opaque=true;module.Execute();module.cooldown=9999;
        while(Time.realtimeSinceStartup-began<seconds&&times.Count<230)
        {
            float elapsed=Time.realtimeSinceStartup-began;if(!Baseline&&!second&&elapsed>10){module.Execute();module.cooldown=9999;second=true;}
            player.position=center+new Vector2(Mathf.Sin(elapsed*.4f)*5,-5);Physics2D.SyncTransforms();peak=Mathf.Max(peak,Clones().Length);
            if(!Baseline)
            {
                peakDirections=Mathf.Max(peakDirections,Clones().Sum(c=>{var v=c.GetComponentInChildren<BossBladeBurstPresentation>();return v!=null?v.MarkerCount:0;}));
                var blades=FindObjectsOfType<StraightBullet>();peakBlades=Mathf.Max(peakBlades,blades.Length);
                opaque&=blades.All(b=>b.GetComponentsInChildren<SpriteRenderer>().All(r=>r.color.a>.99f));
            }
            yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-began);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frames","frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.09f);
        }
        Record(name+": capture contains actual CloneModule births",times.Count>=15&&peak>=2);
        if(!Baseline){Record("Two real clone rounds overlap without recursive children",peak==4);Record("First clone round expires after original fifteen seconds",Clones().Length==2);Record("Inherited attacks keep complete warnings and opaque enemy blades",peakDirections>=24&&peakBlades>=20&&opaque,"Directions="+peakDirections+" blades="+peakBlades+" opaque="+opaque);}
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,frames=times.Count,elapsed=times.LastOrDefault(),peakClones=peak,peakDirections,peakBlades,opaque,timeScale=1,retimed=false}));Clear();changeResolution?.Invoke(1080,1920);yield return null;
    }
    protected override void OnDestroy(){if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}Application.runInBackground=originalBackground;base.OnDestroy();}
}
#endif
