#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ResultReturnProgressValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "ResultReturnProgress";
    protected override string ValidationNote => "Actual Game result buttons and Home scene transitions; victory progress recorded by PlayerProfileService using in-memory storage. No persistent save mutation or full gameplay completion simulation.";
    private bool previousBackground;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        previousBackground = Application.runInBackground; Application.runInBackground = true;
    }

    protected override IEnumerator RunChecks()
    {
        fixture.SetProgress(7); SelectedLevelContext.Set(1, 108);
        PlayerProfileService.Instance.RecordVictory(108, 90, 300, 3); map.RefreshFromProfile();
        Record("Unlocked chapter 2 does not override a manual Home selection", map.CurrentProgressLevel == 201 && map.SelectedLevel == 108 && map.VisibleChapter == 1);

        yield return ReturnCase("chapter-1-to-2", 7, 108, true, 201);
        yield return Capture("home-after-clearing-108");
        Record("Crossing chapter 1 selects autumn map", map.mapBackground.sprite.name.Contains("autumn"));
        Record("Start button resolves newly unlocked chapter 2", HomeHubController.TryResolveStartLevel(out int chapter, out int level) && chapter == 2 && level == 201);
        Record("Manual replay remains available", map.SelectLevel(108) && map.VisibleChapter == 1 && map.SelectedLevel == 108);
        var navigation = FindObjectOfType<HomeTabBar>(true);
        navigation.SwitchTo("character"); yield return null; navigation.SwitchTo("battle"); yield return null;
        Record("Tab refresh retains explicit replay selection", map.SelectedLevel == 108 && map.VisibleChapter == 1);

        yield return ReturnCase("chapter-2-to-3", 15, 208, true, 301);
        yield return Capture("home-after-clearing-208");
        Record("Crossing chapter 2 selects winter map", map.mapBackground.sprite.name.Contains("winter"));
        yield return ReturnCase("failed-boundary-level", 7, 108, false, 108);
        Record("Defeat does not unlock next chapter", !PlayerProfileService.Instance.IsLevelUnlocked(201) && !map.SelectLevel(201));
        yield return ReturnCase("ordinary-level-victory", 1, 102, true, 103);
        yield return ReturnCase("replay-returns-current-progress", 17, 101, true, 302);
        yield return ReturnCase("all-levels-completed", 21, 305, true, 305);
        yield return ReturnCase("unlock-link-returns-current-progress", 7, 108, true, 201, true);
        yield return Capture("home-final-chapter-2");
    }

    private IEnumerator ReturnCase(string name, int priorCleared, int battleLevel, bool victory, int expectedHomeLevel, bool unlockLink = false)
    {
        fixture.SetProgress(priorCleared); SelectedLevelContext.Set(battleLevel / 100, battleLevel);
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var coordinator in FindObjectsOfType<BattleOutcomeCoordinator>()) { coordinator.StopAllCoroutines(); coordinator.enabled = false; }
        yield return null; yield return null;
        Time.timeScale = 0;
        if (victory) PlayerProfileService.Instance.RecordVictory(battleLevel, 90, 300, 3);
        var model = new GameResultViewModel { victory = victory, stars = victory ? 3 : 0, battleDurationUnscaled = 90,
            killCount = 300, completedWaves = victory ? 7 : 3, totalWaves = 7, rewardItems = new List<RewardItemEntry>(),
            unlockedCharacters = unlockLink ? new List<string> { "爆爆" } : null };
        var result = UIManager.Instance.Open<GameResultPanel>(model);
        yield return new WaitForSecondsRealtime(.4f);
        Record(name + " result preserves battle context until action", result != null && SelectedLevelContext.LevelId == battleLevel);
        if (result == null) yield break;
        if (!victory) Record(name + " retry action remains visible", Field<Button>(result, "_btnAgain").gameObject.activeInHierarchy);
        if (victory && ChapterLevelNavigation.TryGetNext(battleLevel / 100, battleLevel, out int nextChapter, out int nextLevel))
            Record(name + " next-level action still resolves battle level", Field<Button>(result, "_btnNext").interactable == PlayerProfileService.Instance.IsLevelUnlocked(nextLevel));
        var button = Field<Button>(result, unlockLink ? "unlockGoButton" : "_btnExit");
        Record(name + " return action is reachable", button != null && button.gameObject.activeInHierarchy && button.interactable);
        if (button == null) yield break;
        button.onClick.Invoke();
        yield return null; yield return null; yield return new WaitForSecondsRealtime(.4f);
        map = FindObjectOfType<HomeChapterMapView>(true);
        Record(name + " returns to Home", SceneManager.GetActiveScene().name == "Home" && map != null);
        Record(name + " selects expected progress", map != null && map.SelectedLevel == expectedHomeLevel && map.VisibleChapter == expectedHomeLevel / 100,
            "Expected " + expectedHomeLevel + ", actual " + (map != null ? map.SelectedLevel.ToString() : "missing map"));
        Record(name + " time scale restored", Time.timeScale == 1);
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    protected override void OnDestroy()
    {
        Time.timeScale = 1; Application.runInBackground = previousBackground; base.OnDestroy();
    }
}
#endif
