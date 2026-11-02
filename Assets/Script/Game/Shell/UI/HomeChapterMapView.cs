using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Maps existing chapter progress onto the approved illustrated route, eight nodes per page.</summary>
public sealed class HomeChapterMapView : MonoBehaviour
{
    public ScrollRect scroll;
    public RectTransform content, world, hero;
    public Image heroPortrait;
    public HomeMapNode[] nodes;
    public CharacterCatalog characterCatalog;
    public TMP_Text chapterText, progressText, selectedLevelText;
    public Button locateButton, startButton;
    public GameObject plannedLandmarks;
    public Image mapBackground;
    public ChapterMapTheme[] chapterThemes;
    public int VisibleChapter { get; private set; } = 1;
    public int SelectedLevel => SelectedLevelContext.HasSelection ? SelectedLevelContext.LevelId : 0;
    public int CurrentProgressLevel { get; private set; } = 101;
    public float MapScale { get; private set; } = 1;
    private Vector2 lastViewport;
    private bool ready;
    private readonly List<int> levels = new List<int>();
    private static readonly Vector2[] Points = { new Vector2(288,1473), new Vector2(635,1342), new Vector2(598,1082), new Vector2(437,960), new Vector2(660,793), new Vector2(567,651), new Vector2(713,490), new Vector2(695,265) };
    private Vector2[] activePoints = Points;
    private Sprite defaultBackground;

    [System.Serializable]
    public sealed class ChapterMapTheme
    {
        public int chapterId;
        public Sprite background;
        // Illustration coordinates remain 1080 x 1920 even when the texture is downsampled.
        public Vector2[] points;
    }

    private void Awake()
    {
        if (mapBackground != null) defaultBackground = mapBackground.sprite;
        if (locateButton != null) locateButton.onClick.AddListener(LocateCurrent);
        if (scroll != null) scroll.onValueChanged.AddListener(_ => PlaceHero());
    }

    private IEnumerator Start() { yield return null; RefreshFromProfile(); }
    private void OnEnable() { if (ready) RefreshFromProfile(); }
    private void LateUpdate()
    {
        if (scroll != null && scroll.viewport.rect.size != lastViewport)
        { ResizeWorld(); if (ready) ScrollToSelected(); }
    }

