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

public sealed class BossReviveValidationRunner:UIValidationRunnerBase
{
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    bool Baseline=>SessionState.GetBool("BossReviveBaseline",false);
    protected override string OutputFolder {get {var custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return custom==""?"BossReviveV1/"+(Baseline?"Before":"After"):custom;}}
    protected override string ValidationNote=>"Real Game scene and enabled table-driven BossBrain / ReviveModule, actual EnemyBase lethal damage and ResistShield damage probes. In-memory save, real-time video without retiming. Boss movement held for visual inspection. No WeChat performance or final upload-size claim.";
    readonly Vector2 center=new Vector2(15,15);
    readonly List<object> observations=new List<object>();
    GameObject prefab,owner;EnemyCatalog catalog;Transform player;PlayerHealth health;
    bool saved,originalBackground;float originalScale,minimumScale=1;UnityEngine.Random.State originalRandom;
    void Awake(){originalBackground=SessionState.GetBool("BossReviveValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    object Read(object o,string name){if(o==null)return null;var type=o.GetType();var p=type.GetProperty(name,Members);if(p!=null)return p.GetValue(o,null);for(;type!=null;type=type.BaseType){var f=type.GetField(name,Members);if(f!=null)return f.GetValue(o);}return null;}
    object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Members).Invoke(o,args);
    List<BossSkillModule> Modules(GameObject go)=>(List<BossSkillModule>)Read(go.GetComponent<BossBrain>(),"_modules");
    Component View(GameObject go)=>go==null?null:go.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null&&c.GetType().Name=="BossRevivePresentation");
    bool ShieldActive(GameObject go){var shield=go.GetComponent<ResistShield>();return shield!=null&&(Read(shield,"IsActive") is bool?(bool)Read(shield,"IsActive"):shield.enabled);}
    float GlyphTranslation(Component view)
    {
        float maximum=0;
        foreach(var batch in (IEnumerable)Read(view,"batches"))
        {
            var source=(List<Vector3>)Read(batch,"source");var animated=(List<Vector3>)Read(batch,"animated");
            for(int i=0;i<source.Count;i+=4){Vector2 a=Vector2.zero,b=Vector2.zero;for(int k=0;k<4;k++){a+=(Vector2)source[i+k];b+=(Vector2)animated[i+k];}maximum=Mathf.Max(maximum,Vector2.Distance(a*.25f,b*.25f));}
        }
        return maximum;
    }
    void LateUpdate(){if(owner!=null&&owner.activeInHierarchy)minimumScale=Mathf.Min(minimumScale,owner.transform.localScale.x);}
    void Clear()
    {
        foreach(var enemy in FindObjectsOfType<EnemyBase>()){SpawnLimiter.Instance?.Unregister("Enemy",enemy.gameObject);GameObjectPool.Release(enemy.gameObject);}
        foreach(var bullet in FindObjectsOfType<BossBullet>())GameObjectPool.Release(bullet.gameObject);owner=null;
    }
    GameObject Owner(int id)
    {
        owner=GameObjectPool.Get(prefab,center,Quaternion.identity);owner.transform.localScale=Vector3.one;minimumScale=1;
        var brain=owner.GetComponent<BossBrain>();brain.enabled=true;brain.StopAllCoroutines();brain.IsBusy=false;
        var eb=owner.GetComponent<EnemyBase>();eb.preventPoolDeath=false;eb.ShowFromRevive();EnemyDefinition definition;catalog.TryGet(id,out definition);eb.InitFromDefinition(definition);eb.ApplyTableStats(25,2000,1.5f);
        owner.GetComponentInChildren<EnemyWordLabel>().SetWord(definition.enemyName);owner.GetComponent<EnemyAI>().enabled=false;
        var body=owner.GetComponent<Rigidbody2D>();body.position=center;body.velocity=Vector2.zero;body.gravityScale=0;
        player.position=center+Vector2.down*5;health.SetMaxHp(10000);health.SetDefense(0);health.ResetToFull();Camera.main.orthographicSize=10.5f;Camera.main.transform.position=new Vector3(center.x,center.y-1,-10);Physics2D.SyncTransforms();return owner;
    }
    ReviveModule Revive(GameObject go){foreach(var module in Modules(go))module.cooldown=9999;return Modules(go).OfType<ReviveModule>().Single();}
    IEnumerator Wait(Func<bool> condition,float seconds=6){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;}
    IEnumerator Snap(string name){float scale=Time.timeScale;Time.timeScale=0;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=scale;}
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;Time.timeScale=1;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();Clear();yield return null;CombatTargetRegistry.ClearAll();
        var skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.enabled=false;skills.ClearAllSkills();health=player.GetComponent<PlayerHealth>();player.GetComponent<PlayerController>().enabled=false;
        catalog=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyCatalog.prefab").GetComponent<EnemyCatalog>();prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Boss/boss_common.prefab");
        Record("Fixture uses real Game, Boss catalog, player and pooled Boss",prefab!=null&&catalog!=null&&health!=null);
        yield return TableCase(32021,.5f,2,"half-twice");yield return TableCase(32081,.5f,1,"half-once");yield return TableCase(32082,1,1,"full-once");yield return TableCase(33021,.5f,2,"chapter3-twice");
        File.WriteAllText(Path.Combine(output,"mechanics-observation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(observations,Newtonsoft.Json.Formatting.Indented));
        if(!Baseline)yield return AdditionalChecks();yield return Motion();Clear();yield return null;yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle exit removes all enemy bodies and projectiles",FindObjectsOfType<EnemyBase>().Length==0&&FindObjectsOfType<BossBullet>().Length==0);
        if(!Baseline)Record("Battle exit removes owned revive render roots and meshes",Resources.FindObjectsOfTypeAll<Mesh>().Count(m=>m!=null&&(m.name=="boss_revive_glyphs"||m.name=="boss_revive_core"))==0);
        Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;
    }
    IEnumerator TableCase(int id,float ratio,int count,string name)
    {
        Clear();yield return null;var go=Owner(id);yield return null;var module=Revive(go);var eb=go.GetComponent<EnemyBase>();
        Record(name+": actual table revive ratio, delay and count remain",module.IsPassive&&Mathf.Approximately((float)Read(module,"_reviveHpPercent"),ratio)&&Mathf.Approximately((float)Read(module,"_reviveDelay"),1.5f)&&(int)Read(module,"_maxRevives")==count);
        int died=0;Action<EnemyDiedEvent> listener=e=>{if(e.enemy==eb)died++;};EventBus.Subscribe(listener);
        for(int round=0;round<count;round++)
        {
            float expected=Mathf.Ceil(eb.MaxHp*ratio),began=Time.time;eb.TakeDamage(100000);yield return null;
            Record(name+" "+round+": lethal damage hides body and physics without a kill event",eb.Hp<=0&&eb.preventPoolDeath&&!go.GetComponent<Collider2D>().enabled&&!go.GetComponent<Rigidbody2D>().simulated&&died==0);
            yield return new WaitForSeconds(.14f);if(round==0&&id==32021)yield return Snap("scatter");
            yield return new WaitForSeconds(.55f);if(round==0&&id==32021)yield return Snap("core");
            if(!Baseline)Record(name+" "+round+": real glyph copies and core survive the body hiding",View(go)!=null&&(bool)Read(View(go),"HasVisibleGeometry")&&(int)Read(View(go),"GlyphCount")>0);
            yield return new WaitForSeconds(.48f);if(round==0&&id==32021)yield return Snap("return");
            yield return Wait(()=>eb.Hp>0);float recoveredAt=Time.time-began;
            Record(name+" "+round+": original recovery delay and current-MaxHp formula remain",recoveredAt>=1.45f&&recoveredAt<1.8f&&Mathf.Approximately(eb.Hp,expected)&&Mathf.Approximately(eb.MaxHp,expected));
            Record(name+" "+round+": recovery restores collider and rigidbody simulation",go.GetComponent<Collider2D>().enabled&&go.GetComponent<Rigidbody2D>().simulated);
            yield return Wait(()=>ShieldActive(go));var shield=go.GetComponent<ResistShield>();float shieldAt=Time.time-began;
            Record(name+" "+round+": original recovery and protection lengths remain",shield!=null&&Mathf.Abs((float)Read(shield,"_duration")-.5f)<.001f&&shieldAt>=1.8f&&shieldAt<2.25f&&!eb.preventPoolDeath);
            float hp=eb.Hp;eb.TakeDamage(10,SkillId.AutoProjectileSword);float delta=hp-eb.Hp;
            Record(name+" "+round+(Baseline?": old cached-null shield leak reproduced":": actual revive shield blocks physical damage"),Baseline?delta>0:Mathf.Approximately(delta,0),"HP delta="+delta);
            if(!Baseline){hp=eb.Hp;eb.TakeDamage(10,SkillId.AutoProjectileTalisman);eb.TakeDamage(10,SkillId.ThrowGrenade);Record(name+" "+round+": actual shield also blocks energy and explosive damage",Mathf.Approximately(hp,eb.Hp));}
            if(round==0&&id==32021)yield return Snap("protected");
            yield return new WaitForSeconds(.62f);hp=eb.Hp;eb.TakeDamage(10,SkillId.AutoProjectileSword);
            Record(name+" "+round+": damage resumes after the original protection duration",!ShieldActive(go)&&hp>eb.Hp);
            observations.Add(new{caseName=name,round,id,ratio,expected,recoveredAt,shieldAt,shieldProbeDelta=delta,minimumRootScale=minimumScale});
        }
        eb.TakeDamage(100000);yield return null;Record(name+": final lethal hit really dies after configured charges are used",died==1&&!go.activeInHierarchy);EventBus.Unsubscribe(listener);
    }
    IEnumerator AdditionalChecks()
    {
        Clear();yield return null;var go=Owner(32021);yield return null;Revive(go);var eb=go.GetComponent<EnemyBase>();eb.TakeDamage(100000);yield return new WaitForSeconds(.3f);
        var view=View(go);var root=(Transform)Read(view,"RuntimeRoot");Time.timeScale=0;yield return new WaitForEndOfFrame();float progress=(float)Read(view,"WaitProgress");var meshes=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();var points=meshes.Select(m=>m.vertices).ToArray();yield return new WaitForSecondsRealtime(.2f);
        Record("Pause freezes actual glyph vertices, core progress and hidden physics",progress==(float)Read(view,"WaitProgress")&&points.Select((p,i)=>p.SequenceEqual(meshes[i].vertices)).All(v=>v)&&eb.Hp<=0&&!go.GetComponent<Rigidbody2D>().simulated);
        Time.timeScale=1;yield return Wait(()=>(float)Read(view,"WaitProgress")>=.97f);Time.timeScale=0;Call(view,"RenderNow");
        Record("The same glyph quads return to their original positions before HP recovery",eb.Hp<=0&&(int)Read(view,"GlyphCount")>0&&(float)Read(view,"ReturnError")<.001f);yield return Snap("gather-end");Time.timeScale=1;
        Time.timeScale=0;float returnProgress=(float)Read(view,"WaitProgress");var scatterData=new SerializedObject(view);var scatterField=scatterData.FindProperty("scatterDistanceMultiplier");float scatterMultiplier=scatterField.floatValue;
        scatterField.floatValue=1;scatterData.ApplyModifiedPropertiesWithoutUndo();Call(view,"SetWaitProgress",.3f);Call(view,"RenderNow");float originalDistance=GlyphTranslation(view);
        scatterField.floatValue=scatterMultiplier;scatterData.ApplyModifiedPropertiesWithoutUndo();Call(view,"RenderNow");float enlargedDistance=GlyphTranslation(view);
        Record("Scatter travels 1.6 times farther with the same glyph shape and timeline",originalDistance>.1f&&Mathf.Approximately(scatterMultiplier,1.6f)&&Mathf.Abs(enlargedDistance/originalDistance-scatterMultiplier)<.002f);
        Call(view,"SetWaitProgress",returnProgress);Call(view,"RenderNow");Record("Larger scatter still returns by the original 96-percent deadline",returnProgress>=.96f&&(float)Read(view,"ReturnError")<.001f&&eb.Hp<=0);Time.timeScale=1;
        yield return Wait(()=>ShieldActive(go));
        var label=go.GetComponentInChildren<EnemyWordLabel>();TextMeshPro original;label.TryGetWorldTextForShatter(out original);
        Record("Reconstructed glyphs share actual font materials and create no new TMP objects",root.GetComponentsInChildren<TMP_Text>(true).Length==0&&root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Glyphs_")).All(r=>original.textInfo.meshInfo.Any(m=>m.material==r.sharedMaterial)||r.sharedMaterial==original.fontSharedMaterial));
        Record("Recovery never shrinks or enlarges the physical root",minimumScale>=.999f&&go.transform.localScale==Vector3.one&&go.GetComponent<Collider2D>().enabled);
        Record("The protection shell follows the actual shield lifetime",(bool)Read(view,"ProtectionVisible")&&(int)Read(view,"ActiveGlyphRendererCount")==0&&original.renderer.enabled);
        float protectionAt=Time.time;yield return Wait(()=>!(bool)Read(view,"HasVisibleGeometry"),1.2f);Record("Shield expiry clears the closed shell and glyph copies",!(bool)Read(view,"ProtectionVisible")&&!(bool)Read(view,"HasVisibleGeometry")&&(int)Read(view,"ActiveGlyphRendererCount")==0&&Time.time-protectionAt<.95f,"elapsed="+(Time.time-protectionAt)+" phase="+Read(view,"Phase"));
        foreach(string mode in new[]{"pool-wait","pool-recover","pool-shield","disable","rebuild"})
        {
            Clear();yield return null;go=Owner(32081);yield return null;Revive(go);eb=go.GetComponent<EnemyBase>();eb.TakeDamage(100000);
            if(mode=="pool-recover")yield return Wait(()=>eb.Hp>0);
            else if(mode=="pool-shield")yield return Wait(()=>ShieldActive(go));else yield return new WaitForSeconds(.2f);
            if(mode.StartsWith("pool"))GameObjectPool.Release(go);else if(mode=="disable")go.SetActive(false);else{go.GetComponent<BossBrain>().OnEnemyDataReady();foreach(var m in Modules(go))m.cooldown=9999;}
            yield return new WaitForSeconds(2.1f);
            Record(mode+": cancellation leaves no delayed revive or active render root",View(go)!=null&&!(bool)Read(View(go),"HasVisibleGeometry")&&!(bool)Read(View(go),"ProtectionVisible")&&!eb.preventPoolDeath&&!ShieldActive(go));
            if(mode.StartsWith("pool"))
            {
                var reused=Owner(32081);yield return null;Revive(reused);var re=reused.GetComponent<EnemyBase>();float hp=re.Hp;re.TakeDamage(10,SkillId.AutoProjectileSword);
                Record(mode+": reused Boss has visible body, full collider and no stale immunity",reused==go&&hp>re.Hp&&reused.GetComponent<Rigidbody2D>().simulated&&reused.GetComponent<Collider2D>().enabled&&reused.GetComponentsInChildren<TextMeshPro>().All(t=>t.renderer.enabled));
            }
            if(mode=="disable")go.SetActive(true);
        }
        Clear();yield return null;go=Owner(32081);yield return null;Revive(go);var brain=go.GetComponent<BossBrain>();eb=go.GetComponent<EnemyBase>();var blade=Modules(go).OfType<BladeBurstModule>().Single();blade.Execute();blade.cooldown=9999;yield return new WaitForSeconds(.12f);eb.TakeDamage(100000);
        foreach(var m in Modules(go))if(!m.IsPassive)m.cooldown=0;go.GetComponent<EnemyAI>().enabled=true;
        yield return new WaitForSeconds(.7f);Record("Death cancels an already charging attack and blocks new hidden attacks",FindObjectsOfType<BossBullet>().Length==0&&go.GetComponentInChildren<BossBladeBurstPresentation>().MarkerCount==0);
        yield return Wait(()=>eb.Hp>0);yield return new WaitForSeconds(.12f);
        Record("Recovery keeps AI and new attacks gated through the visual handoff",(bool)Read(brain,"IsReviving")&&Vector2.Distance(go.transform.position,center)<.01f&&FindObjectsOfType<BossBullet>().Length==0);
        foreach(var m in Modules(go))m.cooldown=9999;go.GetComponent<EnemyAI>().enabled=false;yield return Wait(()=>ShieldActive(go));
        blade.cooldown=0;yield return Wait(()=>go.GetComponentInChildren<BossBladeBurstPresentation>().MarkerCount>0);
        Record("The normal table attack can start again after recovery",!(bool)Read(brain,"IsReviving")&&go.GetComponentInChildren<BossBladeBurstPresentation>().MarkerCount==16);yield return Snap("recovered-attack");
        foreach(int id in new[]{32021,32081})
        {
            Clear();yield return null;go=Owner(id);yield return null;var module=Revive(go);eb=go.GetComponent<EnemyBase>();int deaths=0;Action<EnemyDiedEvent> listener=e=>{if(e.enemy==eb)deaths++;};EventBus.Subscribe(listener);
            eb.TakeDamage(100000);yield return Wait(()=>eb.Hp>0);eb.TakeDamage(100000,SkillId.AutoProjectileSword);yield return null;
            if(id==32021)
            {
                yield return Wait(()=>eb.Hp>0);yield return Wait(()=>ShieldActive(go));Record("A second lethal recovery hit consumes exactly the next configured charge",deaths==0&&(int)Read(module,"_reviveCount")==2&&Mathf.Approximately(eb.MaxHp,500)&&go.activeInHierarchy);
            }
            else
            {yield return new WaitForSeconds(.8f);Record("A lethal recovery hit with no charges left really dies without a stale shield",deaths==1&&!go.activeInHierarchy&&!ShieldActive(go));}
            EventBus.Unsubscribe(listener);
        }
        Clear();yield return null;go=Owner(32021);yield return null;var repeat=Revive(go);eb=go.GetComponent<EnemyBase>();eb.TakeDamage(100000);yield return Wait(()=>ShieldActive(go));yield return new WaitForSeconds(.7f);
        root=(Transform)Read(View(go),"RuntimeRoot");var ids=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.GetInstanceID()).ToArray();int textures=Resources.FindObjectsOfTypeAll<Texture2D>().Length;var reusableShield=go.GetComponent<ResistShield>();
        eb.TakeDamage(100000);yield return Wait(()=>ShieldActive(go));
        Record("Repeated revives reuse glyph meshes and the inactive shield component",ids.SequenceEqual(root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.GetInstanceID()))&&go.GetComponent<ResistShield>()==reusableShield&&Resources.FindObjectsOfTypeAll<Texture2D>().Length==textures);
        Record("Revive allocates no legacy fragments, float labels, glow dots or textured shield rings",FindObjectsOfType<Transform>().All(t=>t.name!="TextFrag"&&t.name!="DeathFrag"&&t.name!="GlowDot"&&t.name!="ReviveText"&&t.name!="ReviveFlash"&&t.name!="ShieldRing"));
        Clear();yield return null;go=Owner(32021);yield return null;Revive(go);go.GetComponentInChildren<EnemyWordLabel>().SetWord(new string('霆',40));go.GetComponent<EnemyBase>().TakeDamage(100000);yield return new WaitForSeconds(.2f);
        Record("Long labels respect the 32-glyph snapshot budget",(int)Read(View(go),"GlyphCount")==32&&((Transform)Read(View(go),"RuntimeRoot")).GetComponentsInChildren<TMP_Text>(true).Length==0);
        Clear();yield return null;go=Owner(32081);yield return null;Revive(go);label=go.GetComponentInChildren<EnemyWordLabel>();label.TryGetWorldTextForShatter(out original);original.gameObject.SetActive(false);go.GetComponent<EnemyBase>().TakeDamage(100000);yield return new WaitForSeconds(.3f);
        Record("A body without an active TMP still has a bounded revive core",(int)Read(View(go),"GlyphCount")==0&&(bool)Read(View(go),"HasVisibleGeometry"));yield return Wait(()=>ShieldActive(go));Record("The no-glyph fallback still restores real health and protection",go.GetComponent<EnemyBase>().Hp>0&&ShieldActive(go));original.gameObject.SetActive(true);
        Clear();yield return null;var first=Owner(32081);first.transform.position+=Vector3.left*2;first.GetComponent<Rigidbody2D>().position=first.transform.position;yield return null;Revive(first);var second=Owner(32081);second.transform.position+=Vector3.right*2;second.GetComponent<Rigidbody2D>().position=second.transform.position;yield return null;Revive(second);
        first.GetComponent<EnemyBase>().TakeDamage(100000);second.GetComponent<EnemyBase>().TakeDamage(100000);yield return new WaitForSeconds(.7f);
        var firstRoot=(Transform)Read(View(first),"RuntimeRoot");var secondRoot=(Transform)Read(View(second),"RuntimeRoot");
        Record("Two real revivers own separate glyph meshes and distinct cores",firstRoot!=secondRoot&&(bool)Read(View(first),"HasVisibleGeometry")&&(bool)Read(View(second),"HasVisibleGeometry")&&Vector3.Distance((Vector3)Read(View(first),"CorePosition"),(Vector3)Read(View(second),"CorePosition"))>3);yield return Snap("two-revivers");
        Clear();yield return null;go=Owner(32081);yield return null;Revive(go);var limiter=SpawnLimiter.Instance.configs.First(c=>c.key==CombatVfxSpawner.SpawnLimiterKey);int maximum=limiter.maxAlive;bool recycle=limiter.recycleOldest;limiter.maxAlive=0;limiter.recycleOldest=false;go.GetComponent<EnemyBase>().TakeDamage(100000);yield return new WaitForSeconds(.3f);
        Record("Cosmetic saturation cannot hide the real revive state or reconstructed glyphs",(bool)Read(View(go),"HasVisibleGeometry")&&(int)Read(View(go),"ActiveGlyphRendererCount")>0);limiter.maxAlive=maximum;limiter.recycleOldest=recycle;
    }
    IEnumerator Motion()
    {
        Clear();yield return null;changeResolution?.Invoke(540,960);yield return new WaitForSecondsRealtime(.2f);var go=Owner(32081);yield return null;Revive(go);UnityEngine.Random.InitState(6147);
        var folder=Path.Combine(output,"revive-motion");Directory.CreateDirectory(Path.Combine(folder,"frames"));var times=new List<float>();float began=Time.realtimeSinceStartup;go.GetComponent<EnemyBase>().TakeDamage(100000);
        while(Time.realtimeSinceStartup-began<3.3f)
        {yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-began);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"frames","frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.055f);}
        Record("Actual lethal-damage video contains the complete recovery",times.Count>20&&go.activeInHierarchy&&go.GetComponent<EnemyBase>().Hp>0);
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{baseline=Baseline,frames=times.Count,elapsed=times.LastOrDefault(),timeScale=1,retimed=false}));changeResolution?.Invoke(1080,1920);
    }
    protected override void OnDestroy(){if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=originalRandom;}Application.runInBackground=originalBackground;base.OnDestroy();}
}
#endif
