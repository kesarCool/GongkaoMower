using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared-art transition page, independent of the operations driving its progress.</summary>
[DisallowMultipleComponent]
public sealed class BattleLoadingPresentation : MonoBehaviour
{
    public Image background, heroPortrait;
    public Sprite[] chapterBackgrounds;
    public CharacterCatalog characters;
    public RectTransform design, hero;
    public CanvasGroup canvasGroup;
    public Slider progress;
    public TMP_Text chapter, level, status, percent, tip;
    public Button retry;
    public GameObject tipRoot;
    public RectTransform shine;
    public float DisplayedProgress { get; private set; }
    public float TargetProgress { get; private set; }
    public bool HasError { get; private set; }
    private Rect lastSafe;
    private Rect lastMenu;
    private Vector2 lastSize, heroRest;
    private float openedAt;
    private static readonly string[] Tips = {
        "边走边躲，技能会自动攻击。",
        "收集绿色能量，升级后选择新技能。",
        "主动技能搭配羁绊被动，可以解锁突破。"
    };

    private void Awake()
    {
        openedAt = Time.unscaledTime;
        if (hero != null) heroRest = hero.anchoredPosition;
        ApplyTheme(); BindHero(); ApplyLayout(); ResetProgress();
    }

    public void RefreshSelectedContent()
    {
        ApplyTheme();
        int id = SelectedLevelContext.HasSelection ? SelectedLevelContext.LevelId : 101;
        if (chapter != null) chapter.text = ChapterLevelDisplay.FormatChapterTitle(id / 100);
        if (level != null) level.text = ChapterLevelDisplay.FormatLevelName(id);
        BindHero();
    }

    private void BindHero()
    {
        PlayerProfileService.Instance.LoadOrCreate();
        var selected = characters != null ? characters.Get(PlayerProfileService.Instance.EquippedCharacterId) : null;
        if (heroPortrait != null)
        {
            heroPortrait.sprite = selected != null ? selected.portrait : null;
            heroPortrait.enabled = heroPortrait.sprite != null;
        }
    }

    private void ApplyTheme()
    {
        int id = SelectedLevelContext.HasSelection ? SelectedLevelContext.LevelId : 101;
        int index = Mathf.Clamp(id / 100 - 1, 0, chapterBackgrounds != null ? Mathf.Max(0, chapterBackgrounds.Length - 1) : 0);
        if (background != null && chapterBackgrounds != null && chapterBackgrounds.Length > index && chapterBackgrounds[index] != null)
            background.sprite = chapterBackgrounds[index];
        if (level != null) level.text = $"关卡 {id / 100}-{id % 100}";
    }

    public void ResetProgress()
    {
        HasError = false; TargetProgress = DisplayedProgress = 0;
        if (progress != null) progress.SetValueWithoutNotify(0);
        if (percent != null) percent.text = "0%";
        if (retry != null) retry.gameObject.SetActive(false);
        if (tipRoot != null) tipRoot.SetActive(true);
        if (canvasGroup != null) { canvasGroup.alpha = 1; canvasGroup.blocksRaycasts = true; }
    }

    public void Report(float value, string message = null)
    {
        TargetProgress = Mathf.Max(TargetProgress, Mathf.Clamp01(value));
        if (message != null && status != null) status.text = message;
    }

    public void ShowError(string message)
    {
        HasError = true;
        if (status != null) status.text = message;
        if (retry != null) retry.gameObject.SetActive(true);
        if (tipRoot != null) tipRoot.SetActive(false);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        DisplayedProgress = Mathf.MoveTowards(DisplayedProgress, TargetProgress, dt * 3);
        if (progress != null) progress.SetValueWithoutNotify(DisplayedProgress);
        if (percent != null) percent.text = Mathf.FloorToInt(DisplayedProgress * 100 + .001f) + "%";
        if (hero != null) hero.anchoredPosition = heroRest + new Vector2(0, Mathf.Sin((Time.unscaledTime - openedAt) * 2.7f) * 6);
        if (shine != null)
        {
            shine.gameObject.SetActive(!HasError && DisplayedProgress > .01f);
            if (shine.gameObject.activeSelf)
                shine.anchoredPosition = new Vector2(Mathf.Repeat((Time.unscaledTime - openedAt) * 300, 960) - 80, 0);
        }
        if (tip != null && !HasError) tip.text = Tips[Mathf.FloorToInt((Time.unscaledTime - openedAt) / 4) % Tips.Length];
    }

    private void LateUpdate()
    {
        var size = ((RectTransform)transform).rect.size;
        if (size != lastSize || Screen.safeArea != lastSafe || PlatformMenuExclusion.MenuRect != lastMenu) ApplyLayout();
    }

    public void ApplyLayout()
    {
        var root = (RectTransform)transform;
        if (root.rect.width <= 0 || root.rect.height <= 0 || design == null) return;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        Rect menu = PlatformMenuExclusion.MenuRect;
        if (menu.width > 0)
            safe.yMax = Mathf.Max(safe.yMin + 1, Mathf.Min(safe.yMax, (1 - menu.yMax) * Screen.height - 16));
        float fullW = root.rect.width, fullH = root.rect.height;
        float safeW = fullW * safe.width / Mathf.Max(1, Screen.width);
        float safeH = fullH * safe.height / Mathf.Max(1, Screen.height);
        design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
        design.sizeDelta = new Vector2(1080, 1920);
        design.localScale = Vector3.one * Mathf.Min(safeW / 1080, safeH / 1920);
        design.anchoredPosition = new Vector2((safe.center.x / Mathf.Max(1, Screen.width) - .5f) * fullW,
            (safe.center.y / Mathf.Max(1, Screen.height) - .5f) * fullH);
        if (background != null)
        {
            float ratio = background.sprite != null ? background.sprite.rect.width / background.sprite.rect.height : 1080f / 1920;
            var rt = background.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Max(fullW, fullH * ratio), Mathf.Max(fullH, fullW / ratio));
        }
        lastSize = root.rect.size; lastSafe = Screen.safeArea; lastMenu = menu;
    }

    public IEnumerator RevealBattle()
    {
        Report(1, "准备就绪，开始割草！");
        while (DisplayedProgress < .999f) yield return null;
        yield return null;
        float elapsed = 0;
        while (elapsed < .22f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .22f));
            yield return null;
        }
        if (canvasGroup != null) { canvasGroup.alpha = 0; canvasGroup.blocksRaycasts = false; }
    }
}
