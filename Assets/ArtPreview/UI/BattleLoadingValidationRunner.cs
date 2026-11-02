#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleLoadingValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "BattleLoading";
    protected override string ValidationNote => "Authored page at three chapter themes and multiple aspect ratios; actual Home -> BattleLoading -> Game, missing-scene retry, and direct quick-start entry. In-memory profile; no WeChat device/network timing test.";
    private bool previousBackground, failNext;
    private int loadingEntries;
    private BattleLoadingSceneController controller;
    private BattleLoadingPresentation page;
    private readonly List<string> samples = new List<string>();
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject); previousBackground = Application.runInBackground; Application.runInBackground = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "BattleLoading")
        {
            loadingEntries++;
            controller = FindObjectOfType<BattleLoadingSceneController>();
            page = controller.GetComponentInChildren<BattleLoadingPresentation>();
            if (failNext) typeof(BattleLoadingSceneController).GetField("nextSceneName", Hidden).SetValue(controller, "MissingSceneForLoadingValidation");
        }
        if (scene.name == "Game")
            foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
    }

    protected override IEnumerator RunChecks()
    {
        TutorialDirector.Progress.skipped = true; TutorialDirector.Current?.Cancel();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Home/BattleLoadingPage.prefab");
        var preview = Instantiate(asset).GetComponent<BattleLoadingPresentation>();
        fixture.SetProgress(20); TutorialDirector.Progress.skipped = true;
        foreach (int chapter in new[] { 1, 2, 3 })
        {
            SelectedLevelContext.Set(chapter, chapter * 100 + 1); preview.RefreshSelectedContent(); preview.ResetProgress();
            preview.Report(.62f, "正在布置战场…"); yield return new WaitForSecondsRealtime(.3f);
            Record("Chapter " + chapter + " reuses its matching map and equipped portrait", preview.background.sprite == preview.chapterBackgrounds[chapter - 1] &&
                preview.heroPortrait.sprite == preview.characters.Get(PlayerProfileService.Instance.EquippedCharacterId).portrait && preview.level.text == ChapterLevelDisplay.FormatLevelName(chapter * 100 + 1));
            Record("Chapter " + chapter + " uses one existing font", preview.GetComponentsInChildren<TMP_Text>(true).All(t => t.font == Resources.Load<TMP_FontAsset>("Fonts/msyh SDF")));
            yield return Capture("chapter-0" + chapter + "-1080x1920");
        }
        preview.Report(.2f);
        Record("Lower phase reports cannot reset progress", Mathf.Abs(preview.TargetProgress - .62f) < .001f);
        Record("Progress bar has a visible sprite-backed fill", preview.progress.fillRect != null && preview.progress.fillRect.rect.width > 100 &&
            preview.progress.fillRect.GetComponent<Image>().sprite != null && preview.percent.text == "62%");
        foreach (var size in new[] { new Vector2Int(1080, 2400), new Vector2Int(1080, 1440) })
        {
            changeResolution(size.x, size.y); yield return null; yield return null;
            preview.ApplyLayout(); Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; preview.design.GetWorldCorners(corners);
            Record(size + " design stays inside the safe canvas", corners[0].x >= -.1f && corners[0].y >= -.1f && corners[2].x <= size.x + .1f && corners[2].y <= size.y + .1f);
            var bg = preview.background.rectTransform; bg.GetWorldCorners(corners);
            Record(size + " background covers the screen without stretching", corners[0].x <= .1f && corners[0].y <= .1f && corners[2].x >= size.x - .1f && corners[2].y >= size.y - .1f &&
                Mathf.Abs(bg.rect.width / bg.rect.height - preview.background.sprite.rect.width / preview.background.sprite.rect.height) < .001f);
            yield return Capture("winter-" + size.x + "x" + size.y);
        }
        changeResolution(1080, 1920); yield return null; yield return null;
        Rect menu; PlatformMenuExclusion.TryNormalize(new Rect(278, 52, 87, 32), new Vector2(375, 667), out menu);
        PlatformMenuExclusion.EditorMenuRectOverride = menu; preview.ApplyLayout(); Canvas.ForceUpdateCanvases();
        var topCorners = new Vector3[4]; preview.design.GetWorldCorners(topCorners);
        Record("WeChat capsule has its own clear region above page content", topCorners[2].y <= (1 - menu.yMax) * Screen.height - 15);
        yield return Capture("winter-wechat-safe-1080x1920");
        PlatformMenuExclusion.EditorMenuRectOverride = null;
        Destroy(preview.gameObject); changeResolution(1080, 1920); yield return null; yield return null;

        fixture.SetProgress(1); TutorialDirector.Progress.skipped = true; SelectedLevelContext.Set(1, 102);
        failNext = true;
        Record("Home launcher accepts selected stage", BattleFlowLauncher.TryStartBattleLoading());
        Record("Immediate duplicate start is ignored", BattleFlowLauncher.TryStartBattleLoading());
        float deadline = Time.realtimeSinceStartup + 30;
        while ((page == null || !page.HasError) && Time.realtimeSinceStartup < deadline) yield return null;
        Record("Actual missing-scene failure keeps page and retry visible", page != null && page.HasError && page.retry.gameObject.activeInHierarchy && page.TargetProgress < 1 && loadingEntries == 1);
        if (page == null || !page.HasError) yield break;
        yield return Capture("retry-state-1080x1920");
        var retryPoint = RectTransformUtility.WorldToScreenPoint(null, page.retry.transform.position + (Vector3)((RectTransform)page.retry.transform).rect.center);
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = retryPoint }, hits);
        Record("Retry remains clickable without Home EventSystem", hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == page.retry);
        failNext = false;
        typeof(BattleLoadingSceneController).GetField("nextSceneName", Hidden).SetValue(controller, "Game");
        page.retry.onClick.Invoke();
        Record("Retry starts a fresh attempt", !page.HasError && page.TargetProgress <= .28f);
        yield return ObserveHandoff("Retry from Home entry");
        yield return Capture("game-after-reveal-1080x1920");

        // New-player login enters BattleLoading directly, with no Home UI present.
        controller = null; page = null;
        SceneManager.LoadSceneAsync("BattleLoading", LoadSceneMode.Single);
        deadline = Time.realtimeSinceStartup + 10;
        while (controller == null && Time.realtimeSinceStartup < deadline) yield return null;
        yield return ObserveHandoff("Direct quick-start entry");
        File.WriteAllLines(Path.Combine(output, "handoff-samples.csv"), samples);
    }

    private IEnumerator ObserveHandoff(string label)
    {
        float lastTarget = 0, lastDisplayed = 0;
        bool monotonic = true, coveredActivation = false, sawFade = false, readyAtFull = true, inputBlocked = true;
        float deadline = Time.realtimeSinceStartup + 30;
        while (controller != null && Time.realtimeSinceStartup < deadline)
        {
            if (page == null) { yield return null; continue; }
            float target = page.TargetProgress, displayed = page.DisplayedProgress, alpha = page.canvasGroup.alpha;
            monotonic &= target >= lastTarget - .0001f && displayed >= lastDisplayed - .0001f;
            lastTarget = target; lastDisplayed = displayed;
            bool game = SceneManager.GetActiveScene().name == "Game";
            bool ready = game && BattleMapLoader.Instance != null && BattleMapLoader.Instance.GroundTilemap != null && FindObjectOfType<PlayerController>() != null;
            if (game && target < .999f) coveredActivation |= alpha > .999f;
            if (target >= .999f) readyAtFull &= ready;
            if (game && alpha > 0) inputBlocked &= Time.timeScale == 0 && TutorialDirector.BlocksGameplayInput;
            sawFade |= alpha > .001f && alpha < .999f;
            samples.Add(label + "," + Time.realtimeSinceStartup.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "," + target + "," + displayed + "," + alpha + "," + game + "," + ready);
            yield return null;
        }
        Record(label + " progress is continuous and never rolls back", monotonic && lastDisplayed >= .999f);
        Record(label + " activation is covered until ready", coveredActivation && readyAtFull);
        Record(label + " game is paused and input blocked behind the page", inputBlocked);
        Record(label + " page fades and fully releases pause/input", sawFade && controller == null && !BattleLoadingSceneController.IsTransitionVisible &&
            SceneManager.GetActiveScene().name == "Game" && Mathf.Approximately(Time.timeScale, 1) && !TutorialDirector.BlocksGameplayInput);
        Record(label + " correct stage and map survive the handoff", BattleLevelContext.LevelId == 102 && BattleMapLoader.Instance != null && BattleMapLoader.Instance.GroundTilemap != null);
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlatformMenuExclusion.EditorMenuRectOverride = null;
        Application.runInBackground = previousBackground; Time.timeScale = 1;
        base.OnDestroy();
    }
}
#endif
