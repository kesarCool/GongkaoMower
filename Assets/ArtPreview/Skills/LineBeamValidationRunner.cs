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

/// <summary>Isolated, real-Game-scene beam visuals and combat invariants. Never writes a skill asset.</summary>
public sealed class LineBeamValidationRunner : UIValidationRunnerBase
{
    bool Baseline=>SessionState.GetBool("LineBeamBaseline",false);
    protected override string OutputFolder {get {string custom=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return !string.IsNullOrEmpty(custom)?custom:Baseline?"LineBeamBefore":"LineBeamAfter";}}
    protected override string ValidationNote=>"Real Game scene, actual LineBeam definition, enemy001 colliders and damage. Isolated memory profile. Still captures freeze the exact cast frame; motion clip shows one prepared cast without changing configured cadence. Not a WeChat-device or build-size test.";
    PlayerSkills playerSkills;
    SkillLineBeam2D beam;
    LineBeamSkillDefinition definition;
    Transform player;
    readonly List<EnemyBase> enemies=new List<EnemyBase>();
    readonly Vector2 origin=new Vector2(15,10),direction=new Vector2(-.30f,.9539392f);
    readonly float[] distances={2.5f,4.65f,6.8f,9.5f};
    UnityEngine.Random.State originalRandom;
    float originalTimeScale;
    bool capturedState;
    string definitionBefore;

