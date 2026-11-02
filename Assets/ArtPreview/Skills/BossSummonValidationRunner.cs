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

[DefaultExecutionOrder(1000)]
public sealed class BossSummonValidationRunner : UIValidationRunnerBase
{
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    bool Baseline=>SessionState.GetBool("BossSummonBaseline",false);
    protected override string OutputFolder {get {var custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return custom==""?"BossSummonV1/"+(Baseline?"Before":"After"):custom;}}
    protected override string ValidationNote=>"Real Game scene, table reserve rows, enabled BossBrain and real SummonModule / SpawnerWaves. Numeric cases hold newborns still after recording their original position; final video restores actual movement. Memory-backed save; real-time captures are not retimed. No revive art redesign or WeChat device-performance claim.";
    readonly Vector2 center=new Vector2(15,15);
    readonly List<GameObject> owners=new List<GameObject>();
    readonly List<Birth> births=new List<Birth>();
    readonly List<object> observations=new List<object>();
    readonly HashSet<int> seen=new HashSet<int>();
    GameObject prefab;EnemyCatalog catalog;SpawnerWaves spawner;Transform player;PlayerHealth health;
    bool observing,saved,originalBackground,holdNewborns=true;int matchedBirths;float castAt,originalScale;UnityEngine.Random.State originalRandom;
    [Serializable]public sealed class Birth { public int id;public float x,y,hp,speed,attack,defense,time; }
    void Awake(){originalBackground=SessionState.GetBool("BossSummonValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    object Read(object o,string name)
    {
        if(o==null)return null;var type=o.GetType();var p=type.GetProperty(name,Members);if(p!=null)return p.GetValue(o,null);
        for(;type!=null;type=type.BaseType){var f=type.GetField(name,Members);if(f!=null)return f.GetValue(o);}return null;
    }
    object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Members).Invoke(o,args);
    List<BossSkillModule> Modules(GameObject go)=>(List<BossSkillModule>)Read(go.GetComponent<BossBrain>(),"_modules");
    SummonModule Summon(GameObject go){foreach(var mod in Modules(go))mod.cooldown=9999;return Modules(go).OfType<SummonModule>().Single();}
    Component View(GameObject go)=>go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null&&c.GetType().Name=="BossSummonPresentation");
    int Portals(GameObject go)=>View(go)==null?0:(int)Read(View(go),"ActiveBirthCount");
    void LateUpdate()
    {
        if(!observing)return;
        foreach(var enemy in FindObjectsOfType<EnemyBase>())
        {
            if(enemy.GetComponent<BossBrain>()!=null||!seen.Add(enemy.GetInstanceID()))continue;
            births.Add(new Birth{id=enemy.EnemyId,x=enemy.transform.position.x,y=enemy.transform.position.y,hp=enemy.MaxHp,speed=enemy.MoveSpeed,attack=enemy.ContactDamage,defense=(float)Read(enemy,"defense"),time=Time.time-castAt});
            if(!Baseline&&owners.Any(o=>o!=null&&View(o)!=null&&(bool)Call(View(o),"HasBirthAt",enemy.transform.position,.005f)))matchedBirths++;
            var ai=enemy.GetComponent<EnemyAI>();if(ai!=null)ai.enabled=!holdNewborns;
            var body=enemy.GetComponent<Rigidbody2D>();if(body!=null){body.velocity=Vector2.zero;body.bodyType=holdNewborns?RigidbodyType2D.Kinematic:RigidbodyType2D.Dynamic;}
        }
    }
    void Clear()
    {
        observing=false;if(spawner!=null)spawner.StopAllCoroutines();
        foreach(var enemy in FindObjectsOfType<EnemyBase>()){SpawnLimiter.Instance?.Unregister("Enemy",enemy.gameObject);GameObjectPool.Release(enemy.gameObject);}
        foreach(var bullet in FindObjectsOfType<BossBullet>())GameObjectPool.Release(bullet.gameObject);
        owners.Clear();births.Clear();seen.Clear();
    }
    IList Reserve(int level)
    {
        SelectedLevelContext.Set(level>=300?3:level>=200?2:1,level);
        var rows=(IList)Call(spawner,"BuildTableWavesForLevel",level);var list=(IList)Activator.CreateInstance(rows.GetType());
        foreach(var row in rows)if((int)Read(row,"wave")==0)list.Add(row);
        var reserves=(IDictionary)Read(spawner,"_reserveWaves");reserves.Clear();if(list.Count>0)reserves.Add(0,list);return list;
    }
    GameObject Owner(int id)
    {
        var go=GameObjectPool.Get(prefab,center+Vector2.up*3,Quaternion.identity);owners.Add(go);
        var brain=go.GetComponent<BossBrain>();brain.enabled=true;brain.StopAllCoroutines();brain.IsBusy=false;
        EnemyDefinition definition;catalog.TryGet(id,out definition);var enemy=go.GetComponent<EnemyBase>();enemy.preventPoolDeath=false;enemy.ShowFromRevive();enemy.InitFromDefinition(definition);enemy.ApplyTableStats(25,10000,1.5f);
        go.GetComponentInChildren<EnemyWordLabel>().SetWord(definition.enemyName);go.GetComponent<EnemyAI>().enabled=false;
        var body=go.GetComponent<Rigidbody2D>();body.position=go.transform.position;body.velocity=Vector2.zero;body.gravityScale=0;
        player.position=center+Vector2.down*3;health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();
        Camera.main.orthographicSize=16;Camera.main.transform.position=new Vector3(center.x,center.y,-10);Physics2D.SyncTransforms();return go;
    }
    void Observe(){births.Clear();seen.Clear();matchedBirths=0;castAt=Time.time;observing=true;}
    IEnumerator Wait(Func<bool> condition,float seconds=6){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    IEnumerator Snap(string name){float previous=Time.timeScale;Time.timeScale=0;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=previous;}
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;Time.timeScale=1;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,105);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        spawner=FindObjectOfType<SpawnerWaves>();spawner.StopAllCoroutines();spawner.autoTriggerWavesOnStart=false;spawner.enableNormalSpawn=false;spawner.debugLogs=false;
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();Clear();yield return null;CombatTargetRegistry.ClearAll();
        var skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.enabled=false;skills.ClearAllSkills();health=player.GetComponent<PlayerHealth>();player.GetComponent<PlayerController>().enabled=false;
        catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Boss/boss_common.prefab");spawner.catalog=catalog;spawner.target=player;
        Record("Fixture loads real Game, Boss, Spawner and catalog",spawner!=null&&prefab!=null&&catalog!=null&&health!=null);
        yield return TableCase(105,31051,10,"chapter1");yield return TableCase(204,32041,12,"chapter2");yield return TableCase(304,33041,15,"chapter3");
        File.WriteAllText(Path.Combine(output,"mechanics-observation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(observations,Newtonsoft.Json.Formatting.Indented));
        if(!Baseline)yield return AdditionalChecks();
        yield return Motion();Clear();yield return null;yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle exit removes all summoned enemies and projectiles",FindObjectsOfType<EnemyBase>().Length==0&&FindObjectsOfType<BossBullet>().Length==0);
        Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator TableCase(int level,int id,int count,string name)
    {
        Clear();yield return null;var rows=Reserve(level);var owner=Owner(id);yield return null;var skill=Summon(owner);var row=rows[0];
        Record(name+": actual table interval and initial delay remain",Mathf.Approximately(skill.interval,level<200?12:15)&&Mathf.Approximately(skill.firstDelayMul,.5f)&&Mathf.Approximately((float)Read(skill,"_chargeTime"),.45f));
        int waveEvents=0;Action<BattleWaveChangedEvent> listener=e=>waveEvents++;EventBus.Subscribe(listener);
        UnityEngine.Random.InitState(9751+level);Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.22f);
        Record(name+": no spawn before original charge completes",births.Count==0&&owner.GetComponent<BossBrain>().IsBusy);yield return Snap(name+"-charge");
        if(!Baseline)Record(name+": actual Boss charge has visible geometry",View(owner)!=null&&(bool)Read(View(owner),"HasVisibleGeometry"));
        yield return new WaitForSeconds(.4f);yield return Snap(name+"-birth");
        if(!Baseline)Record(name+": successful real births have portals",births.Count>0&&Portals(owner)>0);
        yield return Wait(()=>births.Count>=count);observing=false;EventBus.Unsubscribe(listener);
        Record(name+": exact table reserve count spawns",births.Count==count);
        Record(name+": attack HP speed and defense stay table-driven",births.All(b=>Mathf.Approximately(b.hp,(int)Read(row,"maxHp"))&&Mathf.Approximately(b.attack,(int)Read(row,"attack"))&&Mathf.Approximately(b.speed,(float)Read(row,"moveSpeed"))&&Mathf.Approximately(b.defense,(int)Read(row,"defense"))));
        EnemyDefinition requested;int requestedId=(int)Read(row,"monsterId");int actualId=catalog.TryGet(requestedId,out requested)&&requested!=null?requested.id:spawner.enemyPrefab.GetComponent<EnemyBase>().EnemyId;
        Record(name+": current catalog or fallback identity is preserved",births.All(b=>b.id==actualId),"Requested="+requestedId+" actual="+actualId);
        Record(name+": release is not delayed and wave counter stays unchanged",births.Count>0&&births[0].time>=.43f&&births[0].time<.65f&&waveEvents==0&&!owner.GetComponent<BossBrain>().IsBusy);
        var random=JsonUtility.ToJson(UnityEngine.Random.state);
        observations.Add(new{caseName=name,level,id,births=births.ToArray(),randomState=random});
        if(!Baseline)
        {
            var prior=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("ArtOutput/UI/BossSummonV1/Before/mechanics-observation.json")).First(t=>(string)t["caseName"]==name);
            var original=prior["births"].ToObject<Birth[]>();
            Record(name+": original RNG state and precise spawn positions preserved",random==(string)prior["randomState"]&&births.Count==original.Length&&births.Select((b,i)=>Vector2.Distance(new Vector2(b.x,b.y),new Vector2(original[i].x,original[i].y))<.001f).All(v=>v));
            Record(name+": every portal matches the actual final birth position",matchedBirths==births.Count&&(int)Read(View(owner),"TotalBirthFeedback")==count);
            yield return new WaitForSeconds(.8f);Record(name+": portals retire without leaving emitters",Portals(owner)==0&&!(bool)Read(View(owner),"HasVisibleGeometry"));
        }
    }
    IEnumerator AdditionalChecks()
    {
        Clear();yield return null;Reserve(105);var owner=Owner(31051);yield return null;var skill=Summon(owner);Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.15f);
        Time.timeScale=0;var mesh=View(owner).GetComponent<MeshFilter>().sharedMesh;yield return new WaitForSecondsRealtime(.2f);
        Record("Pause freezes the original scaled-time cast before release",births.Count==0&&owner.GetComponent<BossBrain>().IsBusy&&(int)Read(View(owner),"Phase")==1);
        Time.timeScale=1;yield return Wait(()=>births.Count>=2);Time.timeScale=0;int before=births.Count;yield return new WaitForSecondsRealtime(.4f);
        Record("Released wave pause follows the original Spawner clock setting",spawner.useRealtimeForWaves?births.Count>before:births.Count==before,"useRealtimeForWaves="+spawner.useRealtimeForWaves);
        Time.timeScale=1;yield return Wait(()=>births.Count==10);Record("Pause and resume keep all ten actual births",births.Count==10&&matchedBirths==10);
        foreach(string mode in new[]{"disable","pool","death","revive","rebuild"})
        {
            Clear();yield return null;Reserve(105);owner=Owner(mode=="revive"?32082:31051);yield return null;skill=Summon(owner);Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.15f);
            if(mode=="disable")owner.SetActive(false);
            else if(mode=="pool")GameObjectPool.Release(owner);
            else if(mode=="rebuild")owner.GetComponent<BossBrain>().OnEnemyDataReady();
            else owner.GetComponent<EnemyBase>().TakeDamage(20000);
            yield return new WaitForSeconds(.5f);
            Record(mode+": interrupted real charge cannot create delayed monsters",births.Count==0&&!owner.GetComponent<BossBrain>().IsBusy);
            Record(mode+": interrupted charge clears its seal and birth marks",View(owner)!=null&&!(bool)Read(View(owner),"HasVisibleGeometry")&&Portals(owner)==0);
            if(mode=="disable")owner.SetActive(true);
            if(mode=="revive")
            {
                yield return new WaitForSeconds(1.7f);skill=Summon(owner);skill.cooldown=0;
                Record("Actual old revive sequence still recovers and permits a future summon",owner.GetComponent<EnemyBase>().Hp>0&&skill.CanTrigger());
            }
        }
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);((IDictionary)Read(spawner,"_reserveWaves")).Clear();Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.9f);
        Record("Missing reserve group produces no fake portals or stuck busy state",births.Count==0&&Portals(owner)==0&&!(bool)Read(View(owner),"HasVisibleGeometry")&&!owner.GetComponent<BossBrain>().IsBusy);
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);var savedCatalog=spawner.catalog;var savedPrefab=spawner.enemyPrefab;spawner.catalog=null;spawner.enemyPrefab=null;
        Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(1.9f);spawner.catalog=savedCatalog;spawner.enemyPrefab=savedPrefab;
        Record("Failed actual prefab resolution emits no successful-birth feedback",births.Count==0&&(int)Read(View(owner),"TotalBirthFeedback")==0&&Portals(owner)==0);
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);spawner.enabled=false;
        Record("Inactive Spawner cannot start a new charge",!skill.CanTrigger());skill.Execute();yield return new WaitForSeconds(.55f);Record("Inactive Spawner leaves no charge geometry",!(bool)Read(View(owner),"HasVisibleGeometry"));spawner.enabled=true;spawner.StopAllCoroutines();
        var limiter=SpawnLimiter.Instance;var enemyLimit=limiter.configs.First(c=>c.key=="Enemy");var cosmetic=limiter.configs.First(c=>c.key==CombatVfxSpawner.SpawnLimiterKey);
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);int max=enemyLimit.maxAlive;bool recycle=enemyLimit.recycleOldest;enemyLimit.maxAlive=0;enemyLimit.recycleOldest=false;
        Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.7f);Record("Actual enemy cap creates no speculative birth markers",births.Count==0&&Portals(owner)==0&&(int)Read(View(owner),"TotalBirthFeedback")==0);
        enemyLimit.maxAlive=max;enemyLimit.recycleOldest=recycle;yield return Wait(()=>births.Count==10);Record("Actual delayed enemy-cap retries each get a matching portal",births.Count==10&&matchedBirths==10);
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);max=cosmetic.maxAlive;recycle=cosmetic.recycleOldest;cosmetic.maxAlive=0;cosmetic.recycleOldest=false;
        Observe();skill.Execute();skill.cooldown=9999;yield return Wait(()=>births.Count==10);cosmetic.maxAlive=max;cosmetic.recycleOldest=recycle;
        Record("Cosmetic saturation cannot suppress real birth identification",births.Count==10&&matchedBirths==10&&(int)Read(View(owner),"TotalBirthFeedback")==10);
        Record("Summoned monsters remain opaque, active and collidable",FindObjectsOfType<EnemyBase>().Where(e=>e.GetComponent<BossBrain>()==null).All(e=>e.GetComponent<Collider2D>().enabled&&e.GetComponentsInChildren<TMP_Text>().All(t=>t.color.a>.99f)));
        var latest=(Vector3)Read(View(owner),"LastBirthPosition");owner.transform.position+=Vector3.right*2;owner.GetComponent<Rigidbody2D>().position=owner.transform.position;Call(View(owner),"RenderNow");
        Record("Birth portals stay at the actual world point when the caster moves",(bool)Call(View(owner),"HasBirthAt",latest,.005f));
        Clear();yield return null;var ordinaryRows=Reserve(105);owner=Owner(31051);yield return null;Summon(owner);Observe();spawner.StartCoroutine((IEnumerator)Call(spawner,"TableWaveGroupRoutine",ordinaryRows,1,1,false));yield return Wait(()=>births.Count==10);
        Record("Ordinary four-argument wave entry spawns without summon presentation",births.Count==10&&(int)Read(View(owner),"TotalBirthFeedback")==0&&Portals(owner)==0);
        Clear();yield return null;var rows=Reserve(105);object custom=rows[0];custom.GetType().GetField("timeStart").SetValue(custom,1);custom.GetType().GetField("totalMonster").SetValue(custom,2);rows[0]=custom;
        owner=Owner(31051);yield return null;skill=Summon(owner);Observe();skill.Execute();skill.cooldown=9999;yield return new WaitForSeconds(.6f);GameObjectPool.Release(owner);var reused=Owner(31051);yield return null;Summon(reused);
        yield return Wait(()=>births.Count==2,4);Record("Already released reserve scheduling survives owner pool return",births.Count==2);
        Record("Old delayed callbacks cannot attach portals to the reused Boss lease",owner==reused&&(int)Read(View(reused),"TotalBirthFeedback")==0&&Portals(reused)==0);
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);var second=Owner(31051);second.transform.position+=Vector3.right*4;second.GetComponent<Rigidbody2D>().position=second.transform.position;yield return null;var secondSkill=Summon(second);
        Observe();skill.Execute();skill.cooldown=9999;secondSkill.Execute();secondSkill.cooldown=9999;yield return Wait(()=>births.Count==20);
        Record("Two real casters keep both full reserve groups and separate birth ownership",births.Count==20&&(int)Read(View(owner),"TotalBirthFeedback")==10&&(int)Read(View(second),"TotalBirthFeedback")==10&&matchedBirths==20);
        Record("Two casters share the material but own separate reusable meshes",View(owner).GetComponent<MeshRenderer>().sharedMaterial==View(second).GetComponent<MeshRenderer>().sharedMaterial&&View(owner).GetComponent<MeshFilter>().sharedMesh!=View(second).GetComponent<MeshFilter>().sharedMesh);yield return Snap("two-casters");
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);var arena=BossArenaLock.FindInScene();arena.PrepareBossWave();owner.AddComponent<LastWaveBossMarker>();arena.RegisterBoss(owner.GetComponent<EnemyBase>());
        Observe();skill.Execute();skill.cooldown=9999;yield return Wait(()=>births.Count==10);
        Record("Arena-constrained actual spawn points match the birth portals",births.Count==10&&matchedBirths==10&&births.All(b=>Vector2.Distance(new Vector2(b.x,b.y),arena.ClampInside(new Vector2(b.x,b.y)))<.01f));yield return Snap("arena-births");Call(arena,"UnlockArena");Destroy(owner.GetComponent<LastWaveBossMarker>());yield return null;
        Clear();yield return null;Reserve(105);owner=Owner(31071);yield return null;skill=Summon(owner);Observe();skill.Execute();skill.cooldown=9999;yield return Wait(()=>births.Count>=2);
        var blade=Modules(owner).OfType<BladeBurstModule>().Single();blade.cooldown=0;yield return Wait(()=>owner.GetComponentInChildren<BossBladeBurstPresentation>().MarkerCount>0);
        Record("Summon release allows the next real blade warning while births continue",owner.GetComponentInChildren<BossBladeBurstPresentation>().MarkerCount==8&&Portals(owner)>0);yield return Snap("combo-warning");yield return new WaitForSeconds(.6f);
        Record("Combination attacks keep real opaque enemy projectiles",FindObjectsOfType<StraightBullet>().Length>=8&&FindObjectsOfType<StraightBullet>().All(b=>b.GetComponentsInChildren<SpriteRenderer>().All(r=>r.color.a>.99f)));
        Clear();yield return null;Reserve(105);owner=Owner(31051);yield return null;skill=Summon(owner);var sourceMesh=View(owner).GetComponent<MeshFilter>().sharedMesh;Observe();skill.Execute();skill.cooldown=9999;yield return Wait(()=>births.Count==10);yield return new WaitForSeconds(.8f);
        observing=false;foreach(var minion in FindObjectsOfType<EnemyBase>().Where(e=>e.GetComponent<BossBrain>()==null)){SpawnLimiter.Instance?.Unregister("Enemy",minion.gameObject);GameObjectPool.Release(minion.gameObject);}yield return null;
        Observe();skill.Execute();skill.cooldown=9999;yield return Wait(()=>births.Count==10);Record("Repeated real casts reuse one mesh and spawn no per-birth GameObjects",sourceMesh==View(owner).GetComponent<MeshFilter>().sharedMesh&&View(owner).transform.childCount==0&&(int)Read(View(owner),"TotalBirthFeedback")==10);
    }
    IEnumerator Motion()
    {
        Clear();yield return null;Reserve(105);changeResolution?.Invoke(540,960);yield return new WaitForSecondsRealtime(.2f);var owner=Owner(31051);yield return null;var skill=Summon(owner);
        var folder=Path.Combine(output,"summon-motion");Directory.CreateDirectory(Path.Combine(folder,"frames"));var times=new List<float>();UnityEngine.Random.InitState(9856);holdNewborns=false;Observe();float start=Time.realtimeSinceStartup;skill.Execute();skill.cooldown=9999;int peak=0;
        while(Time.realtimeSinceStartup-start<3.2f)
        {
            peak=Mathf.Max(peak,Portals(owner));yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frames","frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.06f);
        }
        Record("Real-time capture includes the actual ten reserve births",births.Count==10&&times.Count>15);
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");
        File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,frames=times.Count,elapsed=times.LastOrDefault(),births=births.Count,peakPortals=peak,enemyMovementEnabled=true,timeScale=1,retimed=false}));holdNewborns=true;changeResolution?.Invoke(1080,1920);
    }
    protected override void OnDestroy(){if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}Application.runInBackground=originalBackground;base.OnDestroy();}
}
#endif
