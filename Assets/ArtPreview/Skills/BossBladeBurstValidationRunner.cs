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

/// <summary>Real BossBrain table modules and physics; the save fixture is owned by UIValidationRunnerBase.</summary>
public sealed class BossBladeBurstValidationRunner : UIValidationRunnerBase
{
    const string BossPath="Assets/Prefab/Enemy/Boss/boss_common.prefab";
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    bool Baseline=>SessionState.GetBool("BossBladeBurstBaseline",false);
    protected override string OutputFolder {get {string custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return string.IsNullOrEmpty(custom)?"BossBladeBurstV1/"+(Baseline?"Before":"After"):custom;}}
    protected override string ValidationNote=>"Real Game scene, real table-built BladeBurstModule, pooled StraightBullet and PlayerHealth collision. In-memory profile only. The 2/4 CloneMarker pressure cases are manually placed simultaneous casters, not a test or repair of CloneModule inheritance. Motion preserves actual capture timestamps at timeScale 1; this is not a device FPS or package-size test.";
    readonly List<GameObject> bosses=new List<GameObject>();
    readonly Vector2 origin=new Vector2(15,15);
    UnityEngine.Random.State originalRandom;float originalScale;bool originalBackground,saved;
    PlayerSkills skills;Transform player;PlayerHealth health;EnemyCatalog catalog;GameObject bossPrefab;
    readonly Dictionary<int,string> names=new Dictionary<int,string>{{31021,"刀削面圣"},{32011,"兵不厌诈"},{32021,"东山再起"}};