    Component Presentation=>player==null?null:player.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c=>c!=null&&c.GetType().Name=="LineBeamPresentation");
    static T Read<T>(object target,string name,T fallback=default(T))
    {
        if(target==null)return fallback;
        var type=target.GetType();var property=type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public);
        if(property!=null)return (T)property.GetValue(target,null);
        var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        return field!=null?(T)field.GetValue(target):fallback;
    }
    static bool Invoke(object target,string method,params object[] args)
    {
        if(target==null)return false;
        var fn=target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(fn==null)return false;fn.Invoke(target,args);return true;
    }
    SkillLineBeam2D Equip(int level,bool? enhancedOverride=null)
    {
        playerSkills.ClearAllSkills();
        var enhanced=typeof(PlayerSkills).GetField("enhancedBeamVisuals",BindingFlags.Instance|BindingFlags.Public);
        if(enhanced!=null)enhanced.SetValue(playerSkills,enhancedOverride??!Baseline);
        playerSkills.TryAddSkill(SkillId.LineBeam);
        var result=playerSkills.GetEquippedSkill<SkillLineBeam2D>(SkillId.LineBeam);
        if(result==null)return null;
        while(result.Level<level)result.OnLevelUp();definition.ApplyStatsToSkill(result,level);
        result.OnEquip(new SkillContext{player=player,enemyTag=playerSkills.enemyTag});beam=result;
        return result;
    }
    void PrepareTargets(bool isolated=false)
    {
        for(int i=0;i<enemies.Count;i++)
        {
            var enemy=enemies[i];enemy.gameObject.SetActive(!isolated||i==0);
            enemy.transform.position=origin+direction*distances[i];enemy.ApplyTableStats(1,10000,0);
        }
        player.position=origin;Physics2D.SyncTransforms();
    }
    void Cast()
    {
        Time.timeScale=1;beam.Tick(beam.interval+.01f);beam.TickVisual();
        Invoke(Presentation,"RenderNow");
    }
    IEnumerator Snapshot(string name)
    {
        // Freeze only the illustration instant; skill damage was already resolved by the real Tick.
        Time.timeScale=0;beam.TickVisual();Invoke(Presentation,"RenderNow");
        Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;
        Time.timeScale=1;
    }
    bool GeometryVisible()=>Baseline?beam.beamLines!=null&&beam.beamLines.Any(l=>l!=null&&l.enabled):Read(Presentation,"HasVisibleGeometry",false);
    Vector3 FirstWorldVertex(MeshRenderer renderer)
    {
        var filter=renderer.GetComponent<MeshFilter>();var vertices=filter.sharedMesh.vertices;
        return vertices.Length==0?Vector3.positiveInfinity:renderer.transform.TransformPoint(vertices[0]);
    }
    protected override IEnumerator RunChecks()
    {
        originalRandom=UnityEngine.Random.state;originalTimeScale=Time.timeScale;capturedState=true;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();
        foreach(var enemy in FindObjectsOfType<EnemyBase>())Destroy(enemy.gameObject);
        yield return null;CombatTargetRegistry.ClearAll();
        playerSkills=FindObjectOfType<PlayerSkills>();player=playerSkills.transform;
        playerSkills.enabled=false;playerSkills.ClearAllSkills();
        var controller=player.GetComponent<PlayerController>();if(controller!=null)controller.enabled=false;
        var rigidbody=player.GetComponent<Rigidbody2D>();if(rigidbody!=null){rigidbody.velocity=Vector2.zero;rigidbody.gravityScale=0;}
        playerSkills.attackMultiplier=1;playerSkills.attackRangeMul=1;playerSkills.critRate=0;playerSkills.penRate=0;
        player.position=origin;Camera.main.transform.position=new Vector3(15,14,-10);Camera.main.orthographicSize=11.5f;
        definition=AssetDatabase.LoadAssetAtPath<LineBeamSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LineBeam.asset");
        definitionBefore=JsonUtility.ToJson(definition);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab");
        UnityEngine.Random.InitState(4169);
        WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.SeededByLevelAndWave,101,2);
        var words=new[]{"北京烤鸭","炸酱面","扬州炒饭","牡丹燕菜"};
        for(int i=0;i<distances.Length;i++)
        {
            var go=Instantiate(prefab,origin+direction*distances[i],Quaternion.identity);
            foreach(var behavior in go.GetComponentsInChildren<MonoBehaviour>())if(!(behavior is EnemyBase)&&!(behavior is EnemyWordLabel)&&!(behavior is TMP_Text))behavior.enabled=false;
            var body=go.GetComponent<Rigidbody2D>();if(body!=null){body.bodyType=RigidbodyType2D.Kinematic;body.velocity=Vector2.zero;body.gravityScale=0;}
            var eb=go.GetComponent<EnemyBase>();eb.ApplyTableStats(1,10000,0);enemies.Add(eb);
            go.GetComponentInChildren<EnemyWordLabel>().SetWord(words[i]);
        }
        Time.timeScale=1;yield return null;PrepareTargets();
        Record("Real scene, player and definition loaded",playerSkills!=null&&definition!=null&&definition.beamLength==8&&enemies.Count==4);
        beam=Equip(1);Record("Only LineBeam is equipped in the fixture",beam!=null&&playerSkills.EquippedSkillCount==1);
        if(beam==null)yield break;
        bool triggers=Physics2D.queriesHitTriggers;
        float[] hp=enemies.Select(e=>e.Hp).ToArray();
        beam.Tick(beam.interval-.02f);
        Record("Original level-one interval prevents an early hit",enemies.Select((e,i)=>Mathf.Abs(e.Hp-hp[i])<.0001f).All(x=>x));
        beam.Tick(.03f);Invoke(Presentation,"RenderNow");
        Record("One cast penetrates every in-range collinear target once",Enumerable.Range(0,3).All(i=>Mathf.Abs(hp[i]-enemies[i].Hp-definition.DamageAt(1))<.005f));
        Record("Target beyond configured beam range remains unharmed",Mathf.Abs(enemies[3].Hp-hp[3])<.005f);
        Record("Physics trigger-query setting is restored",Physics2D.queriesHitTriggers==triggers);
        Record("Single beam geometry is visible on the cast frame",GeometryVisible());
        yield return Snapshot("single-1080x1920");
        if(!Baseline)
        {
            var visual=Presentation;Record("Enhanced presentation is attached",visual!=null);
            if(visual!=null)
            {
                var renderer=Read<MeshRenderer>(visual,"Renderer");
                Record("One shared-material mesh renders while legacy lines are disabled",renderer!=null&&renderer.sharedMaterials.Length==1&&player.GetComponentsInChildren<LineRenderer>(true).All(l=>!l.enabled)&&visual.GetComponents<MeshRenderer>().Length==1);
                Record("Impact feedback is bounded and follows actual hits",Read(visual,"ImpactCount",-1)>=1&&Read(visual,"ImpactCount",99)<=12);
                // Pause stops effect aging, but transforms must still be reprojected to maintain world-space shots.
                Time.timeScale=0;Invoke(visual,"RenderNow");int vertices=Read(visual,"VertexCount",0);
                Vector3 point=renderer!=null?FirstWorldVertex(renderer):Vector3.positiveInfinity;
                Vector2 castOrigin=Read(visual,"CastOrigin",Vector2.positiveInfinity);
                player.position+=(Vector3)new Vector2(.8f,-.3f);yield return null;Invoke(visual,"RenderNow");
                Record("Moving the player cannot drag an already-fired world beam",renderer!=null&&Vector3.Distance(point,FirstWorldVertex(renderer))<.01f&&Read(visual,"CastOrigin",Vector2.positiveInfinity)==castOrigin);
                yield return new WaitForSecondsRealtime(.18f);Invoke(visual,"RenderNow");
                Record("Pause freezes visual lifetime instead of dropping the beam",Read(visual,"HasVisibleGeometry",false)&&Read(visual,"VertexCount",-1)==vertices);
                int builds=Read(visual,"MeshBuildCount",-1);
                yield return null;Invoke(visual,"RenderNow");
                yield return null;Invoke(visual,"RenderNow");
                Record("A stationary paused beam avoids repeated mesh uploads",builds>=0&&Read(visual,"MeshBuildCount",-2)==builds&&Read(visual,"HasVisibleGeometry",false));
                player.position=origin;Time.timeScale=1;
            }
        }
        yield return new WaitForSecondsRealtime(.6f);beam.TickVisual();Invoke(Presentation,"RenderNow");
        Record("Beam and impact geometry retract after the transient window",!GeometryVisible());
        if(!Baseline)
        {
            PrepareTargets();beam=Equip(1);float chargeHp=enemies[0].Hp;
            beam.Tick(beam.interval-.08f);Time.timeScale=0;
            var visual=Presentation;Invoke(visual,"RenderNow");
            Vector2 chargeOrigin=Read(visual,"ChargeOrigin",Vector2.positiveInfinity),offset=new Vector2(.65f,.35f);
            player.position+=(Vector3)offset;yield return null;Invoke(visual,"RenderNow");
            Vector2 movedOrigin=Read(visual,"ChargeOrigin",Vector2.positiveInfinity);
            Record("Real cooldown anticipation follows a moving player before release",Read(visual,"HasCharge",false)
                &&Vector2.Distance(chargeOrigin,origin)<.01f&&Vector2.Distance(movedOrigin-chargeOrigin,offset)<.01f
                &&Mathf.Abs(enemies[0].Hp-chargeHp)<.005f);
            Invoke(visual,"Cancel");player.position=origin;Physics2D.SyncTransforms();Time.timeScale=1;
        }
        PrepareTargets(true);beam=Equip(1);float isolatedHp=enemies[0].Hp;Cast();
        Record("Isolated-target aim keeps the original damage",Mathf.Abs(isolatedHp-enemies[0].Hp-definition.DamageAt(1))<.005f);
        if(!Baseline)Record("Isolated-target shot renders exactly one beam",Read(Presentation,"ActiveBeamCount",-1)==1);
        yield return new WaitForSecondsRealtime(.4f);
        yield return WaitForPreviousFeedback();
        PrepareTargets();beam=Equip(5);UnityEngine.Random.InitState(34107);Cast();
        Record("Level-five normal cast keeps three configured rays",beam.beamCount==definition.BeamCountAt(5)&&definition.BeamCountAt(5)==3&&(Baseline||Read(Presentation,"ActiveBeamCount",-1)==3));
        yield return Snapshot("triple-1080x1920");yield return new WaitForSecondsRealtime(.4f);
        beam=Equip(5);PrepareTargets();UnityEngine.Random.InitState(4169);
        for(int i=0;i<4;i++){Cast();Invoke(Presentation,"Cancel");}
        yield return WaitForPreviousFeedback();
        Cast();
        int active=Baseline?beam.beamLines.Count(l=>l!=null&&l.enabled):Read(Presentation,"ActiveBeamCount",-1);
        Record("Existing fifth maximum-level cast still emits seven rainbow rays",active==definition.variantBeamCount&&definition.maxLevelVariantInterval==5&&definition.variantBeamCount==7);
        if(!Baseline)Record("Seven-ray geometry remains bounded",Read(Presentation,"VertexCount",99999)>0&&Read(Presentation,"VertexCount",99999)<=4096&&Read(Presentation,"ImpactCount",99)<=12);
        yield return Snapshot("rainbow-1080x1920");yield return new WaitForSecondsRealtime(.4f);
        if(!Baseline&&Presentation!=null)
        {
            var visual=Presentation;var renderer=Read<MeshRenderer>(visual,"Renderer");
            int children=visual.GetComponentsInChildren<Transform>(true).Length,material=renderer.sharedMaterial.GetInstanceID();
            for(int i=0;i<24;i++){Cast();Invoke(visual,"RenderNow");}
            Record("Repeated casts reuse visual objects and the same material",visual.GetComponentsInChildren<Transform>(true).Length==children&&renderer.sharedMaterial.GetInstanceID()==material&&renderer.sharedMaterials.Length==1);
            beam.OnUnequip();Invoke(visual,"RenderNow");
            Record("Unequipping immediately cancels all beam effects",!Read(visual,"HasVisibleGeometry",true));
            PrepareTargets();beam=Equip(1,false);Cast();
            var legacy=player.GetComponentsInChildren<LineRenderer>(true);
            bool legacyRendered=legacy.Length>0&&legacy.Any(l=>l.enabled);
            beam=Equip(1,true);Cast();
            Record("A real legacy-to-enhanced transition disables the retained legacy lines",legacyRendered&&legacy.All(l=>!l.enabled)&&Read(Presentation,"ActiveBeamCount",-1)==1);
            ValidateVisualRandomness();
        }
        yield return RecordMotion();
        Record("Skill definition remains unchanged by preview",definitionBefore==JsonUtility.ToJson(definition));
        foreach(var enemy in enemies)if(enemy!=null)Destroy(enemy.gameObject);enemies.Clear();
        playerSkills.ClearAllSkills();Time.timeScale=originalTimeScale;UnityEngine.Random.state=originalRandom;
    }
    void ValidateVisualRandomness()
    {
        var visual=Presentation;bool stable=true,available=true;
        void Probe(string method,params object[] args)
        {
            var before=UnityEngine.Random.state;available&=Invoke(visual,method,args);
            stable&=before.Equals(UnityEngine.Random.state);
        }
        // API-only isolation probe: no screenshot is taken and Cancel runs before any camera frame.
        // The motion proof below exclusively uses the real skill Tick and real hit resolution.
        Probe("Configure",playerSkills.beamVisualWidth,playerSkills.beamSortingOrder,playerSkills.beamVisualDuration);
        Probe("BeginCharge",origin,direction,.08f);Probe("RenderNow");Probe("CancelCharge");
        Probe("BeginCast",origin,7,true);
        for(int i=0;i<7;i++)Probe("SetBeam",i,origin,direction,definition.beamLength);
        foreach(var enemy in enemies)Probe("AddImpact",(Vector2)enemy.transform.position);
        Probe("RenderNow");Probe("Cancel");
        var beforeTick=UnityEngine.Random.state;beam.TickVisual();stable&=beforeTick.Equals(UnityEngine.Random.state);
        Record("Charge, cast, beam, impact, rebuild and cancellation APIs preserve combat random state",available&&stable);
    }
    [Serializable]sealed class MotionCapture
    {
        public bool passed,fired,actualHit,finished,timedOut;
        public int frameCount,visibleFrames;
        public float castAtSeconds=-1;
        public string error="";
        [NonSerialized]public readonly List<float> times=new List<float>();
    }
    IEnumerator WaitForPreviousFeedback()
    {
        // Real prefab lifetime is .75 s (DamageFloatText), and BurstVfx defaults to .35 s.
        // Let existing feedback complete its own scaled-time animation and pool release;
        // never disable the presenter or remove feedback generated by the next real cast.
        float lifetime=0;
        foreach(var text in FindObjectsOfType<DamageFloatText>())lifetime=Mathf.Max(lifetime,Read(text,"lifetime",.75f));
        foreach(var burst in FindObjectsOfType<BurstVfx>())lifetime=Mathf.Max(lifetime,burst.duration);
        if(lifetime<=0)yield break;
        Time.timeScale=1;
        yield return new WaitForSeconds(Mathf.Min(.95f,lifetime+.08f));
        yield return null;
    }
    IEnumerator RecordMotion()
    {
        var state=new MotionCapture();
        Directory.CreateDirectory(Path.Combine(output,"frames"));
        // Invalidate any previous clip before starting, including when a capture later times out.
        File.WriteAllText(Path.Combine(output,"motion-validation.json"),JsonUtility.ToJson(state,true));
        yield return WaitForPreviousFeedback();
        var recorder=StartCoroutine(CaptureMotionSafely(state));float deadline=Time.realtimeSinceStartup+15;
        // This outer coroutine keeps polling even if the capture waits forever for a Game View frame.
        while(!state.finished&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!state.finished){StopCoroutine(recorder);state.timedOut=true;state.error="Motion capture exceeded its 15-second watchdog.";}
        state.passed=state.finished&&!state.timedOut&&state.error==""&&state.fired&&state.actualHit&&state.visibleFrames>0;
        Record("Motion records a real damaging cast and at least one visible post-cast frame",state.passed,
            "Visible cast frames: "+state.visibleFrames+"; total frames: "+state.frameCount+"; "+state.error);
        File.WriteAllText(Path.Combine(output,"frame-times.json"),"["+string.Join(",",state.times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");
        File.WriteAllText(Path.Combine(output,"motion-validation.json"),JsonUtility.ToJson(state,true));
        File.WriteAllText(Path.Combine(output,"motion-note.txt"),"Actual runtime: one cast prepared in the last 80 ms of its unchanged cooldown, then advanced by actual scaled elapsed time. Captures include anticipation, real damage and transient beam; no artificial effect frames or accelerated repeated attacks. Watchdog: 15 seconds. Passed: "+state.passed);
    }
    IEnumerator CaptureMotionSafely(MotionCapture state)
    {
        var flow=CaptureMotionFrames(state);
        while(true)
        {
            bool next;object current=null;
            try{next=flow.MoveNext();if(next)current=flow.Current;}
            catch(Exception ex){state.error=ex.ToString();break;}
            if(!next)break;yield return current;
        }
        state.finished=true;
    }
    IEnumerator CaptureMotionFrames(MotionCapture state)
    {
        PrepareTargets();beam=Equip(1);Time.timeScale=1;
        // Record only the unchanged cooldown's final 80 ms; subsequent advancement is real scaled time.
        beam.Tick(beam.interval-.08f);
        float preparedAt=Time.time,started=Time.realtimeSinceStartup,castTime=-1,castReal=-1,hp=enemies[0].Hp;
        int frame=0;
        while(frame<64)
        {
            if(!state.fired&&Time.time-preparedAt>=.08f)
            {
                beam.Tick(Time.time-preparedAt);state.fired=true;castTime=Time.time;castReal=Time.realtimeSinceStartup;
                state.castAtSeconds=castReal-started;
                state.actualHit=Mathf.Abs(hp-enemies[0].Hp-definition.DamageAt(1))<.005f;
            }
            beam.TickVisual();Invoke(Presentation,"RenderNow");
            yield return new WaitForEndOfFrame();state.times.Add(Time.realtimeSinceStartup-started);
            if(state.fired&&GeometryVisible())state.visibleFrames++;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"frames","frame-"+frame.ToString("D4")+".png"));frame++;
            state.frameCount=frame;
            if(state.fired&&Time.time-castTime>=.5f&&Time.realtimeSinceStartup-castReal>=.65f)break;
            yield return new WaitForSecondsRealtime(.025f);
        }
    }
    protected override void OnDestroy()
    {
        if(capturedState){Time.timeScale=originalTimeScale;UnityEngine.Random.state=originalRandom;}
        base.OnDestroy();
    }
}
#endif
