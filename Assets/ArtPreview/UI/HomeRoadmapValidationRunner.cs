#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HomeRoadmapValidationRunner:UIValidationRunnerBase
{
    protected override string OutputFolder=>"HomeRoadmap";
    protected override IEnumerator RunChecks()
    {
        Record("Home map and navigation available",map!=null&&tabs!=null&&hub!=null);
        Record("Food chapter eight nodes",map.nodes.Count(n=>n.gameObject.activeSelf)==8&&map.VisibleChapter==1);
        Record("Saved stars and progress",map.nodes[0].stars.All(s=>s.gameObject.activeSelf)&&map.progressText.text=="1 / 8",map.progressText.text);
        Record("Initial current stage",map.SelectedLevel==102&&map.CurrentProgressLevel==102);
        Record("Locked node cannot enter",!map.nodes[2].button.interactable);
        yield return Capture("home-current-102-1080x1920");
        int chapter,level;
        bool selected=map.SelectLevel(101);
        Record("Node selects without starting battle",selected&&SceneManager.GetActiveScene().name=="Home");
        Record("Start resolves selected replay",HomeHubController.TryResolveStartLevel(out chapter,out level)&&level==101);
        Record("Locked selection preserves selection",!map.SelectLevel(103)&&map.SelectedLevel==101);
        yield return Capture("home-replay-101-1080x1920");
        map.LocateCurrent();Record("Locate returns progress rather than replay",map.SelectedLevel==102);
        fixture.SetProgress(0);SelectedLevelContext.Clear();map.RefreshFromProfile();
        Record("New account starts at first stage",map.SelectedLevel==101&&map.nodes[0].button.interactable&&!map.nodes[1].button.interactable);
        yield return Capture("home-new-account-1080x1920");
        fixture.SetProgress(7);map.RefreshFromProfile();
        Record("Final stage reachable",map.SelectedLevel==108&&map.nodes[7].IsSelected);
        yield return Capture("home-stage-108-1080x1920");
        fixture.SetProgress(8);SelectedLevelContext.Set(2,201);map.RefreshFromProfile();
        Record("Idiom chapter preserved",map.VisibleChapter==2&&map.nodes[0].LevelId==201);
        yield return Capture("home-idiom-chapter-1080x1920");
        fixture.SetProgress(1);SelectedLevelContext.Set(1,102);hub.RefreshCurrencyHud();map.RefreshFromProfile();
        tabs.SwitchTo("lexicon");yield return null;Record("Vocabulary tab retained",tabs.ActiveTabId=="lexicon"&&!map.gameObject.activeInHierarchy);
        tabs.SwitchTo("battle");yield return null;Record("Return to battle retains map",map.gameObject.activeInHierarchy&&map.SelectedLevel==102);
        hub.OpenSettings();yield return new WaitForSecondsRealtime(.35f);Record("Settings entry retained",UIManager.Instance.Top is SettingsPanel);UIManager.Instance.CloseTop();
        hub.OpenBackpack();yield return new WaitForSecondsRealtime(.35f);Record("Backpack entry retained",UIManager.Instance.Top is BackPackPanel);UIManager.Instance.CloseTop();
        hub.OpenLevelSelect();yield return new WaitForSecondsRealtime(.35f);Record("Full level selector retained",UIManager.Instance.Top is LevelSelectPanel);UIManager.Instance.CloseTop();
        changeResolution(1080,2400);yield return Capture("home-long-screen-1080x2400");
        Record("Long screen cover keeps map ratio",map.MapScale>1&&Mathf.Abs(map.world.localScale.x-map.world.localScale.y)<.001f);
        changeResolution(360,640);yield return Capture("home-phone-360x640");
        Record("Map remains scrollable on phone",map.content.rect.height>map.scroll.viewport.rect.height);
        changeResolution(1080,1920);yield return Capture("home-final-1080x1920");
    }
}
#endif

