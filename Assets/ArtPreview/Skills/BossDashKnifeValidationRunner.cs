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

/// <summary>Real table modules and physics, with optional presentation inspected through its public API.</summary>
public sealed class BossDashKnifeValidationRunner : UIValidationRunnerBase
{
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string BossPath="Assets/Prefab/Enemy/Boss/boss_common.prefab";
    bool Baseline=>SessionState.GetBool("BossDashKnifeBaseline",false);
    protected override string OutputFolder {get {string custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return string.IsNullOrEmpty(custom)?"BossDashKnifeV1/"+(Baseline?"Before":"After"):custom;}}
    protected override string ValidationNote=>"Actual Game scene, real Monster table modules, BossBullet and PlayerHealth. In-memory save only. EnemyAI is disabled to isolate DashModule.CheckDashHit from EnemyAI contact damage; the single-result OverlapCircle limitation is observed and compared, not repaired or presented as verified 30 damage. All real boss/player colliders remain enabled. Motion uses timeScale 1 and actual capture timestamps. No CloneModule inheritance, device FPS or WeChat package-size claim.";
    readonly List<GameObject> bosses=new List<GameObject>();
    readonly List<DashObservation> observations=new List<DashObservation>();
    readonly Vector2 center=new Vector2(15,15);
    readonly Dictionary<int,string> names=new Dictionary<int,string>{{31011,"烤鸭学徒"},{31031,"锅包大仙"},{31041,"蟹黄霸主"},{32022,"如影随形"}};
    UnityEngine.Random.State originalRandom;float originalScale;bool originalBackground,saved;
    PlayerSkills skills;PlayerHealth health;Transform player;GameObject bossPrefab;EnemyCatalog catalog;
    [Serializable] sealed class DashObservation {public string id;public float hpDelta,minPlayerDistance;public bool ownColliderReturned;public string note;}
    object Read(object instance,string name)
    {
        if(instance==null)return null;var type=instance.GetType();var property=type.GetProperty(name,Members);
        if(property!=null)return property.GetValue(instance,null);
        for(;type!=null;type=type.BaseType){var field=type.GetField(name,Members);if(field!=null)return field.GetValue(instance);}
        return null;
    }
    int Int(object instance,string name)=>Read(instance,name) is int?(int)Read(instance,name):-1;
    float Float(object instance,string name)=>Read(instance,name) is float?(float)Read(instance,name):float.NaN;
    bool Bool(object instance,string name)=>Read(instance,name) is bool && (bool)Read(instance,name);
    Vector2 Vector(object instance,string name)=>Read(instance,name) is Vector2?(Vector2)Read(instance,name):Vector2.zero;
    Component View(GameObject go,string type)=>go==null?null:go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null && c.GetType().Name==type);
    Component DashView(GameObject go)=>View(go,"BossDashPresentation");
    Component KnifeView(GameObject go)=>View(go,"BossKnifePresentation");
    void Render(Component view){if(view!=null)view.GetType().GetMethod("RenderNow",Members)?.Invoke(view,null);}
    List<BossSkillModule> Modules(GameObject go)=>(List<BossSkillModule>)typeof(BossBrain).GetField("_modules",Members).GetValue(go.GetComponent<BossBrain>());
    T Module<T>(GameObject go)where T:BossSkillModule=>Modules(go).OfType<T>().FirstOrDefault();
    HomingBullet[] Knives()=>FindObjectsOfType<HomingBullet>();
    BossBullet[] Bullets()=>FindObjectsOfType<BossBullet>();
    void Awake(){originalBackground=SessionState.GetBool("BossDashKnifeValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    void ReleaseBullets(){foreach(var bullet in Bullets())GameObjectPool.Release(bullet.gameObject);}
    void Cleanup(){foreach(var boss in bosses.Distinct())if(boss!=null && boss.activeSelf)GameObjectPool.Release(boss);bosses.Clear();ReleaseBullets();}
    void PlacePlayer(Vector2 point)
    {
        player.position=point;var body=player.GetComponent<Rigidbody2D>();body.velocity=Vector2.zero;body.gravityScale=0;
        health.OnPreDamage=null;health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();Physics2D.SyncTransforms();
    }
    void FrameCamera(Vector2 point){Camera.main.transform.position=new Vector3(point.x,point.y-1,-10);Camera.main.orthographicSize=10.5f;}
    GameObject CreateBoss(int id,Vector2 point)
    {
        var go=GameObjectPool.Get(bossPrefab,point,Quaternion.identity);bosses.Remove(go);bosses.Add(go);
        var brain=go.GetComponent<BossBrain>();brain.enabled=false;brain.StopAllCoroutines();brain.IsBusy=false;
        foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string type=behaviour.GetType().Name;
            if(!(behaviour is EnemyBase) && !(behaviour is BossBrain) && !(behaviour is EnemyWordLabel) && !(behaviour is TMP_Text) &&
                type!="BossDashPresentation" && type!="BossKnifePresentation" && type!="BossBladeBurstPresentation")behaviour.enabled=false;
        }
        EnemyDefinition definition;if(catalog==null || !catalog.TryGet(id,out definition))definition=new EnemyDefinition{id=id,enemyName=names[id],prefab=bossPrefab};
        var enemy=go.GetComponent<EnemyBase>();enemy.preventPoolDeath=false;enemy.addKillToGameLayer=false;enemy.ShowFromRevive();enemy.InitFromDefinition(definition);enemy.ApplyTableStats(1,10000,0);
        go.GetComponentInChildren<EnemyWordLabel>().SetWord(names[id]);var body=go.GetComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.simulated=true;body.velocity=Vector2.zero;body.gravityScale=0;
        Physics2D.SyncTransforms();return go;
    }
    IEnumerator Snap(string name)
    {
        float previous=Time.timeScale;Time.timeScale=0;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=previous;
    }
    IEnumerator WaitFor(Func<bool> condition,float seconds=12)
    {float deadline=Time.realtimeSinceStartup+seconds;while(!condition() && Time.realtimeSinceStartup<deadline)yield return null;}
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var camera in FindObjectsOfType<CameraFollow2D>())camera.enabled=false;
        UIManager.Instance.CloseAllStack();foreach(var enemy in FindObjectsOfType<EnemyBase>())Destroy(enemy.gameObject);yield return null;CombatTargetRegistry.ClearAll();
        skills=FindObjectOfType<PlayerSkills>();skills.enabled=false;skills.ClearAllSkills();player=skills.transform;health=player.GetComponent<PlayerHealth>();
        var controller=player.GetComponent<PlayerController>();if(controller!=null)controller.enabled=false;
        catalog=FindObjectOfType<EnemyCatalog>()??AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();
        string prefabPath=Baseline?SessionState.GetString("BossDashKnifeBaselinePrefab","Assets/ArtPreview/Skills/BossDashKnifeBaseline.prefab"):BossPath;
        bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Time.timeScale=1;FrameCamera(center);
        Record("Fixture uses actual Game player, real boss prefab and catalog",bossPrefab!=null && health!=null && catalog!=null);
        yield return DashCase(31041,15,"dash15");yield return DashCase(31031,20,"dash20");
        File.WriteAllText(Path.Combine(output,"mechanics-observation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,dash=observations,note="EnemyAI disabled; observations isolate the existing single-result DashModule.OverlapCircle and never relabel contact damage as skill damage."},Newtonsoft.Json.Formatting.Indented));
        yield return KnifeCase(31011,1,"knife-1",true);yield return KnifeCase(31031,.5f,"knife-half",false);
        yield return WallCase(false);yield return WallCase(true);
        if(!Baseline){yield return PauseAndCancellation();yield return PoolAndLimiter();}
        yield return Motion("dash15-motion",31041,"dash",2);yield return Motion("dash20-motion",31031,"dash",2);
        yield return Motion("knife-motion",31011,"knife",5.4f);yield return Motion("combo-eight-motion",31031,"combo",10);yield return Motion("combo-sixteen-motion",32022,"combo",10);
        Cleanup();yield return new WaitForSeconds(.5f);yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle exit removes all leased boss bullets and new presenters",Bullets().Length==0 && FindObjectsOfType<MonoBehaviour>().All(b=>b.GetType().Name!="BossDashPresentation" && b.GetType().Name!="BossKnifePresentation"));
        Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator DashCase(int id,float speed,string name)
    {
        Cleanup();PlacePlayer(center+Vector2.right*4);FrameCamera(center);var go=CreateBoss(id,center);yield return null;var module=Module<DashModule>(go);var brain=go.GetComponent<BossBrain>();var body=go.GetComponent<Rigidbody2D>();
        Record(name+": actual Monster table builds original dash parameters",module!=null && Mathf.Approximately(module.interval,6) && Mathf.Approximately(Float(module,"_dashSpeed"),speed) && Mathf.Approximately(Float(module,"_dashDistance"),8) && Mathf.Approximately(Float(module,"_chargeTime"),.3f) && Mathf.Approximately(Float(module,"_recoveryTime"),.2f) && Mathf.Approximately(Float(module,"_damage"),30));
        if(module==null)yield break;
        var ownHit=Physics2D.OverlapCircle(body.position,1.2f);bool ownReturned=ownHit!=null && ownHit.GetComponentInParent<EnemyBase>()==go.GetComponent<EnemyBase>();
        Time.timeScale=.25f;var random=UnityEngine.Random.state;module.Execute();float started=Time.time;yield return new WaitForSeconds(.07f);
        Record(name+": .3 second preparation does not move or damage early",Vector2.Distance(body.position,center)<.01f && health.Hp==10000 && Time.time-started<.3f && brain.IsBusy);
        if(!Baseline)Record(name+": route preparation does not consume combat RNG",random.Equals(UnityEngine.Random.state));
        yield return Snap(name+"-charge");PlacePlayer(center+Vector2.up*4);yield return new WaitForSeconds(.065f);
        if(!Baseline){Render(DashView(go));Record(name+": charge route follows the live player direction",Int(DashView(go),"Phase")==1 && Vector2.Dot(Vector(DashView(go),"AimDirection"),Vector2.up)>.995f && Bool(DashView(go),"HasVisibleGeometry"));}
        yield return Snap(name+"-follow");Vector2 expected=new Vector2(.6f,.8f);PlacePlayer(center+expected*4);yield return new WaitForSeconds(.045f);
        if(!Baseline){Render(DashView(go));Record(name+": route keeps following until the original release time",Int(DashView(go),"Phase")==1 && Vector2.Dot(Vector(DashView(go),"AimDirection"),expected)>.995f);}
        yield return WaitFor(()=>Vector2.Distance(body.position,center)>.08f);Vector2 releaseDirection=(body.position-center).normalized;
        Record(name+": actual dash locks the direction sampled after charge",Vector2.Dot(releaseDirection,expected)>.995f);
        if(!Baseline)Record(name+": visible locked direction agrees with actual motion",Int(DashView(go),"Phase")==2 && Vector2.Dot(Vector(DashView(go),"LockedDirection"),releaseDirection)>.995f);
        yield return Snap(name+"-locked");player.position=center-Vector2.right*4;Physics2D.SyncTransforms();
        yield return WaitFor(()=>!brain.IsBusy);yield return new WaitForFixedUpdate();float travel=Vector2.Distance(body.position,center),delta=10000-health.Hp;
        Record(name+": direction remains locked for the original eight-unit dash",Mathf.Abs(travel-8)<.16f && Vector2.Dot((body.position-center).normalized,releaseDirection)>.995f,"Actual distance="+travel);
        Record(name+": original recovery finishes and cooldown resets",!brain.IsBusy && Mathf.Approximately(module.cooldown,6));
        yield return Snap(name+"-stop");Time.timeScale=1;Cleanup();
        // A separate stationary-player pass observes real collision; the direction-lock pass deliberately moves its target away.
        PlacePlayer(center+Vector2.right*4);go=CreateBoss(id,center);yield return null;module=Module<DashModule>(go);brain=go.GetComponent<BossBrain>();
        var colliders=go.GetComponentsInChildren<Collider2D>();var ai=go.GetComponent<EnemyAI>();Record(name+": damage observation keeps real boss/player colliders and disables AI contact damage",colliders.All(c=>c.enabled) && player.GetComponent<Collider2D>().enabled && (ai==null || !ai.enabled));
        float minDistance=Vector2.Distance(go.transform.position,player.position);Time.timeScale=.25f;module.Execute();yield return WaitFor(()=>{minDistance=Mathf.Min(minDistance,Vector2.Distance(go.transform.position,player.position));return !brain.IsBusy;});delta=10000-health.Hp;
        var observation=new DashObservation{id=name,hpDelta=delta,minPlayerDistance=minDistance,ownColliderReturned=ownReturned,note=delta>0?"Stationary real player; isolated CheckDashHit damage, EnemyAI contact damage disabled.":"Stationary real player; no isolated skill damage observed. Single OverlapCircle can return the boss itself; existing mechanic retained."};observations.Add(observation);
        Record(name+": isolated dash damage is recorded without substituting contact damage",delta>0?Mathf.Abs(delta/30-Mathf.Round(delta/30))<.001f:ownReturned && minDistance<2.15f,observation.note+" Hp delta="+delta+" closest player distance="+minDistance);
        if(!Baseline)
        {
            string before=Path.Combine(Application.dataPath,"../ArtOutput/UI/BossDashKnifeV1/Before/mechanics-observation.json");
            var old=File.Exists(before)?Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(before))["dash"]?.FirstOrDefault(o=>(string)o["id"]==name):null;
            Record(name+": actual isolated damage outcome matches the baseline",old!=null && Mathf.Abs((float)old["hpDelta"]-delta)<.001f);
        }
        Time.timeScale=1;Cleanup();yield return null;
    }
    IEnumerator KnifeCase(int id,float interval,string name,bool hitPlayer)
    {
        Cleanup();PlacePlayer(center+Vector2.right*3.7f);FrameCamera(center);var go=CreateBoss(id,center);yield return null;var module=Module<HomingKnifeModule>(go);
        Record(name+": table keeps its original knife interval, speed, lifetime and damage",module!=null && Mathf.Approximately(module.interval,interval) && Mathf.Approximately(Float(module,"_knifeSpeed"),4) && Mathf.Approximately(Float(module,"_lifetime"),5) && Mathf.Approximately(Float(module,"_damage"),20));
        if(module==null)yield break;
        module.Tick(module.cooldown-.01f);Record(name+": configured cooldown does not fire early",!module.CanTrigger() && Knives().Length==0);module.Tick(.011f);Record(name+": configured cooldown reaches readiness",module.CanTrigger());
        Time.timeScale=.25f;var random=UnityEngine.Random.state;module.Execute();var knives=Knives();
        Record(name+": original knife fires immediately with unchanged collision fields",knives.Length==1 && knives.All(k=>Mathf.Approximately(k.turnRate,0) && Mathf.Approximately(k.speed,4) && Mathf.Approximately(k.damage,20) && Mathf.Approximately(k.lifetime,5) && Mathf.Approximately(k.GetComponent<CircleCollider2D>().radius,.35f) && k.wallMask.value==-1));
        if(!Baseline)
        {
            Record(name+": flash and projectile creation consume no combat RNG",random.Equals(UnityEngine.Random.state));
            Record(name+": dedicated short knife and immediate muzzle flash are present",knives.All(k=>k.GetComponent<BossBladeProjectilePresentation>()!=null) && Int(KnifeView(go),"Phase")==1 && Bool(KnifeView(go),"HasVisibleGeometry"));
        }
        yield return new WaitForSeconds(.025f);yield return Snap(name+"-flash");yield return new WaitForFixedUpdate();
        Record(name+": actual initial flight aims at the player",knives.Length==1 && Vector2.Dot(knives[0].GetComponent<Rigidbody2D>().velocity.normalized,Vector2.right)>.995f && health.Hp==10000);
        if(!hitPlayer){player.position=center+Vector2.up*5;Physics2D.SyncTransforms();}
        yield return new WaitForSeconds(.15f);Record(name+": moving the player never adds tracking to turnRate zero",knives.Length==1 && knives[0].gameObject.activeSelf && Vector2.Dot(knives[0].GetComponent<Rigidbody2D>().velocity.normalized,Vector2.right)>.995f);
        yield return Snap(name+"-flight");Time.timeScale=1;
        if(hitPlayer){yield return WaitFor(()=>health.Hp<10000,3);Record(name+": real BossBullet collision deals the original twenty damage",Mathf.Abs(health.Hp-9980)<.001f,"Hp="+health.Hp);yield return Snap(name+"-contact");}
        else {yield return new WaitForSeconds(5.1f);Record(name+": a missed straight knife expires at its original lifetime",Knives().Length==0 && health.Hp==10000);}
        Cleanup();yield return null;
    }
    IEnumerator WallCase(bool mapEdge)
    {
        Cleanup();GameObject temporary=null;Collider2D wall;Vector2 start,dir;
        if(mapEdge)
        {
            wall=FindObjectsOfType<WallMarker>().SelectMany(marker=>marker.GetComponents<Collider2D>()).FirstOrDefault(col=>col.name=="Wall_Left");
            Record("Actual map supplies a physical left perimeter wall",wall!=null);if(wall==null)yield break;
            start=(Vector2)wall.bounds.center+Vector2.right*4;dir=Vector2.left;
        }
        else
        {
            temporary=new GameObject("DashValidation_StaticWall");temporary.transform.position=center+Vector2.right*4;var collider=temporary.AddComponent<BoxCollider2D>();collider.size=new Vector2(.3f,8);wall=collider;start=center;dir=Vector2.right;
        }
        PlacePlayer(start+dir*7);FrameCamera(start);var go=CreateBoss(31031,start);yield return null;Physics2D.SyncTransforms();var module=Module<DashModule>(go);var brain=go.GetComponent<BossBrain>();
        Time.timeScale=.25f;module.Execute();yield return new WaitForSeconds(.12f);string name=mapEdge?"map-edge":"wall";float preview=Baseline?0:Float(DashView(go),"VisibleDistance");
        if(!Baseline)Record(name+": visible route is truncated by the actual static blocker",preview>.1f && preview<4 && Bool(DashView(go),"HasVisibleGeometry"),"Preview distance="+preview);
        yield return Snap(name+"-charge");yield return WaitFor(()=>!brain.IsBusy);yield return new WaitForFixedUpdate();float actual=Vector2.Distance(start,go.GetComponent<Rigidbody2D>().position);
        Record(name+": original Rigidbody cast stops the real dash before the wall",actual>.1f && actual<4 && module.wallMask.value==-1,"Actual distance="+actual);
        if(!Baseline)Record(name+": warning end agrees with the real wall-stopped dash",Mathf.Abs(preview-actual)<.2f,"Preview="+preview+" actual="+actual);
        yield return Snap(name+"-stop");Time.timeScale=1;Cleanup();if(temporary!=null)Destroy(temporary);yield return null;
    }
    Vector3[] Geometry(Component view)=>view==null?new Vector3[0]:view.GetComponentsInChildren<MeshFilter>(true).Where(filter=>filter.sharedMesh!=null).SelectMany(filter=>filter.sharedMesh.vertices.Select(vertex=>filter.transform.TransformPoint(vertex))).ToArray();
    IEnumerator PauseAndCancellation()
    {
        Cleanup();PlacePlayer(center+Vector2.right*5);var go=CreateBoss(31031,center);yield return null;Time.timeScale=.25f;var module=Module<DashModule>(go);module.Execute();yield return new WaitForSeconds(.08f);
        Time.timeScale=0;yield return new WaitForEndOfFrame();var geometry=Geometry(DashView(go));var position=go.transform.position;yield return new WaitForSecondsRealtime(.16f);yield return new WaitForEndOfFrame();
        Record("Pause freezes live dash warning, original charge and real body",geometry.Length>0 && geometry.SequenceEqual(Geometry(DashView(go))) && Int(DashView(go),"Phase")==1 && go.transform.position==position && health.Hp==10000);
        Time.timeScale=.25f;yield return WaitFor(()=>Int(DashView(go),"Phase")==2);Time.timeScale=0;yield return new WaitForEndOfFrame();position=go.transform.position;geometry=Geometry(DashView(go));
        yield return new WaitForSecondsRealtime(.16f);yield return new WaitForEndOfFrame();Record("Pause freezes the locked dash body and route geometry",Int(DashView(go),"Phase")==2 && position==go.transform.position && geometry.SequenceEqual(Geometry(DashView(go))));
        Time.timeScale=1;yield return WaitFor(()=>!go.GetComponent<BossBrain>().IsBusy);Cleanup();
        foreach(string mode in new[]{"disable","pool","hidden-death"})
        {
            PlacePlayer(center+Vector2.right*5);go=CreateBoss(31031,center);yield return null;Time.timeScale=.25f;Module<DashModule>(go).Execute();yield return new WaitForSeconds(.075f);position=go.transform.position;
            Record(mode+": actual dash cancellation starts during preparation",Int(DashView(go),"Phase")==1);
            if(mode=="disable")go.SetActive(false);else if(mode=="pool")GameObjectPool.Release(go);else{go.GetComponent<EnemyBase>().preventPoolDeath=true;go.GetComponent<EnemyBase>().TakeDamage(20000);}
            yield return new WaitForSeconds(.38f);
            Record(mode+": cancelled dash has no delayed movement, damage or warning",go.transform.position==position && health.Hp==10000 && Int(DashView(go),"Phase")==0 && !Bool(DashView(go),"HasVisibleGeometry") && !go.GetComponent<BossBrain>().IsBusy);
            if(mode=="disable"){go.SetActive(true);GameObjectPool.Release(go);}if(mode=="hidden-death"){go.GetComponent<EnemyBase>().preventPoolDeath=false;go.GetComponent<EnemyBase>().ShowFromRevive();GameObjectPool.Release(go);}Cleanup();
        }
        PlacePlayer(center+Vector2.right*5);go=CreateBoss(31031,center);yield return null;Time.timeScale=.25f;Module<DashModule>(go).Execute();yield return WaitFor(()=>Int(DashView(go),"Phase")==2);
        GameObjectPool.Release(go);Vector2 newSpawn=center+Vector2.left*6;var reused=GameObjectPool.Get(bossPrefab,newSpawn,Quaternion.identity);
        Record("Mid-dash raw pool reuse clears busy, locked route and stale geometry",reused==go && !reused.GetComponent<BossBrain>().IsBusy && Int(DashView(reused),"Phase")==0 && !Bool(DashView(reused),"HasVisibleGeometry"));
        // Do not sync transforms or reset the body in the fixture: the first real physics step must expose a queued old MovePosition.
        yield return new WaitForFixedUpdate();yield return null;var reusedBody=reused.GetComponent<Rigidbody2D>();
        Record("Mid-dash pool reuse survives its first physics step at the new spawn",reused==go && reused.activeInHierarchy && Vector2.Distance(reused.transform.position,newSpawn)<.02f && Vector2.Distance(reusedBody.position,newSpawn)<.02f && reusedBody.velocity.sqrMagnitude<.0001f && !reused.GetComponent<BossBrain>().IsBusy && Int(DashView(reused),"Phase")==0 && !Bool(DashView(reused),"HasVisibleGeometry"),"Transform="+reused.transform.position+" body="+reusedBody.position+" spawn="+newSpawn+" velocity="+reusedBody.velocity);
        GameObjectPool.Release(reused);Time.timeScale=1;Cleanup();
        PlacePlayer(center+Vector2.right*5);go=CreateBoss(31011,center);yield return null;Module<HomingKnifeModule>(go).Execute();yield return new WaitForFixedUpdate();var knife=Knives().FirstOrDefault();
        Time.timeScale=0;yield return new WaitForEndOfFrame();position=knife!=null?knife.transform.position:Vector3.zero;yield return new WaitForSecondsRealtime(.16f);
        Record("Pause freezes actual knife flight without expiring the leased bullet",knife!=null && knife.gameObject.activeSelf && knife.transform.position==position);
        GameObjectPool.Release(go);Record("Boss pool release clears its immediate knife flash",Int(KnifeView(go),"Phase")==0 && !Bool(KnifeView(go),"HasVisibleGeometry"));Time.timeScale=1;Cleanup();yield return null;
    }
    IEnumerator PoolAndLimiter()
    {
        Cleanup();PlacePlayer(center+Vector2.right*5);var go=CreateBoss(31031,center);yield return null;var brain=go.GetComponent<BossBrain>();var prefab=brain.FindPrefab("KnifeBullet")??brain.FindPrefab("HomingBullet");
        var probe=GameObjectPool.Get(prefab,center,Quaternion.identity);var trails=probe.GetComponentsInChildren<TrailRenderer>();foreach(var trail in trails){trail.AddPosition(center);trail.AddPosition(center+Vector2.up);}
        Record("Dedicated knife has one short soft trail",trails.Length==1 && trails[0].time>0 && trails[0].time<=.1f);GameObjectPool.Release(probe);
        Record("Knife pool release clears and disables its old trail",trails.Length==1 && trails.All(t=>t.positionCount==0 && !t.emitting));var reused=GameObjectPool.Get(prefab,center+Vector2.right*8,Quaternion.identity);
        Record("Knife reuse across screen cannot bridge the old trail",reused==probe && trails.All(t=>t.positionCount<=1 && t.emitting));GameObjectPool.Release(reused);
        var limiter=SpawnLimiter.Instance;var config=limiter?.configs.FirstOrDefault(c=>c!=null && c.key==CombatVfxSpawner.SpawnLimiterKey);Record("Game supplies the real shared cosmetic limiter",config!=null);
        if(config!=null)
        {
            int alive=config.currentAlive,frame=config.spawnThisFrame;config.currentAlive=config.maxAlive;config.spawnThisFrame=Mathf.Max(config.spawnPerFrame,1);
            bool rejected=!CombatVfxSpawner.TryPlayPooled(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Prefabs/Effects/Impact/fx_sword_hit_v1.prefab"),center,Quaternion.identity);
            Time.timeScale=.25f;Module<HomingKnifeModule>(go).Execute();Module<DashModule>(go).Execute();yield return new WaitForSeconds(.075f);
            Record("Cosmetic saturation never culls the real knife or dangerous dash warning",rejected && Knives().Length==1 && Knives()[0].GetComponent<Collider2D>().enabled && Int(DashView(go),"Phase")==1 && Bool(DashView(go),"HasVisibleGeometry"));
            config.currentAlive=alive;config.spawnThisFrame=frame;
        }
        Cleanup();Time.timeScale=1;yield return null;
    }
    IEnumerator Motion(string name,int id,string mode,float duration)
    {
        changeResolution?.Invoke(540,960);yield return null;yield return new WaitForSecondsRealtime(.2f);Cleanup();PlacePlayer(center+Vector2.right*4);FrameCamera(center);
        var go=CreateBoss(id,center);yield return null;var brain=go.GetComponent<BossBrain>();var modules=Modules(go).Where(module=>mode=="combo"?module is DashModule || module is HomingKnifeModule || module is BladeBurstModule:mode=="dash"?module is DashModule:module is HomingKnifeModule).ToArray();
        if(mode=="dash")Camera.main.transform.position=new Vector3(center.x+4,center.y-1,-10);
        foreach(var module in modules)module.Tick(module.cooldown+.001f);Time.timeScale=1;
        string folder=Path.Combine(output,name),frames=Path.Combine(folder,"frames");Directory.CreateDirectory(frames);var times=new List<float>();
        int dashCasts=0,knifeCasts=0,burstCasts=0,maxKnives=0,maxBullets=0,warningFrames=0;float start=Time.realtimeSinceStartup,last=Time.time,nextCapture=0;
        while(Time.realtimeSinceStartup-start<duration && times.Count<220)
        {
            float elapsed=Time.realtimeSinceStartup-start,delta=Time.time-last;last=Time.time;
            if(mode=="combo"){float angle=elapsed*.65f;player.position=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*4.7f;Physics2D.SyncTransforms();Vector2 middle=((Vector2)go.transform.position+(Vector2)player.position)*.5f;Camera.main.transform.position=new Vector3(middle.x,middle.y-1,-10);}
            foreach(var module in modules){module.Tick(delta);if(module.CanTrigger() && !brain.IsBusy){module.Execute();if(module is DashModule)dashCasts++;else if(module is HomingKnifeModule)knifeCasts++;else if(module is BladeBurstModule)burstCasts++;}}
            maxKnives=Mathf.Max(maxKnives,Knives().Length);maxBullets=Mathf.Max(maxBullets,Bullets().Length);if(!Baseline && Bool(DashView(go),"HasVisibleGeometry"))warningFrames++;
            if(elapsed>=nextCapture)
            {
                yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);ScreenCapture.CaptureScreenshot(Path.Combine(frames,"frame-"+(times.Count-1).ToString("D4")+".png"));nextCapture=elapsed+.08f;
            }
            yield return null;
        }
        Record(name+": real-time capture records the actual configured module casts",times.Count>=10 && (mode=="knife"?knifeCasts>=4:mode=="dash"?dashCasts==1:dashCasts>=2 && knifeCasts>=8 && burstCasts>=2),"dash="+dashCasts+" knife="+knifeCasts+" burst="+burstCasts);
        if(!Baseline && mode!="knife")Record(name+": real dash stages retain visible route geometry",warningFrames>0);
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(time=>time.ToString(CultureInfo.InvariantCulture)))+"]");
        File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,monsterId=id,mode,frames=times.Count,elapsed=times.LastOrDefault(),dashCasts,knifeCasts,burstCasts,maxKnives,maxBullets,warningFrames,timeScale=1,retimed=false,manualClones=0}));
        Cleanup();yield return new WaitForSeconds(.4f);changeResolution?.Invoke(1080,1920);yield return null;
    }
    protected override void OnDestroy(){if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}Application.runInBackground=originalBackground;base.OnDestroy();}
}
#endif
