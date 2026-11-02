#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SwordPresentationValidationRunner : UIValidationRunnerBase
{
    bool Baseline=>SessionState.GetBool("SwordPresentationBaseline",false);
    protected override string OutputFolder {get {string p=SessionState.GetString("SkillVfxToolkit.OutputFolder","");return string.IsNullOrEmpty(p)?"SwordPresentationV1/"+(Baseline?"Before":"After"):p;}}
    protected override string ValidationNote=>"Real Game scene, actual configured sword volleys, orbit/flight and collision damage. In-memory save only. Burst fixture removes the concurrent straight volley to isolate orbit damage. Screenshots freeze actual projectiles; triggered hit PS is sampled at .055s. Motion uses captured timestamps, not a device FPS test.";
    const string DefinitionPath="Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileSword.asset";
    AutoProjectileSkillDefinition definition;PlayerSkills skills;Transform player;EnemyBase enemy;
    readonly Vector2 origin=new Vector2(15,10),target=new Vector2(15,16);
    UnityEngine.Random.State randomState;float originalScale;bool saved,originalBackground;string splitSnapshot;
    void Awake() {originalBackground=SessionState.GetBool("SwordValidation.previousBackground",Application.runInBackground);Application.runInBackground=true;}
    PlayerBullet[] Bullets()=>FindObjectsOfType<PlayerBullet>().Where(b=>b.skillSource==SkillId.AutoProjectileSword).ToArray();
    AutoProjectileBurstBullet[] BurstBullets()=>Bullets().OfType<AutoProjectileBurstBullet>().ToArray();
    PooledCombatVfx[] Hits()=>FindObjectsOfType<PooledCombatVfx>().Where(v=>v.name.StartsWith("fx_sword_hit_v1",StringComparison.Ordinal)).ToArray();
    void ReleaseBullets() {foreach(var b in Bullets()) {SpawnLimiter.Instance?.Unregister("Bullet",b.gameObject);GameObjectPool.Release(b.gameObject);}}
    void ResetTarget(Vector2 shift,bool burst=false)
    {
        ReleaseBullets();player.position=origin+shift;enemy.transform.position=(burst?origin+new Vector2(1.2f,4):target)+shift;enemy.ApplyTableStats(1,10000,0);enemy.GetComponent<Collider2D>().enabled=true;
        Camera.main.transform.position=new Vector3(15+shift.x,13+shift.y,-10);Physics2D.SyncTransforms();skills.attackMultiplier=1;skills.attackRangeMul=1;skills.critRate=0;skills.penRate=0;skills.pierceCount=0;skills.pierceRate=0;
    }
    SkillAutoProjectileSword Equip(int level)
    {
        skills.ClearAllSkills();skills.TryAddSkill(SkillId.AutoProjectileSword);var s=skills.GetEquippedSkill<SkillAutoProjectileSword>(SkillId.AutoProjectileSword);
        while(s.Level<level) s.OnLevelUp();definition.ApplyStatsToSkill(s,level);s.OnEquip(new SkillContext {player=player,enemyTag=skills.enemyTag});return s;
    }
    IEnumerator Snap(string name,PooledCombatVfx hit=null)
    {
        Time.timeScale=0;if(hit!=null) hit.GetComponent<ParticleSystem>().Simulate(.055f,true,true,false);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;Time.timeScale=1;
    }
    protected override IEnumerator RunChecks()
    {
        randomState=UnityEngine.Random.state;originalScale=Time.timeScale;saved=true;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var s in FindObjectsOfType<SpawnerWaves>()) {s.StopAllCoroutines();s.enabled=false;}
        foreach(var o in FindObjectsOfType<BattleOutcomeCoordinator>()) {o.StopAllCoroutines();o.enabled=false;}
        foreach(var f in FindObjectsOfType<CameraFollow2D>()) f.enabled=false;
        UIManager.Instance.CloseAllStack();foreach(var e in FindObjectsOfType<EnemyBase>()) Destroy(e.gameObject);yield return null;CombatTargetRegistry.ClearAll();
        skills=FindObjectOfType<PlayerSkills>();player=skills.transform;skills.enabled=false;skills.ClearAllSkills();var controller=player.GetComponent<PlayerController>();if(controller!=null)controller.enabled=false;
        var rb=player.GetComponent<Rigidbody2D>();rb.velocity=Vector2.zero;rb.gravityScale=0;Camera.main.orthographicSize=11.5f;
        definition=AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>(DefinitionPath);splitSnapshot=EditorJsonUtility.ToJson(definition.maxLevelPrefab.GetComponent<CritSplitOnHit>());
        var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab"),target,Quaternion.identity);
        foreach(var b in go.GetComponentsInChildren<MonoBehaviour>()) if(!(b is EnemyBase) && !(b is EnemyWordLabel) && !(b is TMP_Text)) b.enabled=false;
        var body=go.GetComponent<Rigidbody2D>();if(body==null)body=go.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.velocity=Vector2.zero;body.gravityScale=0;
        go.GetComponentInChildren<EnemyWordLabel>().SetWord("刀光剑影");enemy=go.GetComponent<EnemyBase>();Time.timeScale=1;yield return null;
        Record("Sword definition stays byte-equivalent to the pre-task backup",File.ReadAllBytes(DefinitionPath).SequenceEqual(File.ReadAllBytes("ArtOutput/VFX/SwordPresentationV1/Baseline/"+DefinitionPath)));
        Record("All original collision radii remain .30",new[]{definition.bulletPrefab,definition.maxLevelPrefab,definition.burstBulletPrefab}.All(p=>Mathf.Approximately(p.GetComponent<CircleCollider2D>().radius,.3f)));
        yield return Shot("normal",1,Vector2.zero);yield return Shot("max",5,Vector2.zero);yield return Shot("grass-max",5,new Vector2(-12,3));
        yield return OrbitShot();yield return Motion("normal-motion",1,2.2f,false);yield return Motion("burst-motion",5,3.2f,false);yield return Motion("legend-motion",5,3.2f,true);
        yield return ProgressionAndSplitChecks();if(!Baseline) {yield return PresentationChecks();yield return HitAnchorChecks();}
        ReleaseBullets();skills.ClearAllSkills();yield return SceneManager.UnloadSceneAsync("Game");yield return null;
        Record("Battle exit clears sword projectiles and pooled hit effects",Bullets().Length==0 && Hits().Length==0);
        Time.timeScale=originalScale;UnityEngine.Random.state=randomState;
    }
    IEnumerator Shot(string name,int level,Vector2 shift)
    {
        ResetTarget(shift);var s=Equip(level);s.Tick(s.interval-.02f);Record(name+": no early shot or early damage",Bullets().Length==0 && enemy.Hp==10000);
        Time.timeScale=.25f;s.Tick(.03f);Record(name+": original projectile count and speed",Bullets().Length==definition.ProjectileCountAt(level) && Bullets().All(b=>Mathf.Approximately(b.speed,definition.bulletSpeed)));
        yield return new WaitForSeconds(.12f);Record(name+": flying swords do not damage before contact",enemy.Hp==10000);
        if(!Baseline)Record(name+": real flight builds a short trail",Bullets().SelectMany(b=>b.GetComponentsInChildren<TrailRenderer>()).Any(t=>t.positionCount>1));
        yield return Snap(name+"-flight");float deadline=Time.realtimeSinceStartup+5;while(enemy.Hp==10000 && Time.realtimeSinceStartup<deadline) yield return null;
        Record(name+": actual collision damages the target",enemy.Hp<10000);Record(name+": new hit feedback follows the collision",Baseline?Hits().Length==0:Hits().Length>0);
        yield return new WaitForEndOfFrame();
        if(!Baseline)Record(name+": collision feedback touches a visible character",Hits().Any(h=>PointOnWord(h.transform.position)));
        yield return Snap(name+"-hit",Hits().FirstOrDefault());yield return new WaitForSeconds(.6f);
        float expected=definition.DamageAt(level)*definition.ProjectileCountAt(level);Record(name+": original full volley damage",Mathf.Abs(10000-enemy.Hp-expected)<.02f,"Actual="+(10000-enemy.Hp)+" expected="+expected);
        Record(name+": flight and short impact return independently",Bullets().Length==0 && Hits().Length==0);skills.ClearAllSkills();yield return null;
    }
    IEnumerator OrbitShot()
    {
        ResetTarget(Vector2.zero,true);var s=Equip(5);s.Tick(s.burstCooldown+.001f);
        Record("Original maximum level spawns eight orbiting swords",BurstBullets().Length==definition.burstCount && BurstBullets().All(b=>!b.GetComponent<Collider2D>().enabled && b.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Kinematic));
        yield return new WaitForSeconds(.16f);
        // Remove the simultaneous regular volley before contact, leaving real orbiting swords for this fixture.
        foreach(var b in Bullets().Where(b=>!(b is AutoProjectileBurstBullet))) {SpawnLimiter.Instance?.Unregister("Bullet",b.gameObject);GameObjectPool.Release(b.gameObject);}
        enemy.ApplyTableStats(1,10000,0);yield return new WaitForSeconds(.3f);yield return Snap("burst-ring");
        Record("Orbit stage follows the authored radius without dealing damage",enemy.Hp==10000 && BurstBullets().All(b=>Mathf.Abs(Vector2.Distance(b.transform.position,player.position)-definition.burstOrbitRadius)<.02f));
        var poses=BurstBullets().Select(b=>b.transform.position).ToArray();Time.timeScale=0;yield return new WaitForSecondsRealtime(.13f);
        Record("Pause freezes real orbit positions",poses.SequenceEqual(BurstBullets().Select(b=>b.transform.position)));Time.timeScale=1;
        float deadline=Time.realtimeSinceStartup+4;while(BurstBullets().Any(b=>!b.GetComponent<Collider2D>().enabled) && Time.realtimeSinceStartup<deadline)yield return null;
        Record("Orbit completes into original tangential penetrating flight",BurstBullets().Length==definition.burstCount && BurstBullets().All(b=>b.GetComponent<Collider2D>().enabled && b.FullScreenPenetration && b.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Dynamic));
        yield return Snap("burst-flight");while(enemy.Hp==10000 && Time.realtimeSinceStartup<deadline)yield return null;
        Record("An actual orbit sword hits for the original damage quantum",enemy.Hp<10000 && Mathf.Abs((10000-enemy.Hp)/definition.DamageAt(5)-Mathf.Round((10000-enemy.Hp)/definition.DamageAt(5)))<.02f);
        if(!Baseline)Record("Orbit flight uses the shared hit stamp",Hits().Length>0);
        yield return Snap("burst-hit",Hits().FirstOrDefault());ReleaseBullets();skills.ClearAllSkills();yield return new WaitForSeconds(.4f);
    }
    IEnumerator Motion(string name,int level,float duration,bool legend)
    {
        changeResolution?.Invoke(540,960);yield return null;yield return new WaitForSecondsRealtime(.25f);ResetTarget(Vector2.zero);var s=Equip(level);
        if(legend)s.ApplyLegendBreakthrough(2);
        if(level==5){s.Tick(s.burstCooldown-1.1f);ReleaseBullets();enemy.ApplyTableStats(1,10000,0);}
        string folder=Path.Combine(output,name),frames=Path.Combine(folder,"frames");Directory.CreateDirectory(frames);
        var times=new List<float>();float start=Time.realtimeSinceStartup,last=Time.time;int maxBullets=0,maxOrbit=0,maxHits=0;
        while(Time.realtimeSinceStartup-start<duration && times.Count<150)
        {
            float dt=Time.time-last;last=Time.time;s.Tick(dt);maxBullets=Mathf.Max(maxBullets,Bullets().Length);maxOrbit=Mathf.Max(maxOrbit,BurstBullets().Length);maxHits=Mathf.Max(maxHits,Hits().Length);
            yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);ScreenCapture.CaptureScreenshot(Path.Combine(frames,"frame-"+(times.Count-1).ToString("D4")+".png"));yield return new WaitForSecondsRealtime(.04f);
        }
        Record(name+": motion records real configured firing and damage",enemy.Hp<10000 && times.Count>10 && (level!=5 || maxOrbit==(legend?definition.burstCount*2:definition.burstCount)));
        File.WriteAllText(Path.Combine(folder,"frame-times.json"),"["+string.Join(",",times.Select(t=>t.ToString(CultureInfo.InvariantCulture)))+"]");File.WriteAllText(Path.Combine(folder,"motion.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {baseline=Baseline,level,legend,damaged=enemy.Hp<10000,frames=times.Count,elapsed=times.Last(),maxBullets,maxOrbit,maxHits}));
        ReleaseBullets();skills.ClearAllSkills();yield return new WaitForSeconds(.4f);changeResolution?.Invoke(1080,1920);yield return null;
    }
    IEnumerator ProgressionAndSplitChecks()
    {
        ResetTarget(Vector2.zero);var s=Equip(1);bool upgraded=true;for(int i=0;i<3;i++)upgraded &= skills.TryLevelUp(SkillId.AutoProjectileSword);
        Record("Real sword progression reaches level four",upgraded && s.Level==4);Record("Original bonded-passive gate blocks level five",!skills.TryLevelUp(SkillId.AutoProjectileSword) && s.Level==4);
        Record("Bonded passive unlocks the existing level-five sword",skills.TryAddPassive(definition.bondedPassiveId) && skills.TryLevelUp(SkillId.AutoProjectileSword) && s.Level==5);
        s.ApplyLegendBreakthrough(2);Record("Legend retains six regular swords, sixteen burst swords and two pierces",s.projectileCount==definition.ProjectileCountAt(5)+1 && s.burstCount==definition.burstCount*2 && skills.pierceCount==2);
        definition.ApplyStatsToSkill(s,5);s.OnAfterStatsApplied();Record("Stats refresh preserves original Legend counts",s.projectileCount==definition.ProjectileCountAt(5)+1 && s.burstCount==definition.burstCount*2 && skills.pierceCount==2);
        skills.critRate=1;s.Tick(s.interval+.001f);float finalDamage=Bullets().First().damage;float deadline=Time.realtimeSinceStartup+5;
        while(enemy.Hp==10000 && Time.realtimeSinceStartup<deadline)yield return null;Record("Legend critical swords still deal real collision damage",enemy.Hp<10000);
        enemy.GetComponent<Collider2D>().enabled=false;ReleaseBullets();yield return new WaitForSeconds(.23f);
        var split=Bullets().Where(b=>Mathf.Approximately(b.lifetime,1.2f)).ToArray();
        Record("Critical collision still creates three original split swords",split.Length==3 && split.All(b=>Mathf.Abs(b.damage-finalDamage*.4f)<.001f));
        if(!Baseline)Record("Split swords inherit ordinary art and hit feedback",split.All(b=>b.GetComponent<ProjectileHitFeedback>()!=null && b.GetComponentInChildren<SpriteRenderer>().sprite.name=="sword_qingfeng_v1"));
        yield return Snap("critical-split");ReleaseBullets();enemy.GetComponent<Collider2D>().enabled=true;skills.ClearAllSkills();yield return new WaitForSeconds(.4f);
    }
    IEnumerator PresentationChecks()
    {
        var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Res/Textures/VFX/sheet_sword_presentation_v1.png");
        Record("Two blade states and eight hit frames use one 512x256 atlas without mips or CPU copy",atlas.width==512 && atlas.height==256 && !atlas.isReadable && atlas.mipmapCount==1);
        Record("Source atlas PNG stays within 96 KiB budget",new FileInfo(AssetDatabase.GetAssetPath(atlas)).Length<=96*1024);
        var templates=new[]{definition.bulletPrefab,definition.maxLevelPrefab,definition.burstBulletPrefab};var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/Materials/VFX/mat_sword_trail_v1.mat");
        Record("All sword variants opt into hit feedback and the same atlas",templates.All(p=>p.GetComponent<ProjectileHitFeedback>()!=null && p.GetComponentInChildren<SpriteRenderer>().sprite.texture==atlas));
        Record("Maximum and burst use a dedicated double-edge silhouette",definition.bulletPrefab.GetComponentInChildren<SpriteRenderer>().sprite!=definition.maxLevelPrefab.GetComponentInChildren<SpriteRenderer>().sprite && definition.maxLevelPrefab.GetComponentInChildren<SpriteRenderer>().sprite==definition.burstBulletPrefab.GetComponentInChildren<SpriteRenderer>().sprite);
        Record("Every sword uses one short, tapered, soft-edged trail",templates.All(p=>p.GetComponentsInChildren<TrailRenderer>().Length==1 && p.GetComponentInChildren<TrailRenderer>().sharedMaterial==material && p.GetComponentInChildren<TrailRenderer>().time<=.1f && p.GetComponentInChildren<TrailRenderer>().widthCurve.Evaluate(1)==0));
        Record("Sword layers remain below word enemies",templates.All(p=>p.GetComponentInChildren<SpriteRenderer>().sortingOrder==-3 && p.GetComponentInChildren<TrailRenderer>().sortingOrder==-4));
        var pistol=AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectilePistol.asset");Record("Other projectile types do not gain sword feedback",pistol.bulletPrefab.GetComponent<ProjectileHitFeedback>()==null);
        foreach(var prefab in templates)
        {
            var probe=GameObjectPool.Get(prefab,origin,Quaternion.identity);var trail=probe.GetComponentInChildren<TrailRenderer>();trail.AddPosition(origin);trail.AddPosition(origin+Vector2.up);
            GameObjectPool.Release(probe);Record(prefab.name+": pool release clears and disables trail",trail.positionCount==0 && !trail.emitting);
            var reused=GameObjectPool.Get(prefab,origin+Vector2.right*8,Quaternion.identity);Record(prefab.name+": reuse never bridges the old position",reused==probe && trail.positionCount<=1 && trail.emitting);GameObjectPool.Release(reused);
        }
        ResetTarget(Vector2.zero);var random=UnityEngine.Random.state;var feedback=GameObjectPool.Get(definition.bulletPrefab,target,Quaternion.identity);feedback.GetComponent<ProjectileHitFeedback>().Play(enemy.GetComponent<Collider2D>());
        Record("Hit feedback consumes no combat RNG",random.Equals(UnityEngine.Random.state));GameObjectPool.Release(feedback);yield return new WaitForSeconds(.4f);
        var hitPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Prefabs/Effects/Impact/fx_sword_hit_v1.prefab");CombatVfxSpawner.TryPlayPooled(hitPrefab,target,Quaternion.identity,.34f);yield return null;var hit=Hits().First();Time.timeScale=0;yield return null;float time=hit.GetComponent<ParticleSystem>().time;yield return new WaitForSecondsRealtime(.15f);
        Record("Pause freezes the hit animation and pool timer",hit.gameObject.activeSelf && Mathf.Abs(hit.GetComponent<ParticleSystem>().time-time)<.001f);Time.timeScale=1;yield return new WaitForSeconds(.4f);Record("Resume returns the hit effect",Hits().Length==0);
        yield return null;int accepted=0;for(int i=0;i<40;i++)if(CombatVfxSpawner.TryPlayPooled(hitPrefab,target,Quaternion.identity))accepted++;
        Record("Dense sword impacts keep the existing shared VFX cap",accepted>0 && accepted<=8 && Hits().Length<=32);yield return new WaitForSeconds(.4f);Record("Dense impacts leave no active stamp",Hits().Length==0);
    }
    bool PointOnWord(Vector2 point)
    {
        var text=enemy.GetComponentInChildren<TMP_Text>();var local=text.transform.InverseTransformPoint(point);
        for(int i=0;i<text.textInfo.characterCount;i++)
        {
            var c=text.textInfo.characterInfo[i];
            if(c.isVisible && local.x>=c.bottomLeft.x-.01f && local.x<=c.topRight.x+.01f && local.y>=c.bottomLeft.y-.01f && local.y<=c.topRight.y+.01f)return true;
        }
        return false;
    }
    IEnumerator HitAnchorChecks()
    {
        var label=enemy.GetComponentInChildren<EnemyWordLabel>();var text=enemy.GetComponentInChildren<TMP_Text>();var initialTextPosition=text.transform.localPosition;
        var talisman=AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset");
        var cases=new[]{definition.bulletPrefab,definition.maxLevelPrefab,definition.burstBulletPrefab,talisman.bulletPrefab,talisman.maxLevelPrefab,talisman.burstBulletPrefab};
        foreach(var prefab in cases)
        {
            ResetTarget(Vector2.zero);text.transform.localPosition=initialTextPosition;Time.timeScale=.1f;
            var projectile=GameObjectPool.Get(prefab,origin,Quaternion.identity);bool played=projectile.GetComponent<ProjectileHitFeedback>().Play(enemy.GetComponent<Collider2D>());GameObjectPool.Release(projectile);
            yield return new WaitForEndOfFrame();var hit=FindObjectsOfType<PooledCombatVfx>().FirstOrDefault(v=>v.name.StartsWith(prefab.name.Contains("Sword")?"fx_sword_hit_v1":"fx_talisman_hit",StringComparison.Ordinal));
            Record(prefab.name+": shared impact begins on a visible character",played && hit!=null && PointOnWord(hit.transform.position));
            if(hit==null)continue;
            Vector3 local=text.transform.InverseTransformPoint(hit.transform.position);float z=hit.transform.position.z;
            enemy.transform.position+=new Vector3(.35f,.12f,0);text.transform.localPosition+=new Vector3(.06f,.05f,0);Physics2D.SyncTransforms();
            yield return new WaitForEndOfFrame();Vector3 expected=text.transform.TransformPoint(local);
            Record(prefab.name+": impact follows moving word and hit punch",Vector2.Distance(hit.transform.position,expected)<.01f && Mathf.Approximately(hit.transform.position.z,z));
            if(prefab==definition.bulletPrefab) {yield return Snap("moving-word-hit",hit);Time.timeScale=.1f;}
            Time.timeScale=0;Vector3 frozen=hit.transform.position;enemy.transform.position+=Vector3.right*.15f;yield return new WaitForEndOfFrame();
            Record(prefab.name+": pause freezes the visual anchor",hit.transform.position==frozen);Time.timeScale=.1f;yield return new WaitForEndOfFrame();
            Record(prefab.name+": resume updates the visual anchor",Vector2.Distance(hit.transform.position,text.transform.TransformPoint(local))<.01f);
            Vector3 previous=hit.transform.position;enemy.gameObject.SetActive(false);enemy.transform.position+=Vector3.right*6;enemy.gameObject.SetActive(true);yield return new WaitForEndOfFrame();
            Record(prefab.name+": same-frame enemy reuse never carries the old impact",hit.transform.position==previous && hit.transform.parent!=enemy.transform);
            Time.timeScale=1;yield return new WaitForSeconds(.7f);Record(prefab.name+": followed impact still returns on time",!hit.gameObject.activeSelf);
        }
        text.transform.localPosition=initialTextPosition;ResetTarget(Vector2.zero);
        var groundPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Prefabs/Effects/Impact/fx_sword_hit_v1.prefab");Vector3 ground=new Vector3(14,15,-.15f);
        CombatVfxSpawner.TryPlayPooled(groundPrefab,ground,Quaternion.identity);yield return new WaitForEndOfFrame();var fresh=Hits().First();enemy.transform.position+=Vector3.right*2;yield return new WaitForEndOfFrame();
        Record("Reused pooled effect clears old attachment for a fixed-world spawn",fresh.transform.position==ground);yield return new WaitForSeconds(.4f);
        ResetTarget(Vector2.zero);enemy.ApplyTableStats(1,1,0);enemy.preventPoolDeath=true;Time.timeScale=.1f;
        var lethal=GameObjectPool.Get(definition.bulletPrefab,origin,Quaternion.identity);lethal.GetComponent<ProjectileHitFeedback>().Play(enemy.GetComponent<Collider2D>());GameObjectPool.Release(lethal);yield return new WaitForEndOfFrame();var stamp=Hits().First();Vector3 contact=stamp.transform.position;
        enemy.TakeDamage(2,SkillId.AutoProjectileSword);enemy.transform.position+=Vector3.right*3;yield return new WaitForEndOfFrame();
        Record("Lethal hit freezes the stamp even when revive keeps the object active",enemy.Hp<=0 && stamp.transform.position==contact);
        enemy.preventPoolDeath=false;enemy.ApplyTableStats(1,10000,0);enemy.ShowFromRevive();Time.timeScale=1;yield return new WaitForSeconds(.4f);
    }
    protected override void OnDestroy()
    {
        if(saved){Time.timeScale=originalScale;UnityEngine.Random.state=randomState;}
        if(definition!=null && !string.IsNullOrEmpty(splitSnapshot))EditorJsonUtility.FromJsonOverwrite(splitSnapshot,definition.maxLevelPrefab.GetComponent<CritSplitOnHit>());
        Application.runInBackground=originalBackground;base.OnDestroy();
    }
}
#endif
