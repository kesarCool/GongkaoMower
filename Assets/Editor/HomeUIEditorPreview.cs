#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HomeUIEditorPreview
{
    private const string Key = "GongkaoMower.HomeUIPreview.";
    static HomeUIEditorPreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore;
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key+"pending",false))
            {
                SessionState.SetBool(Key+"pending",false);
                string phase=SessionState.GetString(Key+"phase","home");
                string name=phase=="platform-menu"?"PlatformMenuSpacingValidationRunner":phase=="chapters"?"HomeChapterMapsValidationRunner":phase=="home"?"HomeRoadmapValidationRunner":phase=="pages"?"HomePagesValidationRunner":phase=="dialogs"?"DialogsValidationRunner":phase=="backpack"?"BackpackValidationRunner":phase=="battle"?"BattleUIValidationRunner":phase=="battle-readability"?"BattleReadabilityValidationRunner":phase=="map-decoration"?"MapDecorationValidationRunner":phase=="boss-wheel"?"BossWheelValidationRunner":phase=="beam"?"LineBeamValidationRunner":phase=="boss-arena"?"BossArenaWallValidationRunner":phase=="feedback"?"ProgressionFeedbackValidationRunner":"CharacterProgressionValidationRunner";
                if(phase=="grenade-explosion"||phase=="explosion-library") name="ExplosionLibraryValidationRunner";
                if(phase=="talisman") name="TalismanPresentationValidationRunner";
                if(phase=="lightning") name="LightningPresentationValidationRunner";
                if(phase=="field-generator") name="FieldGeneratorValidationRunner";
                if(phase=="sword") name="SwordPresentationValidationRunner";
                if(phase=="boss-blade-burst") name="BossBladeBurstValidationRunner";
                if(phase=="boss-dash-knife") name="BossDashKnifeValidationRunner";
                if(phase=="boss-clone") name="BossCloneValidationRunner";
                if(phase=="boss-summon") name="BossSummonValidationRunner";
                if(phase=="boss-revive") name="BossReviveValidationRunner";
                if(phase=="boss-zone") name="BossZoneValidationRunner";
                if(phase=="gunner-aim") name="PlayerGunnerAimValidationRunner";
                if(phase=="result-return") name="ResultReturnProgressValidationRunner";
                if(phase=="attribute-delta") name="CharacterAttributeDeltaValidationRunner";
                if(phase=="boss-entry") name="BossArenaEntryValidationRunner";
                if(phase=="tutorial") name="TutorialValidationRunner";
                if(phase=="skill-feedback") name="SkillSelectionV5ValidationRunner";
                if(phase=="home-navigation") name="HomeNavigationValidationRunner";
                if(phase=="energy-pickup") name="EnergyPickupValidationRunner";
                if(phase=="battle-loading") name="BattleLoadingValidationRunner";
                if(phase=="player-spawn") name="PlayerSpawnValidationRunner";
                var type=typeof(UIValidationRunnerBase).Assembly.GetType(name);
                if(type==null) throw new InvalidOperationException("Missing preview runner: "+name);
                var runner=(UIValidationRunnerBase)new GameObject(name).AddComponent(type);
                runner.changeResolution=SkillSelectionUIV4Preview.SetSize;
            }
        };
    }
    [MenuItem("工具/Home UI/Preview Roadmap")]
    private static void HomeMenu(){Debug.Log(Begin("home"));}
    [MenuItem("工具/Home UI/Preview Seasonal Chapter Maps")]
    private static void ChaptersMenu(){Debug.Log(Begin("chapters"));}
    [MenuItem("工具/Home UI/Preview Character")]
    private static void CharacterMenu(){Debug.Log(Begin("character"));}
    [MenuItem("工具/Home UI/Preview Tutorial")]
    private static void TutorialMenu(){Debug.Log(Begin("tutorial"));}
    [MenuItem("工具/Home UI/Finish Preview")]
    public static void Finish(){if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;else Restore();}
    public static string Begin(string phase)
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Restore();
        var saved=new SceneList();
        foreach(var setup in EditorSceneManager.GetSceneManagerSetup())
        {
            if(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(setup.path).isDirty) throw new InvalidOperationException("Unsaved scene changes; preview was not started.");
            saved.scenes.Add(new Record{path=setup.path,isLoaded=setup.isLoaded,isActive=setup.isActive});
        }
        SessionState.SetString(Key+"scenes",JsonUtility.ToJson(saved));
        var gv=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        SessionState.SetInt(Key+"size",(int)gv.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(gv));
        SessionState.SetString(Key+"phase",phase); SessionState.SetBool(Key+"pending",true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SkillSelectionUIV4Preview.SetSize(1080,1920);
        EditorApplication.isPlaying=true;
        return "Started isolated "+phase+" validation with in-memory save storage.";
    }
    private static void Restore()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
        string json=SessionState.GetString(Key+"scenes","");if(json=="")return;
        var scenes=JsonUtility.FromJson<SceneList>(json).scenes.Where(s=>!string.IsNullOrEmpty(s.path))
            .Select(s=>new SceneSetup{path=s.path,isLoaded=s.isLoaded,isActive=s.isActive}).ToArray();
        if(scenes.Length>0) EditorSceneManager.RestoreSceneManagerSetup(scenes);
        SessionState.EraseString(Key+"scenes");
        var gv=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gv.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(gv,SessionState.GetInt(Key+"size",0));
    }
    [Serializable]private class SceneList{public List<Record> scenes=new List<Record>();}
    [Serializable]private class Record{public string path;public bool isLoaded,isActive;}
}
#endif

