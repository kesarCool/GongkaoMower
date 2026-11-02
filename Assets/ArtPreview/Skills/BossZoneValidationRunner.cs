#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BossZoneValidationRunner : UIValidationRunnerBase
{
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    bool Baseline=>SessionState.GetBool("BossZoneBaseline",false);
    protected override string OutputFolder=>SessionState.GetString("SkillVfxToolkit.OutputFolder","BossZoneV1/"+(Baseline?"Before":"After"));
    protected override string ValidationNote=>"Real Game scene, common Boss prefab and real ZoneModule; explicit test-only zone injection because Monster rows do not currently assign zone. Memory save. Scaled-time gameplay, timestamped real-time capture. No device-performance claim.";
    readonly Vector2 center=new Vector2(15,15);
    GameObject prefab,owner;EnemyCatalog catalog;Transform player;PlayerHealth health;ZoneModule skill;BossBrain brain;
    bool saved,background;float originalScale;UnityEngine.Random.State originalRandom;
    object Read(object o,string name){var t=o.GetType();var p=t.GetProperty(name,Members);if(p!=null)return p.GetValue(o,null);for(;t!=null;t=t.BaseType){var f=t.GetField(name,Members);if(f!=null)return f.GetValue(o);}return null;}
    object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Members).Invoke(o,args);
    Component View=>owner==null?null:owner.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(v=>v!=null&&v.GetType().Name=="BossZonePresentation");
    void Awake(){background=SessionState.GetBool("BossZoneValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    void Clear(){foreach(var e in FindObjectsOfType<EnemyBase>()){SpawnLimiter.Instance?.Unregister("Enemy",e.gameObject);GameObjectPool.Release(e.gameObject);}foreach(var b in FindObjectsOfType<BossBullet>())GameObjectPool.Release(b.gameObject);owner=null;}
    IEnumerator Owner(int id=31051,string parameters="10,5,1.5,30")
    {
        Clear();yield return null;owner=GameObjectPool.Get(prefab,center,Quaternion.identity);owner.transform.localScale=Vector3.one;brain=owner.GetComponent<BossBrain>();brain.enabled=true;
        EnemyDefinition def;catalog.TryGet(id,out def);var enemy=owner.GetComponent<EnemyBase>();enemy.preventPoolDeath=false;enemy.ShowFromRevive();enemy.InitFromDefinition(def);enemy.ApplyTableStats(25,10000,1.5f);owner.GetComponentInChildren<EnemyWordLabel>().SetWord(def.enemyName);
        owner.GetComponent<EnemyAI>().enabled=false;foreach(var c in owner.GetComponentsInChildren<Collider2D>())c.enabled=false;
        var body=owner.GetComponent<Rigidbody2D>();body.position=center;body.velocity=Vector2.zero;
        player.position=center+Vector2.down*5;health.OnPreDamage=null;health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();health.SetInvulnerable(false);
        yield return null;brain.RemoveAllModulesExcept(new HashSet<string>{"revive"});Call(brain,"AddModule","zone",parameters,id);
        var mods=(List<BossSkillModule>)Read(brain,"_modules");foreach(var m in mods)m.cooldown=9999;skill=mods.OfType<ZoneModule>().Single();Physics2D.SyncTransforms();
    }
    Vector2[] Points(int seed,int count=5)
    {
        var prior=UnityEngine.Random.state;UnityEngine.Random.InitState(seed);var points=new Vector2[count];
        for(int i=0;i<count;i++){float t=(float)i/(count-1),deg=-90+Mathf.Lerp(-75,75,t);points[i]=center+new Vector2(Mathf.Cos(deg*Mathf.Deg2Rad),Mathf.Sin(deg*Mathf.Deg2Rad))*Mathf.Lerp(1,6,t*.7f+UnityEngine.Random.value*.3f);}
        UnityEngine.Random.state=prior;return points;
    }
    void Cast(int seed=7421){UnityEngine.Random.InitState(seed);skill.Execute();skill.cooldown=9999;}
    IEnumerator Wait(Func<bool> test,float seconds=4){float until=Time.realtimeSinceStartup+seconds;while(!test()&&Time.realtimeSinceStartup<until)yield return null;}
    IEnumerator Snap(string name){float scale=Time.timeScale;Time.timeScale=0;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=scale;}
    protected override IEnumerator RunChecks()
    {
        saved=true;originalScale=Time.timeScale;originalRandom=UnityEngine.Random.state;Time.timeScale=1;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,105);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        var spawner=FindObjectOfType<SpawnerWaves>();spawner.StopAllCoroutines();spawner.autoTriggerWavesOnStart=false;spawner.enableNormalSpawn=false;spawner.debugLogs=false;
        foreach(var o in FindObjectsOfType<BattleOutcomeCoordinator>()){o.StopAllCoroutines();o.enabled=false;}foreach(var f in FindObjectsOfType<CameraFollow2D>())f.enabled=false;
        UIManager.Instance.CloseAllStack();Clear();yield return null;CombatTargetRegistry.ClearAll();var skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.ClearAllSkills();skills.enabled=false;player.GetComponent<PlayerController>().enabled=false;health=player.GetComponent<PlayerHealth>();
        catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Boss/boss_common.prefab");
        Camera.main.orthographicSize=12;Camera.main.transform.position=new Vector3(center.x+.8f,center.y-1,-10);
        yield return Owner();Record("Actual test-only module preserves cooldown count radius damage",skill.interval==10&&skill.firstDelayMul==.5f&&(int)Read(skill,"_zoneCount")==5&&(float)Read(skill,"_zoneRadius")==1.5f&&(float)Read(skill,"_damage")==30);
        Cast();yield return new WaitForSeconds(.04f);
        if(Baseline){var warning=owner.transform.Find("ZoneWarn_0");Record("Baseline captures existing world-position mismatch",warning!=null&&Vector2.Distance(warning.position,Points(7421)[0])>10);File.WriteAllText(Path.Combine(output,"baseline-observation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{expected=new[]{Points(7421)[0].x,Points(7421)[0].y},actual=new[]{warning.position.x,warning.position.y},oldPerZoneWarningSeconds=.14f,perZoneTextures=true}));}
        yield return Snap("warning");yield return new WaitForSeconds(Baseline?.14f:.72f);yield return Snap("rupture");yield return new WaitForSeconds(2);
        Record("Cast releases Boss busy state",!brain.IsBusy);
        if(!Baseline)yield return AdditionalChecks();
        yield return Motion();Clear();yield return null;yield return SceneManager.UnloadSceneAsync("Game");Record("Battle exit leaves no enemies",FindObjectsOfType<EnemyBase>().Length==0);Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator AdditionalChecks()
    {
        yield return Owner();var expected=Points(7421);Cast();yield return new WaitForSeconds(.06f);
        Record("Warning is visible immediately at the actual world hit position",View!=null&&(bool)Read(View,"HasVisibleGeometry")&&(bool)Call(View,"HasWarningAt",(Vector3)expected[0],1.5f,.002f));
        var worldVertices=View.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p=>(Vector2)View.transform.TransformPoint(p)).ToArray();
        Record("Rendered outer boundary equals the actual 1.5-unit overlap radius",Mathf.Abs(worldVertices.Max(p=>Vector2.Distance(p,expected[0]))-1.5f)<.003f);
        var mesh=View.GetComponent<MeshFilter>().sharedMesh;owner.transform.position+=new Vector3(3,2);owner.transform.localScale=new Vector3(1.3f,.7f,1);Call(View,"RenderNow");
        worldVertices=mesh.vertices.Select(p=>(Vector2)View.transform.TransformPoint(p)).ToArray();
        Record("Moving or scaling the owner preserves the world warning circle",(bool)Call(View,"HasWarningAt",(Vector3)expected[0],1.5f,.002f)&&Mathf.Abs(worldVertices.Max(p=>Vector2.Distance(p,expected[0]))-1.5f)<.003f);
        Time.timeScale=0;yield return new WaitForEndOfFrame();var verts=mesh.vertices;int warnings=(int)Read(View,"WarningCount");yield return new WaitForSecondsRealtime(.2f);
        Record("Pause freezes warning progression and mesh",verts.SequenceEqual(mesh.vertices)&&warnings==(int)Read(View,"WarningCount")&&(int)Read(skill,"ImpactCount")==0);Time.timeScale=1;yield return Wait(()=>!brain.IsBusy);yield return Wait(()=>!(bool)Read(View,"HasVisibleGeometry"));
        Record("Five impacts retire their warning and residue geometry",(int)Read(skill,"ImpactCount")==5&&(int)Read(View,"WarningCount")==0&&!(bool)Read(View,"HasVisibleGeometry"));
        int builds=(int)Read(View,"MeshBuildCount");yield return new WaitForSeconds(.15f);Record("Expired ground fissures stop rebuilding their idle mesh",builds==(int)Read(View,"MeshBuildCount"));
        yield return Owner();Cast();var lockedFirst=expected[0];yield return new WaitForSeconds(.06f);player.position=center+Vector2.up*6;Physics2D.SyncTransforms();yield return Wait(()=>(int)Read(skill,"ImpactCount")>0);
        Record("Moving the player after cast does not retarget an existing warning",Vector2.Distance((Vector2)Read(skill,"LastImpactPosition"),lockedFirst)<.002f);
        yield return Owner();expected=Points(7421);Cast();player.position=expected[0];Physics2D.SyncTransforms();float hp=health.Hp;yield return new WaitForSeconds(.45f);Record("Player inside the real circle takes no premature warning damage",health.Hp==hp);yield return Wait(()=>(int)Read(skill,"ImpactCount")>=1);
        Record("First real impact follows a full 0.7-second warning and deals 30",Mathf.Approximately(hp-health.Hp,30)&&(float)Read(skill,"LastImpactTime")-(float)Read(skill,"CastTime")>=.69f);
        Record("Impact immediately removes the danger boundary",!(bool)Call(View,"HasWarningAt",(Vector3)expected[0],1.5f,.002f));
        player.position=expected[4];health.SetInvulnerable(false);Physics2D.SyncTransforms();yield return Wait(()=>!brain.IsBusy);Record("A single cast cannot hit the same PlayerHealth twice",Mathf.Approximately(hp-health.Hp,30));
        yield return Owner();expected=Points(7421);Cast();player.position=expected[0]+Vector2.up*2.8f;Physics2D.SyncTransforms();hp=health.Hp;yield return Wait(()=>!brain.IsBusy);Record("Player outside every true circle receives no ground-fissure damage",health.Hp==hp);
        foreach(string mode in new[]{"disable","pool","rebuild","death","revive"})
        {
            yield return Owner(mode=="revive"?32082:31051);Cast();yield return new WaitForSeconds(.15f);var v=View;
            if(mode=="disable")owner.SetActive(false);else if(mode=="pool")GameObjectPool.Release(owner);else if(mode=="rebuild")brain.OnEnemyDataReady();else owner.GetComponent<EnemyBase>().TakeDamage(20000);
            yield return new WaitForSeconds(.85f);Record(mode+": cancellation removes pending damage and all warning geometry",(int)Read(skill,"ImpactCount")==0&&!(bool)Read(v,"HasVisibleGeometry")&&brain.IsBusy==brain.IsReviving);
        }
        yield return Owner();Cast();yield return Wait(()=>!brain.IsBusy);yield return Wait(()=>!(bool)Read(View,"HasVisibleGeometry"));mesh=View.GetComponent<MeshFilter>().sharedMesh;Cast();yield return new WaitForSeconds(.15f);Record("Repeated casts reuse one mesh without per-zone children or sprites",mesh==View.GetComponent<MeshFilter>().sharedMesh&&View.transform.childCount==0&&owner.GetComponentsInChildren<SpriteRenderer>().All(s=>!s.name.StartsWith("ZoneWarn")));
        yield return Wait(()=>!brain.IsBusy);
        yield return Owner();var cosmetic=SpawnLimiter.Instance.configs.First(c=>c.key==CombatVfxSpawner.SpawnLimiterKey);int oldMax=cosmetic.maxAlive;bool recycle=cosmetic.recycleOldest;cosmetic.maxAlive=0;cosmetic.recycleOldest=false;
        Cast();yield return new WaitForSeconds(.1f);Record("Cosmetic limits cannot suppress gameplay warnings",(int)Read(View,"WarningCount")>0&&(bool)Read(View,"HasVisibleGeometry"));cosmetic.maxAlive=oldMax;cosmetic.recycleOldest=recycle;yield return Wait(()=>!brain.IsBusy);
        yield return Owner();bool originalQueries=Physics2D.queriesHitTriggers;Physics2D.queriesHitTriggers=false;Cast();player.position=Points(7421)[0];Physics2D.SyncTransforms();hp=health.Hp;yield return Wait(()=>!brain.IsBusy);Record("Real trigger hits work and global query settings are restored",Mathf.Approximately(hp-health.Hp,30)&&!Physics2D.queriesHitTriggers);Physics2D.queriesHitTriggers=originalQueries;
        yield return Owner();var data=new SerializedObject(View);var originalMaterial=data.FindProperty("solidMaterial").objectReferenceValue;data.FindProperty("solidMaterial").objectReferenceValue=null;data.ApplyModifiedPropertiesWithoutUndo();Cast();yield return new WaitForSeconds(.85f);Record("Missing warning material refuses invisible damage",(int)Read(skill,"ImpactCount")==0&&!brain.IsBusy);data.FindProperty("solidMaterial").objectReferenceValue=originalMaterial;data.ApplyModifiedPropertiesWithoutUndo();
        yield return Owner();Cast();yield return new WaitForSeconds(.1f);var oldOwner=owner;var oldMesh=View.GetComponent<MeshFilter>().sharedMesh;GameObjectPool.Release(owner);yield return Owner();Cast();yield return new WaitForSeconds(.1f);Record("Pool reuse starts a fresh warning lease using the same mesh",owner==oldOwner&&oldMesh==View.GetComponent<MeshFilter>().sharedMesh&&(int)Read(skill,"ImpactCount")==0&&(int)Read(View,"WarningCount")==1);yield return Wait(()=>!brain.IsBusy);
        yield return Owner(31051,"10,5,3,30");Cast();var second=expected[0];yield return new WaitForSeconds(.05f);Record("Configured radius drives the warning directly",(bool)Call(View,"HasWarningAt",(Vector3)Points(7421)[0],3f,.002f));
        yield return Wait(()=>!brain.IsBusy);
    }
    IEnumerator Motion()
    {
        yield return Owner();changeResolution?.Invoke(540,960);yield return new WaitForSecondsRealtime(.2f);Camera.main.orthographicSize=12;Camera.main.transform.position=new Vector3(15.8f,14,-10);
        string folder=Path.Combine(output,"zone-motion");Directory.CreateDirectory(Path.Combine(folder,"frames"));var times=new List<float>();float start=Time.realtimeSinceStartup;Cast();
        while(Time.realtimeSinceStartup-start<3.1f){yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frames","frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.055f);}
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,frames=times.Count,timeScale=1,retimed=false}));Record("Real-time footage captures an entire cast",times.Count>15&&!brain.IsBusy);changeResolution?.Invoke(1080,1920);
    }
    protected override void OnDestroy(){if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}Application.runInBackground=background;base.OnDestroy();}
}
#endif