    public void RefreshFromProfile()
    {
        if (scroll == null || nodes == null || nodes.Length == 0) return;
        TableManager.Instance.Init();
        var profile = PlayerProfileService.Instance; profile.LoadOrCreate();
        int chapter, latest;
        if (!ChapterLevelNavigation.TryGetMaxUnlockedLevel(out chapter, out latest)) { chapter = 1; latest = 101; }
        CurrentProgressLevel = latest;
        int selected = SelectedLevelContext.HasSelection && profile.IsLevelUnlocked(SelectedLevelContext.LevelId) ? SelectedLevelContext.LevelId : latest;
        VisibleChapter = selected / 100;
        ApplyChapterTheme();
        SelectedLevelContext.Set(VisibleChapter, selected);
        levels.Clear();
#if USE_FB_TABLE
        var table = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        if (table != null) foreach (var entry in table)
            if (entry.Value is ProtoTable.ChapterLevel row && row.chapterId == VisibleChapter) levels.Add(row.levelId);
#endif
        if (levels.Count == 0) for (int i = 1; i <= 8; i++) levels.Add(VisibleChapter * 100 + i);
        levels.Sort();
        int selectedIndex = Mathf.Max(0, levels.IndexOf(selected));
        int page = selectedIndex / nodes.Length;
        int clearedCount = 0;
        foreach (int level in levels) if (profile.HasCleared(level)) clearedCount++;
        if (chapterText != null) chapterText.text = ChapterLevelDisplay.FormatChapterTitle(VisibleChapter);
        if (progressText != null) progressText.text = $"{clearedCount} / {levels.Count}";
        if (selectedLevelText != null) selectedLevelText.text = ChapterLevelDisplay.FormatLevelName(selected);
        for (int i = 0; i < nodes.Length; i++)
        {
            int index = page * nodes.Length + i;
            nodes[i].gameObject.SetActive(index < levels.Count);
            if (index >= levels.Count) continue;
            int id = levels[index];
            LevelProgressEntry progress;
            profile.TryGetProgress(id, out progress);
            nodes[i].Bind(id, profile.IsLevelUnlocked(id), progress != null && progress.cleared,
                progress != null ? Mathf.Clamp(progress.stars, 0, 3) : 0, id == selected, SelectLevelFromButton);
            Vector2 point = PointAt(i);
            nodes[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(point.x, -point.y);
        }
        if (plannedLandmarks != null) plannedLandmarks.SetActive(VisibleChapter == 1);
        if (startButton != null) startButton.interactable = profile.IsLevelUnlocked(selected);
        var def = characterCatalog != null ? characterCatalog.Get(profile.EquippedCharacterId) : null;
        if (heroPortrait != null) { heroPortrait.sprite = def != null ? def.portrait : null; heroPortrait.enabled = heroPortrait.sprite != null; }
        ready = true;
        Canvas.ForceUpdateCanvases(); ResizeWorld(); ScrollToSelected();
    }

    private void SelectLevelFromButton(int id) { SelectLevel(id); }

    private void ApplyChapterTheme()
    {
        activePoints = Points;
        Sprite background = defaultBackground;
        if (chapterThemes != null) foreach (var theme in chapterThemes)
        {
            if (theme == null || theme.chapterId != VisibleChapter) continue;
            if (theme.background != null) background = theme.background;
            if (theme.points != null && theme.points.Length > 0) activePoints = theme.points;
            break;
        }
        if (mapBackground != null && background != null) mapBackground.sprite = background;
    }

    private Vector2 PointAt(int index)
    {
        if (activePoints != null && index >= 0 && index < activePoints.Length) return activePoints[index];
        return Points[Mathf.Clamp(index, 0, Points.Length - 1)];
    }
    public bool SelectLevel(int id)
    {
        if (!PlayerProfileService.Instance.IsLevelUnlocked(id)) return false;
        SelectedLevelContext.Set(id / 100, id);
        RefreshFromProfile(); return true;
    }

    public void LocateCurrent()
    {
        int chapter, id;
        if (ChapterLevelNavigation.TryGetMaxUnlockedLevel(out chapter, out id))
        { SelectedLevelContext.Set(chapter, id); RefreshFromProfile(); }
    }

    private void ResizeWorld()
    {
        if (scroll == null || world == null || content == null) return;
        lastViewport = scroll.viewport.rect.size;
        MapScale = Mathf.Max(1, lastViewport.y / 1920f);
        content.sizeDelta = new Vector2(1080 * MapScale, 1920 * MapScale);
        world.localScale = Vector3.one * MapScale;
    }

    public void ScrollToSelected()
    {
        int index = FindSelectedIndex(); if (index < 0) return;
        float maxScroll = Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
        float offset = Mathf.Clamp(PointAt(index).y * MapScale - scroll.viewport.rect.height * 0.68f, 0, maxScroll);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = maxScroll > 0 ? 1 - offset / maxScroll : 1;
        PlaceHero();
    }

    private int FindSelectedIndex()
    {
        for (int i = 0; i < nodes.Length; i++) if (nodes[i].gameObject.activeSelf && nodes[i].IsSelected) return i;
        return -1;
    }

    private void PlaceHero()
    {
        if (hero == null || content == null || scroll == null) return;
        int index = FindSelectedIndex(); hero.gameObject.SetActive(index >= 0);
        if (index < 0) return;
        float offset = (1 - scroll.verticalNormalizedPosition) * Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
        Vector2 point = PointAt(index);
        float available = point.y - offset / MapScale - 46 - 116 / MapScale;
        float height = Mathf.Clamp(available, 100, 172.8f);
        hero.sizeDelta = new Vector2(height * 166.32f / 172.8f, height);
        hero.anchoredPosition = new Vector2(point.x, -(point.y - 46 - 4));
    }

    public static string ChapterName(int chapter) => ChapterLevelDisplay.ResolveChapterName(chapter) ?? $"第{chapter}章";
}
