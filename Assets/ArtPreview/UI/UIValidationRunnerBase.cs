#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class UIValidationRunnerBase : MonoBehaviour
{
    public Action<int,int> changeResolution;
    protected string output;
    protected HomeChapterMapView map;
    protected HomeTabBar tabs;
    protected HomeHubController hub;
    protected UIProfileFixture fixture;
    protected readonly List<Check> checks=new List<Check>();
    private readonly List<string> runtimeErrors=new List<string>();
    protected abstract string OutputFolder{get;}
    protected virtual string ValidationNote => "Unity Editor rendering and real Home scene; isolated in-memory profile; no WeChat device or package-size validation.";
    protected abstract IEnumerator RunChecks();

    private IEnumerator Start()
    {
        output=Path.GetFullPath("ArtOutput/UI/"+OutputFolder);Directory.CreateDirectory(output);
        Application.logMessageReceived+=CaptureError;
        File.WriteAllText(Path.Combine(output,"status.txt"),"running");
        fixture=new UIProfileFixture();fixture.SetProgress(1);
        var op=SceneManager.LoadSceneAsync("Assets/Scenes/Home.unity",LoadSceneMode.Additive);yield return op;
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath("Assets/Scenes/Home.unity"));
        yield return null;yield return null;yield return new WaitForSecondsRealtime(.4f);
        map=FindObjectOfType<HomeChapterMapView>(true);tabs=FindObjectOfType<HomeTabBar>(true);hub=FindObjectOfType<HomeHubController>(true);
        var flow=RunChecks();
        while(true)
        {
            bool next;
            try{next=flow.MoveNext();}
            catch(Exception ex){Record("No unhandled validation error",false,ex.ToString());WriteReport();Debug.LogException(ex);yield break;}
            if(!next)break;yield return flow.Current;
        }
        Record("PlayerPrefs save preserved",fixture.IsPersistentSaveUnchanged,"UI operations used ISaveStorage backed by memory.");
        Record("No runtime errors during validation",runtimeErrors.Count==0,string.Join("\n",runtimeErrors));
        WriteReport();
    }
    protected IEnumerator Capture(string filename)
    {
        yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,filename+".png"));yield return null;yield return null;
    }
    protected void Record(string name,bool passed,string detail=""){checks.Add(new Check{name=name,passed=passed,detail=detail});}
    private void WriteReport()
    {
        bool passed=checks.All(c=>c.passed);
        File.WriteAllText(Path.Combine(output,"validation-report.json"),JsonUtility.ToJson(new Report{passed=passed,checks=checks,note=ValidationNote},true));
        File.WriteAllText(Path.Combine(output,"status.txt"),passed?"passed":"failed");
        Debug.Log("[Home UI] "+OutputFolder+": "+checks.Count+" checks, passed="+passed);
    }
    private void CaptureError(string message,string stack,LogType type)
    { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors.Add(message); }
    protected virtual void OnDestroy(){Application.logMessageReceived-=CaptureError;fixture?.Restore();}
    [Serializable]protected class Check{public string name,detail;public bool passed;}
    [Serializable]private class Report{public bool passed;public List<Check>checks;public string note;}
}

public sealed class UIProfileFixture
{
    private readonly object original;
    private readonly string saved,guest;
    private readonly FieldInfo singleton=typeof(PlayerProfileService).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic);
    public bool IsPersistentSaveUnchanged=>saved==PlayerPrefs.GetString(PlayerProfileService.SaveKey,"")&&guest==PlayerPrefs.GetString(PlayerProfileService.GuestIdKey,"");
    public UIProfileFixture(){original=singleton.GetValue(null);saved=PlayerPrefs.GetString(PlayerProfileService.SaveKey,"");guest=PlayerPrefs.GetString(PlayerProfileService.GuestIdKey,"");}
    public void SetProgress(int cleared)
    {
        var catalog=AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
        var knife=catalog.characters.First(c=>c.startingSkill==SkillId.AutoProjectileSword);
        var pistol=catalog.characters.First(c=>c.startingSkill==SkillId.AutoProjectilePistol);
        var data=new PlayerSaveData{playerId="ui_preview_guest",gold=832,diamond=325,equippedCharacterId=knife.characterId,
            unlockedCharacters=new[]{knife.characterId,pistol.characterId},itemIds=new[]{1},itemCounts=new[]{832},
            heroUpgrades=new[]{new HeroUpgradeEntry{characterId=knife.characterId,level=2},new HeroUpgradeEntry{characterId=pistol.characterId,level=5}},
            characterFragmentKeys=new[]{knife.characterId},characterFragmentValues=new[]{20}};
        var service=(PlayerProfileService)Activator.CreateInstance(typeof(PlayerProfileService),true);
        var storage=new MemoryStorage(); storage.SaveString(PlayerProfileService.GuestIdKey,data.playerId);storage.Save(PlayerProfileService.SaveKey,JsonUtility.ToJson(data));
        typeof(PlayerProfileService).GetField("_storage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(service,storage);
        singleton.SetValue(null,service);service.LoadOrCreate();
        TableManager.Instance.Init();
        var ids=new List<int>();
#if USE_FB_TABLE
        var table=TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        foreach(var entry in table) if(entry.Value is ProtoTable.ChapterLevel row)ids.Add(row.levelId);
#endif
        if(ids.Count==0)for(int i=101;i<=108;i++)ids.Add(i);
        ids.Sort();
        var levels=(Dictionary<int,LevelProgressEntry>)typeof(PlayerProfileService).GetField("_levels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        for(int i=0;i<Mathf.Min(cleared,ids.Count);i++)levels[ids[i]]=new LevelProgressEntry{levelId=ids[i],cleared=true,stars=3,bestTimeSec=100,bestKills=300};
        service.Data.levels=levels.Values.ToArray();
        SelectedLevelContext.Set(1,Mathf.Min(108,101+cleared));SelectedCharacterContext.Set(knife.characterId);
    }
    public void Restore(){singleton.SetValue(null,original);}
    private sealed class MemoryStorage:ISaveStorage
    {
        private readonly Dictionary<string,string> data=new Dictionary<string,string>();
        public bool TryLoad(string key,out string json)=>data.TryGetValue(key,out json);
        public void Save(string key,string json){data[key]=json;}
        public bool TryLoadString(string key,out string value)=>data.TryGetValue(key,out value);
        public void SaveString(string key,string value){data[key]=value;}
    }
}
#endif
