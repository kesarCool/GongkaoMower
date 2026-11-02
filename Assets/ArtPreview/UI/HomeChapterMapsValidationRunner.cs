#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class HomeChapterMapsValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "HomeChapterMapsUnity";
    private bool previousBackground;
    private void Awake() { previousBackground = Application.runInBackground; Application.runInBackground = true; }

    protected override IEnumerator RunChecks()
    {
        Record("Three chapter themes configured", map != null && map.mapBackground != null && map.chapterThemes != null && map.chapterThemes.Length == 3);
        var food = map.chapterThemes.First(t => t.chapterId == 1);
        var autumn = map.chapterThemes.First(t => t.chapterId == 2);
        var winter = map.chapterThemes.First(t => t.chapterId == 3);
        Record("Distinct seasonal art", autumn.background != null && winter.background != null && autumn.background != winter.background && food.background != autumn.background);
        foreach (var theme in new[] { autumn, winter })
        {
            var texture = theme.background.texture;
            Record("Chapter " + theme.chapterId + " compressed dimensions", texture.width == 720 && texture.height == 1280);
            Record("Chapter " + theme.chapterId + " Crunch format", texture.format == TextureFormat.DXT1Crunched, texture.format.ToString());
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(theme.background));
            Record("Chapter " + theme.chapterId + " WebGL settings", importer.GetPlatformTextureSettings("WebGL").overridden && !importer.isReadable && !importer.mipmapEnabled);
        }
        Record("Chapter 1 art and points preserved", map.mapBackground.sprite == food.background && map.nodes[1].GetComponent<RectTransform>().anchoredPosition == new Vector2(635, -1342));
        yield return Capture("chapter-01-regression-1080x1920");

        SetProgress(8, 201);
        CheckChapter(2, 8, 201, autumn);
        Record("Chapter 2 initial progress", map.progressText.text == "0 / 8");
        Record("Chapter 2 locked stage rejected", !map.SelectLevel(202) && map.SelectedLevel == 201);
        yield return Capture("chapter-02-first-stage-1080x1920");
        SetProgress(9, 202);
        CheckChapter(2, 8, 202, autumn);
        Record("Chapter 2 stars retained", map.nodes[0].stars.All(s => s.gameObject.activeSelf));
        Record("Chapter 2 progress retained", map.progressText.text == "1 / 8");
        CheckSelectedPosition(autumn, 1, "Autumn current stage");
        Record("Autumn route has independent scroll offset", Mathf.Abs(map.scroll.verticalNormalizedPosition - ExpectedScroll(autumn.points[1])) < .002f);
        yield return Capture("chapter-02-autumn-1080x1920");
        Record("Chapter 2 replay selection", map.SelectLevel(201) && map.mapBackground.sprite == autumn.background);
        map.LocateCurrent();
        Record("Locate API restores chapter 2 current stage", map.SelectedLevel == 202);
        SetProgress(15, 208);
        CheckSelectedPosition(autumn, 7, "Autumn final stage");
        CheckSelectedOnScreen("Autumn final stage");
        yield return Capture("chapter-02-last-stage-1080x1920");

        fixture.SetProgress(16); SelectedLevelContext.Clear(); map.RefreshFromProfile();
        CheckChapter(3, 5, 301, winter);
        Record("Completing chapter 2 automatically opens chapter 3 art", map.VisibleChapter == 3 && map.SelectedLevel == 301);
        Record("Only configured third-chapter levels visible", map.nodes.Skip(5).All(n => !n.gameObject.activeSelf));
        Record("Third chapter progress denominator follows data", map.progressText.text == "0 / 5");
        yield return Capture("chapter-03-first-stage-1080x1920");
        SetProgress(17, 302);
        CheckChapter(3, 5, 302, winter);
        CheckSelectedPosition(winter, 1, "Winter current stage");
        Record("Winter route has independent scroll offset", Mathf.Abs(map.scroll.verticalNormalizedPosition - ExpectedScroll(winter.points[1])) < .002f);
        Record("Winter locked stage rejected", !map.SelectLevel(303) && map.SelectedLevel == 302);
        Record("Winter title and stage name follow table", map.chapterText.text == ChapterLevelDisplay.FormatChapterTitle(3) && map.selectedLevelText.text == ChapterLevelDisplay.FormatLevelName(302));
        int chapter, level;
        Record("Start resolves actual third-chapter selection", HomeHubController.TryResolveStartLevel(out chapter, out level) && chapter == 3 && level == 302);
        yield return Capture("chapter-03-winter-1080x1920");

        map.SelectLevel(102); Record("Replay old chapter restores food art", map.VisibleChapter == 1 && map.mapBackground.sprite == food.background && map.plannedLandmarks.activeSelf);
        map.LocateCurrent(); Record("Locate crosses back to winter chapter", map.VisibleChapter == 3 && map.SelectedLevel == 302 && map.mapBackground.sprite == winter.background);
        SetProgress(20, 305);
        CheckSelectedPosition(winter, 4, "Winter final configured stage");
        CheckSelectedOnScreen("Winter final configured stage");
        Record("Winter final configured stage unlocked", map.nodes[4].button.interactable && map.progressText.text == "4 / 5");
        yield return Capture("chapter-03-last-stage-1080x1920");

        SetProgress(17, 302); tabs.SwitchTo("lexicon"); yield return null; tabs.SwitchTo("battle"); yield return null;
        Record("Tab round trip retains winter map", map.VisibleChapter == 3 && map.SelectedLevel == 302 && map.mapBackground.sprite == winter.background);
        changeResolution(1080, 2400); yield return Capture("chapter-03-winter-1080x2400");
        Record("Winter tall screen preserves world ratio", Mathf.Abs(map.world.localScale.x - map.world.localScale.y) < .001f);
        CheckSelectedOnScreen("Winter tall screen");
        SetProgress(9, 202); yield return Capture("chapter-02-autumn-1080x2400"); CheckSelectedOnScreen("Autumn tall screen");
        changeResolution(360, 640); SetProgress(17, 302); yield return Capture("chapter-03-winter-360x640"); CheckSelectedOnScreen("Winter phone screen");
        changeResolution(1080, 1920); yield return null;
    }

    private void SetProgress(int cleared, int selected)
    {
        fixture.SetProgress(cleared); SelectedLevelContext.Set(selected / 100, selected); map.RefreshFromProfile();
        Canvas.ForceUpdateCanvases();
    }
    private void CheckChapter(int chapter, int count, int selected, HomeChapterMapView.ChapterMapTheme theme)
    {
        Record("Chapter " + chapter + " stage " + selected + " art and node count", map.VisibleChapter == chapter && map.SelectedLevel == selected &&
            map.mapBackground.sprite == theme.background && map.nodes.Count(n => n.gameObject.activeSelf) == count);
        Record("Chapter " + chapter + " route points", map.nodes.Take(count).Select((node, i) =>
            Vector2.Distance(node.GetComponent<RectTransform>().anchoredPosition, new Vector2(theme.points[i].x, -theme.points[i].y)) < .01f).All(v => v));
        Record("Chapter " + chapter + " planned landmarks hidden", !map.plannedLandmarks.activeSelf);
    }
    private void CheckSelectedPosition(HomeChapterMapView.ChapterMapTheme theme, int index, string label)
    {
        var point = theme.points[index];
        Record(label + " hero follows seasonal route", Vector2.Distance(map.hero.anchoredPosition, new Vector2(point.x, -(point.y - 50))) < .01f);
    }
    private float ExpectedScroll(Vector2 point)
    {
        float max = Mathf.Max(0, map.content.rect.height - map.scroll.viewport.rect.height);
        return max > 0 ? 1 - Mathf.Clamp(point.y * map.MapScale - map.scroll.viewport.rect.height * .68f, 0, max) / max : 1;
    }
    private void CheckSelectedOnScreen(string label)
    {
        var node = map.nodes.First(n => n.gameObject.activeSelf && n.IsSelected);
        var screen = RectTransformUtility.WorldToScreenPoint(null, node.transform.position);
        Record(label + " selected node visible", screen.x >= 0 && screen.x <= Screen.width && screen.y >= 0 && screen.y <= Screen.height);
    }
    protected override void OnDestroy() { Application.runInBackground = previousBackground; base.OnDestroy(); }
}
#endif
