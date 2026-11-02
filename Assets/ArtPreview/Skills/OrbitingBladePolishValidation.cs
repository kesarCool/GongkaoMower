#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in empty-scene Play Mode check of the real skill, cooldown and pool lifecycle.</summary>
public sealed class OrbitingBladePolishValidation : MonoBehaviour
{
    const string Output = "ArtOutput/VFX/OrbitingBladePolishV1";
    SkillOrbitingBlades skill;
    GameObject player;
    bool ticking;
    readonly List<string> passed = new List<string>();
    readonly List<string> failed = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!SessionState.GetBool("OrbitingBladePolish.Validate", false)) return;
        SessionState.SetBool("OrbitingBladePolish.Validate", false);
        new GameObject("Orbiting blade validation").AddComponent<OrbitingBladePolishValidation>();
    }

    void Check(string name, bool condition) { (condition ? passed : failed).Add(name); }
    OrbitingBladeVisual[] Blades() => player.GetComponentsInChildren<OrbitingBladeVisual>(true);
    void Update() { if (ticking) skill.Tick(Time.deltaTime); }
    void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed.Add(message);
    }

    IEnumerator Start()
    {
        Application.logMessageReceived += Log;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/status.txt", "running");
        player = new GameObject("Fixture player (no save or game systems)");
        var def = AssetDatabase.LoadAssetAtPath<OrbitingBladesSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_OrbitingBlades.asset");
        skill = (SkillOrbitingBlades)def.CreateRuntimeSkill(new SkillRuntimeBindings());
        skill.OnEquip(new SkillContext { player=player.transform, enemyTag="Enemy" });
        ticking = true;
        yield return new WaitForSeconds(.25f);
        var regular = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Res/image/Game/bullet/blade.png");
        var advanced = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Res/image/Game/bullet/blade2.png");
        Check("Level 1 has 2 original blade sprites", Blades().Length==2 && Blades().All(b=>b.GetComponent<SpriteRenderer>().sprite==regular));
        while(skill.Level<4) skill.OnLevelUp();
        def.ApplyStatsToSkill(skill,skill.Level);
        yield return new WaitForSeconds(.2f);
        Check("Level 4 has 5 original blade sprites",Blades().Length==5 && Blades().All(b=>b.GetComponent<SpriteRenderer>().sprite==regular));
        Capture("regular-runtime");
        skill.maxLevelBladePrefab=null; // Also exercise restoring the binding before the immediate rebuild.
        skill.OnLevelUp();
        def.ApplyStatsToSkill(skill,skill.Level);
        yield return new WaitForSeconds(.25f);
        var blades=Blades();
        Check("Breakthrough immediately has 6 blade2 sprites",blades.Length==6 && blades.All(b=>b.GetComponent<SpriteRenderer>().sprite==advanced));
        Capture("breakthrough-runtime");
        Check("Authored radius / speed / damage / tick retained",Mathf.Approximately(skill.orbitRadius,2) && Mathf.Approximately(skill.rotateSpeedDeg,260) && Mathf.Approximately(skill.damagePerTick,.7f) && Mathf.Approximately(skill.tickInterval,.1f));
        Check("One shared-material trail and no particles per blade",blades.All(b=>b.GetComponentsInChildren<TrailRenderer>().Length==1 && b.GetComponentsInChildren<ParticleSystem>().Length==0) && blades.Select(b=>b.GetComponentInChildren<TrailRenderer>().sharedMaterial).Distinct().Count()==1);
        Check("Trails render behind the blade",blades.All(b=>b.GetComponentInChildren<TrailRenderer>().sortingOrder==b.GetComponent<SpriteRenderer>().sortingOrder-1));
        ticking=false;
        skill.Tick(skill.activeDuration+.01f);
        Check("Cooldown hides colliders and clears all trail positions",Blades().All(b=>!b.gameObject.activeSelf && b.GetComponentInChildren<TrailRenderer>(true).positionCount==0));
        player.transform.position=new Vector3(8,3,0);
        skill.Tick(skill.cooldownDuration+.01f);
        Check("Cooldown re-entry holds emission until final positioning",Blades().All(b=>b.gameObject.activeSelf && !b.GetComponentInChildren<TrailRenderer>().emitting));
        yield return new WaitForEndOfFrame();
        Check("First rendered restart contains no previous-location samples",Blades().All(b=> {
            var t=b.GetComponentInChildren<TrailRenderer>();
            var points=new Vector3[t.positionCount];
            t.GetPositions(points);
            return t.emitting && points.All(p=>Vector3.Distance(p,t.transform.position)<.15f);
        }));
        ticking=true;
        yield return new WaitForSeconds(.15f);
        var blade=Blades()[0];
        var trail=blade.GetComponentInChildren<TrailRenderer>();
        Check("Trail resumes after repositioning",trail.emitting && trail.positionCount>1);
        Time.timeScale=0;
        var rotation=blade.transform.rotation;
        yield return new WaitForSecondsRealtime(.12f);
        Check("Pause freezes blade rotation",Quaternion.Angle(rotation,blade.transform.rotation)<.001f);
        Time.timeScale=1;
        ticking=false;
        skill.OnUnequip();
        Check("Unequip clears pooled trails",trail!=null && !trail.emitting && trail.positionCount==0 && !blade.gameObject.activeInHierarchy);
        yield return null;
        var prefab=def.maxLevelPrefab;
        var reused=GameObjectPool.Get(prefab,new Vector3(-10,-10),Quaternion.identity);
        Check("Pool re-entry starts without stale trail",reused.GetComponentInChildren<TrailRenderer>().positionCount==0);
        GameObjectPool.Release(reused);
        GameObjectPool.ClearAllPools();
        Destroy(player);
        yield return null;
        var report=new Report { success=failed.Count==0, passed=passed.ToArray(), failed=failed.ToArray() };
        File.WriteAllText(Output+"/validation.json",JsonUtility.ToJson(report,true));
        File.WriteAllText(Output+"/status.txt",report.success?"passed":"failed");
        Application.logMessageReceived-=Log;
        Debug.Log("[Orbiting blade polish] "+passed.Count+" passed, "+failed.Count+" failed");
    }

    void OnDestroy()
    {
        Application.logMessageReceived-=Log;
        Time.timeScale=1;
    }

    void Capture(string filename)
    {
        var root=new GameObject("Runtime proof camera");
        var camera=root.AddComponent<Camera>();
        camera.orthographic=true;
        camera.orthographicSize=3.1f;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.22f,.28f,.24f);
        root.transform.position=player.transform.position+Vector3.back*10;
        var previous=RenderTexture.active;
        var target=new RenderTexture(720,720,24);
        var texture=new Texture2D(720,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;
            camera.Render();
            RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,720,720),0,0);
            texture.Apply();
            File.WriteAllBytes(Output+"/"+filename+".png",texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;
            RenderTexture.active=previous;
            Destroy(texture);Destroy(target);Destroy(root);
        }
    }
    [Serializable] sealed class Report { public bool success; public string[] passed,failed; }
}
#endif
