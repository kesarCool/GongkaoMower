using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 词汇预览弹窗：与 <c>LexiconPreviewPanel.prefab</c> 一致——<c>ThemePack</c> / <c>CategoryTag</c> 下按表数据生成 <see cref="Toggle"/>，
/// 主题、分类各为 <b>单选</b>（父节点上 <see cref="ToggleGroup"/>，<c>allowSwitchOff = false</c>）；词条在 <c>Loop Vertical Scroll Rect</c> 中展示。
/// </summary>
[DisallowMultipleComponent]
public class LexiconPreviewPanel : UIPanelBase
{
    public const int DefaultThemePackId = LexiconCategoryTags.ThemePackFood;
    public const int DefaultCategoryTag = LexiconCategoryTags.FoodJingJinJi;

    [Header("Prefab 引用（ThemePack / CategoryTag 下各保留一个 Toggle 作模板，运行时会隐藏）")]
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform themeFilterRoot;
    [SerializeField] private RectTransform categoryFilterRoot;
    [SerializeField] private Toggle themeToggleTemplate;
    [SerializeField] private Toggle categoryToggleTemplate;
    [SerializeField] private LoopVerticalScrollRect loopScroll;
    [SerializeField] private LexiconPreviewLoopScrollDriver loopDriver;
    [SerializeField] private RectTransform listContent;

    private int _selectedThemeId;
    private int _selectedCategoryTag;

    private readonly List<Toggle> _themeToggles = new List<Toggle>();
    private readonly List<Toggle> _categoryToggles = new List<Toggle>();
    private readonly List<int> _themeIds = new List<int>();
    private readonly List<int> _categoryIds = new List<int>();