    void Awake()
    {
        originalBackground=SessionState.GetBool("BossBurstValidation.previousBackground",Application.runInBackground);
        Application.runInBackground=true;
    }
    BossBullet[] Bullets()=>FindObjectsOfType<BossBullet>().Where(b=>b is StraightBullet).ToArray();
    Component Presentation(GameObject go)=>go!=null?go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null && c.GetType().Name=="BossBladeBurstPresentation"):null;
    Component ProjectilePresentation(GameObject go)=>go!=null?go.GetComponent("BossBladeProjectilePresentation"):null;
    object Read(Component component,string member)
    {
        if(component==null)return null;
        Type type=component.GetType();var property=type.GetProperty(member,Members);
        return property!=null?property.GetValue(component,null):type.GetField(member,Members)?.GetValue(component);
    }
    int Int(Component component,string member)=>Read(component,member) is int?(int)Read(component,member):-1;
    bool Bool(Component component,string member)=>Read(component,member) is bool && (bool)Read(component,member);
    void Invoke(Component component,string member,params object[] args)
    {
        if(component==null)throw new InvalidOperationException("Missing presentation for "+member);
        var method=component.GetType().GetMethod(member,Members);
        if(method==null)throw new MissingMethodException(component.GetType().Name,member);
        method.Invoke(component,args);
    }
    BladeBurstModule Module(GameObject go)
    {
        var brain=go.GetComponent<BossBrain>();
        var modules=(List<BossSkillModule>)typeof(BossBrain).GetField("_modules",Members).GetValue(brain);
        return modules.OfType<BladeBurstModule>().FirstOrDefault();
    }
    void ReleaseBullets(){foreach(var b in Bullets())GameObjectPool.Release(b.gameObject);}
    void ReleaseBosses()
    {
        foreach(var go in bosses.Distinct())if(go!=null && go.activeSelf)GameObjectPool.Release(go);
        bosses.Clear();ReleaseBullets();
    }
    void ResetPlayer(Vector2 center)
    {
        ReleaseBullets();player.position=center+Vector2.right*3.7f;
        var body=player.GetComponent<Rigidbody2D>();body.velocity=Vector2.zero;body.gravityScale=0;
        health.OnPreDamage=null;health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();
        Camera.main.transform.position=new Vector3(center.x,center.y-1,-10);Physics2D.SyncTransforms();
    }
    GameObject CreateBoss(int monsterId,Vector2 position,bool clone=false)
    {
        var go=GameObjectPool.Get(bossPrefab,position,Quaternion.identity);bosses.Remove(go);bosses.Add(go);
        go.name="BladeBurstValidation_"+monsterId+(clone?"_manualClone":"_main");
        var brain=go.GetComponent<BossBrain>();brain.enabled=false;brain.StopAllCoroutines();brain.IsBusy=false;
        foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string type=behaviour.GetType().Name;
            if(!(behaviour is EnemyBase) && !(behaviour is BossBrain) && !(behaviour is EnemyWordLabel) && !(behaviour is TMP_Text) && type!="BossBladeBurstPresentation")behaviour.enabled=false;
        }
        EnemyDefinition definition;
        if(catalog==null || !catalog.TryGet(monsterId,out definition))definition=new EnemyDefinition{id=monsterId,enemyName=names[monsterId],prefab=bossPrefab};
        var enemy=go.GetComponent<EnemyBase>();enemy.preventPoolDeath=false;enemy.addKillToGameLayer=false;enemy.InitFromDefinition(definition);enemy.ApplyTableStats(25,10000,0);
        go.GetComponentInChildren<EnemyWordLabel>().SetWord(names[monsterId]);
        var body=go.GetComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.simulated=true;body.velocity=Vector2.zero;body.gravityScale=0;
        foreach(var col in go.GetComponentsInChildren<Collider2D>())col.enabled=true;
        if(clone && go.GetComponent<CloneMarker>()==null)go.AddComponent<CloneMarker>();
        if(!clone && go.GetComponent<CloneMarker>()!=null)Destroy(go.GetComponent<CloneMarker>());
        Physics2D.SyncTransforms();return go;
    }
    IEnumerator Snap(string name)
    {
        float previous=Time.timeScale;Time.timeScale=0;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=previous;
    }
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();foreach(var enemy in FindObjectsOfType<EnemyBase>())Destroy(enemy.gameObject);yield return null;CombatTargetRegistry.ClearAll();
        skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.enabled=false;skills.ClearAllSkills();health=player.GetComponent<PlayerHealth>();
        var controller=player.GetComponent<PlayerController>();if(controller!=null)controller.enabled=false;
        catalog=FindObjectOfType<EnemyCatalog>();if(catalog==null)catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);Camera.main.orthographicSize=9.5f;Time.timeScale=1;
        Record("Fixture uses the actual shared boss prefab, player and enemy catalog",bossPrefab!=null && health!=null && catalog!=null);
        yield return SingleBurst(31021,8,Vector2.zero,"eight");
        yield return SingleBurst(32011,12,Vector2.zero,"twelve");
        yield return SingleBurst(32021,16,new Vector2(-12,3),"sixteen-grass");
        yield return Motion("eight-motion",31021,8,0,Vector2.zero);
        yield return Motion("twelve-motion",32011,12,0,Vector2.zero);
        yield return Motion("sixteen-motion",32021,16,0,new Vector2(-12,3));
        if(!Baseline)
        {
            yield return PauseAndCancellation();yield return LimiterAndPoolChecks();
            yield return Motion("two-clones-motion",32011,12,2,Vector2.zero);
            yield return Motion("four-clones-motion",32021,16,4,new Vector2(-12,3));
            yield return EdgeLifetime();
        }
        ReleaseBosses();yield return new WaitForSeconds(.4f);yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle scene exit removes all leased enemy blades and charge presenters",Bullets().Length==0 && FindObjectsOfType<MonoBehaviour>().All(b=>b.GetType().Name!="BossBladeBurstPresentation" && b.GetType().Name!="BossBladeProjectilePresentation"));
        Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator SingleBurst(int id,int count,Vector2 shift,string name)
    {
        ReleaseBosses();Vector2 center=origin+shift;ResetPlayer(center);var go=CreateBoss(id,center);yield return null;
        var module=Module(go);Record(name+": real Monster table builds a bladeBurst module",module!=null);
        if(module==null)yield break;
        float initialCooldown=module.cooldown;module.Tick(initialCooldown-.02f);Record(name+": initial table delay does not trigger early",!module.CanTrigger());module.Tick(.021f);
        Record(name+": real module becomes ready after its configured delay",module.CanTrigger() && (Mathf.Approximately(module.interval,8) || Mathf.Approximately(module.interval,4)));
        Time.timeScale=.25f;var random=UnityEngine.Random.state;float started=Time.time;module.Execute();
        Record(name+": execution resets the original table cooldown",Mathf.Approximately(module.cooldown,module.interval));
        if(!Baseline)Record(name+": presentation and launch preparation do not consume combat RNG",random.Equals(UnityEngine.Random.state));
        yield return new WaitForSeconds(.20f);
        Record(name+": charge has no early projectile or damage",Bullets().Length==0 && health.Hp==10000 && go.GetComponent<BossBrain>().IsBusy && Time.time-started<.5f);
        if(!Baseline)
        {
            var presentation=Presentation(go);Record(name+": all configured directions have visible warning blades",Int(presentation,"Phase")==1 && Int(presentation,"MarkerCount")==count && Bool(presentation,"HasVisibleGeometry"));
            var text=go.GetComponentInChildren<TMP_Text>();text.ForceMeshUpdate();Record(name+": real boss text stays visible during charge",text.enabled && text.textInfo.characterCount>0 && text.GetComponent<Renderer>().enabled && text.color.a>.1f);
        }
        yield return Snap(name+"-charge");yield return new WaitForSeconds(.31f);yield return new WaitForFixedUpdate();
        var blades=Bullets();Record(name+": real burst launches the original "+count+" blades",blades.Length==count,"Actual="+blades.Length);
        Record(name+": speed, damage, lifetime and collider stay authored values",blades.Length==count && blades.All(b=>Mathf.Approximately(b.speed,7) && Mathf.Approximately(b.damage,25) && Mathf.Abs(b.lifetime-6f/7f)<.001f && Mathf.Approximately(b.GetComponent<CircleCollider2D>().radius,.4f)));
        bool directions=blades.Length==count;
        var observed=new List<int>();
        foreach(var blade in blades)
        {
            Vector2 direction=blade.GetComponent<Rigidbody2D>().velocity.normalized;
            float angle=Mathf.Repeat(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,360);int index=Mathf.RoundToInt(angle/(360f/count))%count;
            directions &= Mathf.Abs(Mathf.DeltaAngle(angle,index*360f/count))<.2f && Vector2.Dot(((Vector2)blade.transform.position-center).normalized,direction)>.999f;
            observed.Add(index);
        }
        Record(name+": evenly spaced radial launch matches warning directions",directions && observed.Distinct().Count()==count);
        Record(name+": bullets have not damaged the distant player at release",health.Hp==10000);
        if(!Baseline)Record(name+": every damaging blade has its dedicated visual presenter",blades.Length==count && blades.All(b=>ProjectilePresentation(b.gameObject)!=null));
        yield return new WaitForSeconds(.025f);yield return Snap(name+"-flight");Time.timeScale=1;
        float deadline=Time.realtimeSinceStartup+2;
        while(health.Hp==10000 && Time.realtimeSinceStartup<deadline)yield return null;
        Record(name+": actual player collision deals the original 25 damage",Mathf.Abs(health.Hp-9975)<.001f,"Hp="+health.Hp);
        yield return Snap(name+"-contact");yield return new WaitForSeconds(1);
        Record(name+": all original blades return by their authored lifetime",Bullets().Length==0);
        Record(name+": original .15 second recovery ends busy state",!go.GetComponent<BossBrain>().IsBusy);
        GameObjectPool.Release(go);yield return null;
    }
    IEnumerator Motion(string name,int id,int count,int clones,Vector2 shift)
    {
        changeResolution?.Invoke(540,960);yield return null;yield return new WaitForSecondsRealtime(.2f);
        ReleaseBosses();Vector2 center=origin+shift;ResetPlayer(center);var casters=new List<GameObject>{CreateBoss(id,center)};
        for(int i=0;i<clones;i++)
        {
            float angle=(90+i*360f/clones)*Mathf.Deg2Rad;casters.Add(CreateBoss(id,center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*3.2f,true));
        }
        if(clones>0){player.position=center+Vector2.down*7;Physics2D.SyncTransforms();}
        yield return null;health.ResetToFull();
        string folder=Path.Combine(output,name),frames=Path.Combine(folder,"frames");Directory.CreateDirectory(frames);var times=new List<float>();
        int maxBlades=0,maxMarkers=0,maxRenderers=0;float start=Time.realtimeSinceStartup;
        foreach(var caster in casters)Module(caster).Execute();
        while(Time.realtimeSinceStartup-start<1.85f && times.Count<70)
        {
            maxBlades=Mathf.Max(maxBlades,Bullets().Length);
            if(!Baseline){maxMarkers=Mathf.Max(maxMarkers,casters.Sum(c=>Mathf.Max(0,Int(Presentation(c),"MarkerCount"))));maxRenderers=Mathf.Max(maxRenderers,casters.Sum(c=>Mathf.Max(0,Int(Presentation(c),"RendererCount"))));}
            yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);
            ScreenCapture.CaptureScreenshot(Path.Combine(frames,"frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.045f);
        }
        Record(name+": real-time capture includes all simultaneous blade volleys",times.Count>=12 && maxBlades==count*(clones+1),"Peak="+maxBlades+", configured="+count*(clones+1));
        if(!Baseline)Record(name+": all simultaneous casters preserve every warning direction",maxMarkers==count*(clones+1));
        Record(name+": motion ends with no active enemy blade",Bullets().Length==0);
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");
        File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,monsterId=id,count,manualCloneCasters=clones,frames=times.Count,elapsed=times.LastOrDefault(),maxBlades,maxMarkers,maxRenderers,timeScale=1,retimed=false}));
        ReleaseBosses();yield return new WaitForSeconds(.4f);changeResolution?.Invoke(1080,1920);yield return null;
    }
    Vector3[] Geometry(Component presentation)
    {
        if(presentation==null)return new Vector3[0];
        return presentation.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh!=null).SelectMany(m=>m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v))).ToArray();
    }
    IEnumerator PauseAndCancellation()
    {
        ReleaseBosses();ResetPlayer(origin);var go=CreateBoss(31021,origin);yield return null;var module=Module(go);module.Execute();yield return new WaitForSeconds(.2f);
        var presentation=Presentation(go);Time.timeScale=0;yield return new WaitForEndOfFrame();var geometry=Geometry(presentation);int phase=Int(presentation,"Phase");
        yield return new WaitForSecondsRealtime(.18f);yield return new WaitForEndOfFrame();
        Record("Pause freezes charge geometry, phase, health and projectile timing",geometry.Length>0 && geometry.SequenceEqual(Geometry(presentation)) && Int(presentation,"Phase")==phase && Bullets().Length==0 && health.Hp==10000);
        Time.timeScale=1;yield return new WaitForSeconds(.34f);yield return new WaitForFixedUpdate();
        var blades=Bullets();Time.timeScale=0;yield return new WaitForEndOfFrame();var positions=blades.Select(b=>b.transform.position).ToArray();
        yield return new WaitForSecondsRealtime(.18f);Record("Pause freezes actual enemy blades and keeps them leased",blades.Length==8 && blades.All(b=>b!=null && b.gameObject.activeSelf) && positions.SequenceEqual(blades.Select(b=>b.transform.position)));
        Time.timeScale=1;yield return new WaitForSeconds(1);Record("Resume lets the original enemy lifetime return all blades",Bullets().Length==0);GameObjectPool.Release(go);yield return null;
        foreach(string mode in new[]{"disable","pool","death","hidden-death"})
        {
            ResetPlayer(origin);go=CreateBoss(31021,origin);yield return null;module=Module(go);presentation=Presentation(go);module.Execute();yield return new WaitForSeconds(.15f);
            Record(mode+": cancellation case begins with real charge geometry",Int(presentation,"Phase")==1 && Bool(presentation,"HasVisibleGeometry"));
            if(mode=="disable")go.SetActive(false);
            else if(mode=="pool")GameObjectPool.Release(go);
            else {go.GetComponent<EnemyBase>().preventPoolDeath=mode=="hidden-death";go.GetComponent<EnemyBase>().TakeDamage(20000);}
            yield return new WaitForSeconds(.65f);
            Record(mode+": cancelled charge cannot emit delayed damage or stale warning",Bullets().Length==0 && health.Hp==10000 && (presentation==null || (Int(presentation,"Phase")==0 && !Bool(presentation,"HasVisibleGeometry"))));
            if(mode=="disable" && go!=null){go.SetActive(true);GameObjectPool.Release(go);}
            if(mode=="hidden-death" && go!=null){var enemy=go.GetComponent<EnemyBase>();enemy.preventPoolDeath=false;enemy.ShowFromRevive();GameObjectPool.Release(go);}
        }
        ReleaseBosses();ResetPlayer(origin);go=CreateBoss(31021,origin);yield return null;
        float previousScale=Time.timeScale;Time.timeScale=.25f;module=Module(go);presentation=Presentation(go);module.Execute();
        float releaseDeadline=Time.realtimeSinceStartup+8;
        while(Int(presentation,"Phase")==1 && Time.realtimeSinceStartup<releaseDeadline)yield return null;
        var releaseBrain=go.GetComponent<BossBrain>();bool enteredRelease=Int(presentation,"Phase")==2 && releaseBrain.IsBusy;
        GameObjectPool.Release(go);
        bool releaseCleared=!releaseBrain.IsBusy && Int(presentation,"Phase")==0 && !Bool(presentation,"HasVisibleGeometry");
        // Read the raw pool result before CreateBoss can reset IsBusy, so the fixture cannot hide stale state.
        var reusedBoss=GameObjectPool.Get(bossPrefab,origin+Vector2.right*6,Quaternion.identity);
        Record("Release-phase interruption and same-instance reuse clear busy and old geometry",enteredRelease && releaseCleared && reusedBoss==go && !reusedBoss.GetComponent<BossBrain>().IsBusy && Int(Presentation(reusedBoss),"Phase")==0 && !Bool(Presentation(reusedBoss),"HasVisibleGeometry"));
        GameObjectPool.Release(reusedBoss);Time.timeScale=previousScale;ReleaseBullets();
        ReleaseBosses();yield return null;
    }
    IEnumerator LimiterAndPoolChecks()
    {
        ResetPlayer(origin);var go=CreateBoss(31021,origin);yield return null;
        var limiter=SpawnLimiter.Instance;var config=limiter!=null?limiter.configs.FirstOrDefault(c=>c!=null && c.key==CombatVfxSpawner.SpawnLimiterKey):null;
        Record("Actual Game scene provides the shared decoration limiter",config!=null);
        if(config!=null)
        {
            int alive=config.currentAlive,frame=config.spawnThisFrame;config.currentAlive=config.maxAlive;config.spawnThisFrame=Mathf.Max(config.spawnPerFrame,1);
            var decoration=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Prefabs/Effects/Impact/fx_sword_hit_v1.prefab");
            bool rejected=!CombatVfxSpawner.TryPlayPooled(decoration,origin,Quaternion.identity);
            Module(go).Execute();yield return new WaitForSeconds(.24f);
            Record("Saturated player decoration budget still renders all enemy warnings",rejected && Int(Presentation(go),"MarkerCount")==8 && Bool(Presentation(go),"HasVisibleGeometry"));
            yield return new WaitForSeconds(.3f);yield return new WaitForFixedUpdate();
            Record("Saturated player decoration budget never culls damaging enemy blades",Bullets().Length==8 && Bullets().All(b=>b.GetComponent<Collider2D>().enabled));
            config.currentAlive=alive;config.spawnThisFrame=frame;
        }
        ReleaseBullets();GameObjectPool.Release(go);yield return new WaitForSeconds(.3f);
        var prefab=go.GetComponent<BossBrain>().FindPrefab("BladeBurstBullet") ?? go.GetComponent<BossBrain>().FindPrefab("StraightBullet");
        Record("Dedicated blade keeps the existing zero wall mask and .4 trigger",prefab.GetComponent<BossBullet>().wallMask.value==0 && prefab.GetComponent<CircleCollider2D>().isTrigger && Mathf.Approximately(prefab.GetComponent<CircleCollider2D>().radius,.4f));
        var probe=GameObjectPool.Get(prefab,origin,Quaternion.identity);var trails=probe.GetComponentsInChildren<TrailRenderer>();
        foreach(var trail in trails){trail.AddPosition(origin);trail.AddPosition(origin+Vector2.up);}
        Record("New enemy projectile provides a real short trail",trails.Length>0 && trails.All(t=>t.time>0 && t.time<=.16f));
        GameObjectPool.Release(probe);Record("Enemy projectile pool release clears every old trail",trails.Length>0 && trails.All(t=>t.positionCount==0 && !t.emitting));
        var reused=GameObjectPool.Get(prefab,origin+Vector2.right*8,Quaternion.identity);
        Record("Enemy projectile reuse cannot bridge across screen",reused==probe && trails.All(t=>t.positionCount<=1 && t.emitting));
        var random=UnityEngine.Random.state;Invoke(ProjectilePresentation(reused),"PlayDissolve",(Vector2)reused.transform.position);
        Record("Enemy dissolve presentation consumes no combat RNG",random.Equals(UnityEngine.Random.state));GameObjectPool.Release(reused);yield return new WaitForSeconds(.5f);
    }
    IEnumerator EdgeLifetime()
    {
        ReleaseBosses();Vector2 edge=new Vector2(2,15);ResetPlayer(edge);var go=CreateBoss(32021,edge);yield return null;Module(go).Execute();yield return new WaitForSeconds(.54f);yield return new WaitForFixedUpdate();
        Record("Map-edge burst preserves the full sixteen damaging blades",Bullets().Length==16 && Bullets().All(b=>b.wallMask.value==0));
        yield return Snap("map-edge-flight");yield return new WaitForSeconds(1);Record("Map-edge burst leaves no leased blade after authored expiry",Bullets().Length==0);GameObjectPool.Release(go);yield return null;
    }
    protected override void OnDestroy()
    {
        if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}
        Application.runInBackground=originalBackground;base.OnDestroy();
    }
}
#endif
