#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Checks actual Home entrances and first-frame selection in the recycled level list.</summary>
public sealed class HomeNavigationValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "HomeNavigation";
    private bool previousBackground;
    private void Awake() { previousBackground = Application.runInBackground; Application.runInBackground = true; }
    private static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    protected override IEnumerator RunChecks()
    {
        TutorialDirector.Progress.skipped = true; TutorialDirector.Current?.Cancel();
        var badge = map.chapterText.transform.parent.GetComponent<Button>();
        var bag = Field<Button>(hub, "openBackpackButton");
        var group = Field<GameObject>(hub, "homeButtonGroup").transform;
        Record("Chapter badge is the registered selection entrance", badge != null && Field<Button>(hub, "openLevelSelectButton") == badge);
        Record("Current-stage shortcut removed", map.locateButton == null && map.transform.Find("IllustratedMap/LocateCurrent") == null);
        Record("Separate level shortcut hidden", !group.Find("ButtonChapterLevel").gameObject.activeSelf);

        fixture.SetProgress(10); TutorialDirector.Progress.skipped = true;
        SelectedLevelContext.Set(2, 203); hub.RefreshCurrencyHud();
        yield return null; yield return null;
        var ui = UIManager.Instance;
        foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400), new Vector2Int(1080, 1440) })
        {
            changeResolution(size.x, size.y); yield return null; yield return null;
            Rect normalized; PlatformMenuExclusion.TryNormalize(new Rect(278, 52, 87, 32),
                new Vector2(375, 375f * size.y / size.x), out normalized);
            PlatformMenuExclusion.EditorMenuRectOverride = normalized;
            hub.GetComponent<HomeShellLayout>().Apply(); Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)badge.transform;
            bool wholeBadge = true;
            foreach (var sample in new[] { new Vector2(.03f, .5f), new Vector2(.5f, .5f), new Vector2(.97f, .5f), new Vector2(.5f, .1f), new Vector2(.5f, .9f) })
            {
                var local = new Vector3(Mathf.Lerp(rt.rect.xMin, rt.rect.xMax, sample.x), Mathf.Lerp(rt.rect.yMin, rt.rect.yMax, sample.y), 0);
                var point = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));
                wholeBadge &= HitButton(point) == badge;
            }
            Record(size + " whole badge including both edges is clickable", wholeBadge);
            var capsule = new Rect(normalized.x * size.x, (1 - normalized.yMax) * size.y, normalized.width * size.x, normalized.height * size.y);
            Record(size + " badge avoids WeChat capsule", !ScreenRect(rt).Overlaps(capsule));
            var bagRect = ScreenRect((RectTransform)bag.transform);
            var start = ScreenRect((RectTransform)Field<Button>(hub, "startGameButton").transform);
            var nav = ScreenRect((RectTransform)tabs.transform);
            Record(size + " lower backpack stays visible and avoids main button and navigation", bagRect.yMin >= nav.yMax && bagRect.yMax < size.y * .45f &&
                !bagRect.Overlaps(start) && HitButton(bagRect.center) == bag, bagRect.ToString());
            if (size.y != 1440) yield return Capture("home-chapter-02-" + size.x + "x" + size.y);
        }
        PlatformMenuExclusion.EditorMenuRectOverride = null;
        changeResolution(1080, 1920); yield return null; yield return null;

        // A real pointer down/up/click sequence also exercises the button's pressed state.
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSecondsRealtime(.12f);
        Record("Chapter badge has visible press feedback", badge.transition == Selectable.Transition.ColorTint && badge.image.canvasRenderer.GetColor().g < .95f);
        ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Record("Badge click opens existing level panel", ui.Top is LevelSelectPanel);
        yield return CheckInstantSelection(203, "Badge click");
        yield return Capture("level-select-current-203-1080x1920");
        ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);

        foreach (int cleared in new[] { 0, 7, 17, 20 })
        {
            fixture.SetProgress(cleared); TutorialDirector.Progress.skipped = true;
            int chapter, current; ChapterLevelNavigation.TryGetMaxUnlockedLevel(out chapter, out current);
            var panel = ui.Open<LevelSelectPanel>(null, UiOpenOptions.NonPausingModal);
            yield return CheckInstantSelection(current, "Progress " + current);
            if (current == 305) yield return Capture("level-select-current-305-1080x1920");
            // Browse away, close, then reopen the pooled panel with stale velocity.
            var loop = panel.GetComponentInChildren<LoopVerticalScrollRect>();
            loop.RefillCells(0); loop.velocity = new Vector2(0, 1500);
            ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
            ui.Open<LevelSelectPanel>(null, UiOpenOptions.NonPausingModal);
            yield return CheckInstantSelection(current, "Reopen " + current);
            ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        }

        bag.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
        Record("Moved backpack opens existing backpack panel", ui.Top is BackPackPanel);
        ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
    }

    private IEnumerator CheckInstantSelection(int levelId, string label)
    {
        var panel = UIManager.Instance.Top as LevelSelectPanel;
        var loop = panel.GetComponentInChildren<LoopVerticalScrollRect>();
        Canvas.ForceUpdateCanvases();
        var row = loop.content.GetComponentsInChildren<LevelSelectLevelRowCell>().FirstOrDefault(r => r.LevelId == levelId);
        bool found = row != null && row.gameObject.activeInHierarchy;
        float initialY = found ? RelativeCenter(loop, row) : float.NaN;
        Record(label + " target visible before any wait", found && FullyVisible(loop, row));
        Record(label + " no inherited scroll velocity", loop.velocity.sqrMagnitude < .01f);
        yield return new WaitForSecondsRealtime(.4f);
        row = loop.content.GetComponentsInChildren<LevelSelectLevelRowCell>().FirstOrDefault(r => r.LevelId == levelId);
        Record(label + " target does not slowly travel after opening", row != null && FullyVisible(loop, row) && Mathf.Abs(RelativeCenter(loop, row) - initialY) < 1,
            "Stage " + levelId + ", first Y=" + initialY + ", settled Y=" + (row != null ? RelativeCenter(loop, row) : float.NaN));
        var locked = loop.content.GetComponentsInChildren<LevelSelectLevelRowCell>().Where(r => r.LevelId > levelId).ToArray();
        Record(label + " later locked stages remain disabled", locked.All(r => !Field<Button>(r, "clickButton").interactable));
    }

    private static bool FullyVisible(LoopVerticalScrollRect loop, LevelSelectLevelRowCell row)
    {
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(loop.viewport, row.transform);
        return bounds.min.y >= loop.viewport.rect.yMin - 1 && bounds.max.y <= loop.viewport.rect.yMax + 1;
    }
    private static float RelativeCenter(LoopVerticalScrollRect loop, LevelSelectLevelRowCell row) =>
        RectTransformUtility.CalculateRelativeRectTransformBounds(loop.viewport, row.transform).center.y;
    private static Button HitButton(Vector2 position)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<Button>() : null;
    }
    private static Rect ScreenRect(RectTransform rt)
    {
        var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
    protected override void OnDestroy()
    {
        PlatformMenuExclusion.EditorMenuRectOverride = null;
        Application.runInBackground = previousBackground; base.OnDestroy();
    }
}
#endif