    private void Awake()
    {
      //  EnsureFilterLayout(themeFilterRoot);
       // EnsureFilterLayout(categoryFilterRoot);
        EnsureToggleGroup(themeFilterRoot);
        EnsureToggleGroup(categoryFilterRoot);

        if (loopDriver == null && loopScroll != null)
            loopDriver = loopScroll.GetComponent<LexiconPreviewLoopScrollDriver>();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private static void EnsureToggleGroup(RectTransform root)
    {
        if (root == null)
            return;
        var g = root.GetComponent<ToggleGroup>();
        if (g == null)
            g = root.gameObject.AddComponent<ToggleGroup>();
        g.allowSwitchOff = false;
    }

    private static void EnsureFilterLayout(RectTransform root)
    {
        if (root == null)
            return;
        if (root.GetComponent<HorizontalLayoutGroup>() == null 
            ||root.GetComponent<VerticalLayoutGroup>() == null)
        {
            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = false;
            h.childForceExpandWidth = false;
            h.padding = new RectOffset(8, 8, 4, 4);
        }

        if (root.GetComponent<ContentSizeFitter>() == null)
        {
            var f = root.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    public override void OnOpen(object payload)
    {
        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (loopDriver == null && loopScroll != null)
            loopDriver = loopScroll.GetComponent<LexiconPreviewLoopScrollDriver>();

        ApplyDefaultSelection();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        // 仅在弹窗模式下关闭（Tab 模式下 closeButton 不绑，但此方法不会被调用）
        if (UIManager.Instance != null && UIManager.Instance.TryGetInstance(out LexiconPreviewPanel _))
            UIManager.Instance.CloseTop();
    }

    private void RebuildThemeFilters()
    {
        if (themeFilterRoot == null || themeToggleTemplate == null)
        {
            Debug.LogWarning("[LexiconPreviewPanel] themeFilterRoot 或 themeToggleTemplate 未绑定。");
            return;
        }

        ClearFilterChildrenExcept(themeFilterRoot, themeToggleTemplate.transform);
        _themeToggles.Clear();
        _themeIds.Clear();
        themeToggleTemplate.gameObject.SetActive(false);

        var themeGroup = themeFilterRoot.GetComponent<ToggleGroup>();
        var themes = LexiconPreviewCatalog.CollectThemePackIds();
        foreach (var tid in themes)
        {
            _themeIds.Add(tid);
            var label = LexiconPreviewCatalog.GetThemeTabLabel(tid);
            var initialOn = tid == _selectedThemeId;
            var toggle = InstantiateToggleFrom(themeToggleTemplate, themeFilterRoot, label, initialOn, on => OnThemeToggleChanged(tid, on), themeGroup);
            _themeToggles.Add(toggle);
        }

        // ToggleGroup allowSwitchOff=false 可能在后续 Toggle 注册时覆盖 SetIsOnWithoutNotify，
        // 循环结束后把正确 Toggle 的选中态拉回来。
        FixupToggleSelection(_themeToggles, _themeIds, _selectedThemeId);
    }

    private void RebuildCategoryFilters()
    {
        if (categoryFilterRoot == null || categoryToggleTemplate == null)
        {
            Debug.LogWarning("[LexiconPreviewPanel] categoryFilterRoot 或 categoryToggleTemplate 未绑定。");
            return;
        }

        ClearFilterChildrenExcept(categoryFilterRoot, categoryToggleTemplate.transform);
        _categoryToggles.Clear();
        _categoryIds.Clear();
        categoryToggleTemplate.gameObject.SetActive(false);

        var categoryGroup = categoryFilterRoot.GetComponent<ToggleGroup>();
        var cats = _selectedThemeId > 0
            ? LexiconPreviewCatalog.CollectCategoryTagsForTheme(_selectedThemeId)
            : new List<int>();

        foreach (var cid in cats)
        {
            _categoryIds.Add(cid);
            var label = LexiconPreviewCatalog.GetCategoryTabLabel(_selectedThemeId, cid);
            var initialOn = cid == _selectedCategoryTag;
            var toggle = InstantiateToggleFrom(categoryToggleTemplate, categoryFilterRoot, label, initialOn, on => OnCategoryToggleChanged(cid, on), categoryGroup);
            _categoryToggles.Add(toggle);
        }

        FixupToggleSelection(_categoryToggles, _categoryIds, _selectedCategoryTag);
    }

    private static Toggle InstantiateToggleFrom(Toggle template, RectTransform parent, string label, bool initialOn, UnityAction<bool> handler, ToggleGroup group)
    {
        var go = Instantiate(template.gameObject, parent, false);
        go.SetActive(true);
        var toggle = go.GetComponent<Toggle>();
        toggle.group = group;
        toggle.SetIsOnWithoutNotify(initialOn);
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(handler);

        var labelTx = toggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (labelTx != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(labelTx);
            labelTx.text = label;
        }

        return toggle;
    }

    /// <summary>
    /// ToggleGroup 的 <c>allowSwitchOff=false</c> 可能在后续 Toggle 注册到 Group 时覆盖掉
    /// <see cref="Toggle.SetIsOnWithoutNotify"/> 的结果。在所有 Toggle 创建完毕后，
    /// 把匹配 <paramref name="selectedId"/> 的 Toggle 强行拉回选中态。
    /// </summary>
    private static void FixupToggleSelection(List<Toggle> toggles, List<int> ids, int selectedId)
    {
        if (toggles.Count == 0 || ids.Count != toggles.Count)
            return;
        for (int i = 0; i < toggles.Count; i++)
        {
            if (ids[i] == selectedId && !toggles[i].isOn)
            {
                toggles[i].SetIsOnWithoutNotify(true);
                break;
            }
        }
    }

    private static void ClearFilterChildrenExcept(RectTransform root, Transform keep)
    {
        if (root == null)
            return;
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            if (c == keep)
                continue;
            Destroy(c.gameObject);
        }
    }

    private void OnThemeToggleChanged(int themePackId, bool isOn)
    {
        if (!isOn)
            return;

        UiClickSound.Play();
        _selectedThemeId = themePackId;
        AdjustCategoryAfterThemeChanged();
        RebuildCategoryFilters();
        RefreshEntryList();
    }

    private void OnCategoryToggleChanged(int categoryTag, bool isOn)
    {
        if (!isOn)
            return;

        UiClickSound.Play();
        _selectedCategoryTag = categoryTag;
        RefreshEntryList();
    }

    private void AdjustCategoryAfterThemeChanged()
    {
        var available = LexiconPreviewCatalog.CollectCategoryTagsForTheme(_selectedThemeId);
        if (available.Count == 0)
        {
            _selectedCategoryTag = 0;
            return;
        }

        if (_selectedCategoryTag == 0 || !available.Contains(_selectedCategoryTag))
            _selectedCategoryTag = available.Contains(DefaultCategoryTag) ? DefaultCategoryTag : available[0];
    }

    private void ApplyDefaultSelection()
    {
        var themes = LexiconPreviewCatalog.CollectThemePackIds();
        _selectedThemeId = 0;
        _selectedCategoryTag = 0;

        if (themes.Count == 0)
        {
            RebuildThemeFilters();
            RebuildCategoryFilters();
            RefreshEntryList();
            return;
        }

        _selectedThemeId = themes.Contains(DefaultThemePackId) ? DefaultThemePackId : themes[0];
        AdjustCategoryAfterThemeChanged();

        RebuildThemeFilters();
        RebuildCategoryFilters();
        RefreshEntryList();
    }

    private void RefreshEntryList()
    {
        if (loopDriver == null)
            return;

        List<string> lines;
        if (_selectedThemeId == 0 || _selectedCategoryTag == 0)
            lines = new List<string>();
        else
            lines = LexiconPreviewCatalog.CollectDisplayTexts(_selectedThemeId, _selectedCategoryTag);

        if (lines.Count == 0)
            loopDriver.SetLines(new List<string> { "（无词条或未编译 USE_FB_TABLE / 表未加载）" });
        else
            loopDriver.SetLines(lines);

        if (listContent != null && loopScroll != null && loopScroll.viewport != null)
        {
            var w = ((RectTransform)loopScroll.viewport).rect.width - 16f;
            listContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(120f, w));
        }

        if (listContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            Canvas.ForceUpdateCanvases();
        }

        if (loopScroll != null)
            loopScroll.verticalNormalizedPosition = 0f;
    }
}
