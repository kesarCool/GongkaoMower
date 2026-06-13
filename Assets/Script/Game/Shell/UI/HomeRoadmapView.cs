using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home 纵向路线图：一行一节点，左右交替上升，节点间 V 形双斜线连接，章节间横线分割。
///
/// ════ 视觉控制点 ════
/// 【Inspector 可调】
///   nodeSize         — 节点宽高（默认 130）
///   rowHeight        — 节点行间距（默认 220）
///   sideOffset       — 节点离中轴水平偏移（默认 300）
///   dividerHeight    — 章节分割线区域高度（默认 50）
///   paddingBottom/Top— 内容区上下留白（默认 300）
///   lineActiveColor  — 已解锁连线颜色
///   lineDimColor     — 未解锁连线颜色
///   dividerColor     — 章节分割线颜色
///   popupPanelSize   — 详情弹窗尺寸（默认 460×300）
///   popupTitle/Star/Detail/ButtonFontSize — 弹窗各级字号
///   popupButtonSize  — 弹窗按钮尺寸
/// 【代码内常量】
///   MakeDivider()    — 分割线线粗(2px)、章节名字号(22)
///   ShowDetailPopup()— 弹窗面板色、按钮色
///   ScrollToCurrentLevel() — 滚动定位公式
/// </summary>
[DisallowMultipleComponent]
public class HomeRoadmapView : MonoBehaviour
{
    [Header("滚动")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    [Header("节点")]
    [SerializeField] private RoadmapNodeView nodePrefab;

    [Header("布局")]
    [Tooltip("节点大小（宽高一致）")]
    [SerializeField] private float nodeSize = 130f;
    [SerializeField] private float rowHeight = 220f;       // 每行高度
    [SerializeField] private float sideOffset = 300f;      // 左右偏移量
    [SerializeField] private float dividerHeight = 50f;    // 章节分割线高度
    [SerializeField] private float paddingBottom = 300f;
    [SerializeField] private float paddingTop = 300f;

    [Header("节点外观")]
    [Tooltip("关卡圆点 Sprite（拖入则覆盖 Prefab 默认值；留空走程序生成圆点）。")]
    [SerializeField] private Sprite nodeCircleSprite;
    [Tooltip("程序生成圆点的纹理尺寸（仅在 nodeCircleSprite 为空时生效）。")]
    [SerializeField] private int circleTexSize = 128;

    [Header("当前英雄")]
    [Tooltip("拖拽 CharacterCatalog（Assets/ScriptableObject/Character/ 下），否则头像无法加载。")]
    [SerializeField] private CharacterCatalog characterCatalog;
    [SerializeField] private Sprite currentHeroSprite;

    [Header("颜色")]
    [SerializeField] private Color lineActiveColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    [SerializeField] private Color lineDimColor = new Color(0.2f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color dividerColor = new Color(0.35f, 0.35f, 0.4f, 1f);

    [Header("详情弹窗")]
    [SerializeField] private Vector2 popupPanelSize = new Vector2(460f, 300f);
    [SerializeField] private Vector2 popupButtonSize = new Vector2(210f, 54f);
    [SerializeField] private float popupTitleFontSize = 34f;
    [SerializeField] private float popupStarFontSize = 40f;
    [SerializeField] private float popupDetailFontSize = 24f;
    [SerializeField] private float popupButtonFontSize = 26f;

    private Sprite _circleSprite;
    private Sprite _whitePixelSprite;  // 用于连线 Image（无 sprite 不渲染）
    private float _contentHeight;
    private int _currentLevelId;
    private bool _built;
    private RectTransform _layoutRoot;
    private GameObject _activePopup;
    private readonly List<RoadmapNodeView> _nodes = new List<RoadmapNodeView>();

    // ═══════════════ 生命周期 ═══════════════

    private IEnumerator Start()
    {
        if (_circleSprite == null) _circleSprite = GenerateCircleSprite(circleTexSize);
        if (_whitePixelSprite == null) _whitePixelSprite = GenerateWhitePixelSprite();
        PlayerProfileService.Instance.LoadOrCreate();
        TableManager.Instance.Init();
        ResolveCurrentHeroPortrait(); // 必须在 LoadOrCreate 之后
        if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        yield return null;
        yield return null;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        BuildAll();
        yield return null;
        Canvas.ForceUpdateCanvases();
        ScrollToCurrentLevel();
    }

    private void OnEnable()
    {
        // 切回 battle 页签时：全量重建（头像/进度可能在角色页签里变了）
        if (_built)
            RefreshAll();
    }

    private void OnDestroy()
    {
        if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    public void RefreshAll()
    {
        PlayerProfileService.Instance.LoadOrCreate();
        // 每次刷新时重新获取当前上阵角色头像
        ResolveCurrentHeroPortrait();
        ClearAll();
        _built = false;
        BuildAll();
        if (gameObject.activeInHierarchy)
            StartCoroutine(DelayedScroll());
    }

    private void ResolveCurrentHeroPortrait()
    {
        // Inspector 绑定的优先；未绑则兜底尝试 Resources（需在 Resources 目录下才有效）
        var cat = characterCatalog;
        if (cat == null)
            cat = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
        string charId = PlayerProfileService.Instance.EquippedCharacterId;
        var def = cat?.Get(charId);
        currentHeroSprite = def != null ? def.portrait : null;
        Debug.Log($"[Roadmap] 头像加载: catalog={cat != null}, charId={charId ?? "null"}, def={def != null}, portrait={currentHeroSprite != null}");
    }

    private IEnumerator DelayedScroll() { yield return null; ScrollToCurrentLevel(); }
    public void SetHeroSprite(Sprite s) { currentHeroSprite = s; RefreshAll(); }

    // ═══════════════ 构建 ═══════════════

    private void BuildAll()
    {
        if (_built || content == null || nodePrefab == null) return;

        // 清空
        for (int i = content.childCount - 1; i >= 0; i--)
            DestroyImmediate(content.GetChild(i).gameObject);
        _nodes.Clear();

        var allChapters = GetOrderedChapters();
        if (allChapters.Count == 0) return;
        _currentLevelId = ResolveCurrentLevelId();

        // ── 章节显示规则：当前所在章 + 下一章（预览）──
        int currentChapterIdx = FindChapterIndex(allChapters, _currentLevelId);
        int maxShowChapterIdx = Mathf.Min(currentChapterIdx + 1, allChapters.Count - 1);
        if (currentChapterIdx < 0) currentChapterIdx = 0;

        // ── 收集关卡数据 ──
        var allLevels = new List<(LevelData d, int chIdx, string chapterName)>();
        for (int ci = 0; ci <= maxShowChapterIdx; ci++)
        {
            var levels = GetChapterLevels(allChapters[ci].chapterId);
            if (levels.Count == 0) continue;
            // 当前及之前章节必须已解锁；下一章（预览）即使未解锁也展示（灰显）
            if (ci <= currentChapterIdx && !levels[0].unlocked) break;
            string chName = $"第{allChapters[ci].chapterId}章";
            foreach (var lv in levels)
                allLevels.Add((lv, ci, chName));
        }

        if (allLevels.Count == 0) return;

        // ── 算高度（每个章节开头一条分割线）──
        int totalRows = allLevels.Count;
        int dividerCount = 0;
        int lastCh = -1;
        for (int i = 0; i < allLevels.Count; i++)
        {
            if (allLevels[i].chIdx != lastCh)
                dividerCount++;
            lastCh = allLevels[i].chIdx;
        }

        // 获取视口尺寸，保证 content 高度 ≥ 视口高度，否则 ScrollRect 钳在顶部无法居中
        float vh = scrollRect?.viewport?.rect.height ?? 1920f;
        float vw = scrollRect?.viewport?.rect.width ?? 1080f;

        float rawContentHeight = paddingBottom + paddingTop + totalRows * rowHeight + dividerCount * dividerHeight;
        // 保证 content 比视口高至少一个视口，让所有关卡都有充分滚动空间
        _contentHeight = rawContentHeight + vh;
        float nodeAreaW = sideOffset * 2f + 200f;              // 节点区域所需宽度
        float cw = Mathf.Max(nodeAreaW, vw);                   // content 宽度 ≥ 视口宽
        content.sizeDelta = new Vector2(cw, _contentHeight);

        // LayoutRoot：填满 content
        var lrGo = new GameObject("LayoutRoot", typeof(RectTransform));
        lrGo.transform.SetParent(content, false);
        _layoutRoot = lrGo.GetComponent<RectTransform>();
        _layoutRoot.anchorMin = _layoutRoot.anchorMax = new Vector2(0, 0);
        _layoutRoot.pivot = new Vector2(0, 0);
        _layoutRoot.anchoredPosition = Vector2.zero;
        _layoutRoot.sizeDelta = new Vector2(cw, _contentHeight);

        float contentCenterX = cw * 0.5f; // 中心 X

        // ── 逐行放置 ──
        float yCursor = paddingBottom;
        lastCh = -1;
        int globalRow = 0;

        for (int i = 0; i < allLevels.Count; i++)
        {
            var (d, chIdx, chName) = allLevels[i];

            // 章节分割线（每个章节开头放置，含第一章）
            if (chIdx != lastCh)
            {
                MakeDivider(yCursor, contentCenterX, cw, chName);
                yCursor += dividerHeight;
            }
            lastCh = chIdx;

            // 左右交替：偶数行在左，奇数行在右
            bool onLeft = (globalRow % 2 == 0);
            float nx = onLeft ? contentCenterX - sideOffset : contentCenterX + sideOffset;
            float ny = yCursor + rowHeight * 0.5f;

            var node = Instantiate(nodePrefab, _layoutRoot, false);
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(nx, ny);
            rt.sizeDelta = new Vector2(180, 90);
            // 优先 Inspector 配置的圆点 sprite，未配则兜底程序生成
            Sprite circle = nodeCircleSprite ? nodeCircleSprite : _circleSprite;
            node.SetCircleSprite(circle);

            node.Bind(d.levelId, d.chapterId, d.unlocked, d.cleared, d.stars,
                d.mapName, d.levelId == _currentLevelId,
                d.levelId == _currentLevelId ? currentHeroSprite : null,
                OnNodeClicked);
            _nodes.Add(node);

            // V 形斜线在节点上自带，后面统一设置

            yCursor += rowHeight;
            globalRow++;
        }

        // 设置节点间 V 形斜线状态
        SetNodeLines();

        // 居中对齐：把节点区域（高 rawContentHeight）在 content 内居中
        float offsetX = Mathf.Max(0, (vw - nodeAreaW) * 0.5f);
        float offsetY = Mathf.Max(0, (_contentHeight - rawContentHeight) * 0.5f); // 额外的视口补足高度均分
        _layoutRoot.anchoredPosition = new Vector2(offsetX, offsetY);

        _built = true;
        Debug.Log($"[Roadmap] {allLevels.Count} 节点, contentH={_contentHeight:F0}, rawH={rawContentHeight:F0}, offset=({offsetX:F0},{offsetY:F0})");
    }

    private void SetNodeLines()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            bool lastNode = (i == _nodes.Count - 1);
            if (lastNode)
            {
                _nodes[i].HideLines();
                continue;
            }

            // 已解锁的下一节点 → 亮色连线；未解锁 → 暗色
            bool lineActive = _nodes[i + 1].IsUnlocked;
            Color color = lineActive ? lineActiveColor : lineDimColor;

            // 当前 i 偶数=节点在左，下一节点在右 → 亮 rightUpLine，隐藏 leftUpLine
            // 当前 i 奇数=节点在右，下一节点在左 → 亮 leftUpLine，隐藏 rightUpLine
            bool nextOnRight = (i % 2 == 0);
            _nodes[i].SetLineState(
                leftActive: !nextOnRight,
                rightActive: nextOnRight,
                color, color, _whitePixelSprite);
        }
    }

    private void MakeDivider(float yBase, float cx, float cw, string chapterName)
    {
        float y = yBase + dividerHeight * 0.5f;
        // 横线
        var go = new GameObject("Div", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_layoutRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, y);
        rt.sizeDelta = new Vector2(cw * 0.7f, 2f);
        go.GetComponent<Image>().color = dividerColor;
        go.GetComponent<Image>().raycastTarget = false;

        // 章节名（居中，横线上方）
        var lblGo = new GameObject("ChName", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(_layoutRoot, false);
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(cx, y + 24f);
        lrt.sizeDelta = new Vector2(cw * 0.7f, 50f);
        var tmp = lblGo.GetComponent<TextMeshProUGUI>();
        tmp.text = chapterName;
        tmp.fontSize = 42;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = dividerColor;
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToTMP(tmp);
    }

    // ═══════════════ 滚动 ═══════════════

    private void OnScrollValueChanged(Vector2 _)
    {
        if (content == null || scrollRect == null) return;
        float vh = scrollRect.viewport?.rect.height ?? 0f;
        if (vh <= 0) return;
        float maxY = Mathf.Max(0, _contentHeight - vh);
        Vector2 p = content.anchoredPosition;
        p.y = Mathf.Clamp(p.y, 0, maxY);
        content.anchoredPosition = p;
    }

    private void ScrollToCurrentLevel()
    {
        if (_nodes.Count == 0 || scrollRect == null || content == null) return;
        RoadmapNodeView tgt = null;
        for (int i = 0; i < _nodes.Count; i++)
            if (_nodes[i].IsCurrent) { tgt = _nodes[i]; break; }
        if (tgt == null) return;

        // 节点在 LayoutRoot 中的 y + LayoutRoot 在 content 中的偏移 = 在 content 中的 y
        float nodeLocalY = tgt.GetComponent<RectTransform>().anchoredPosition.y;
        float rootOffsetY = _layoutRoot != null ? _layoutRoot.anchoredPosition.y : 0f;
        float posInContent = nodeLocalY + rootOffsetY;

        float vh = scrollRect.viewport?.rect.height ?? 800f;
        if (_contentHeight <= vh) return;

        // target = content.anchoredPosition.y 使得节点居视口中央
        // 节点在视口中的 y = (vh - _contentHeight + target) + posInContent = vh/2
        // → target = _contentHeight - vh/2 - posInContent
        float target = Mathf.Clamp(_contentHeight - vh * 0.5f - posInContent, 0, _contentHeight - vh);
        scrollRect.verticalNormalizedPosition = target / (_contentHeight - vh);
    }

    // ═══════════════ 点击 ═══════════════

    private void OnNodeClicked(RoadmapNodeView node)
    {
        if (node == null) return;
        UiClickSound.Play();
        if (node.IsCurrent)
        {
            UIManager.Instance?.ShowConfirm("进入关卡",
                $"是否进入「{ChapterLevelDisplay.FormatLevelName(node.LevelId)}」？",
                ok => { if (ok) { SelectedLevelContext.Set(node.ChapterId, node.LevelId); BattleFlowLauncher.TryStartBattleLoading(); } });
            return;
        }
        if (node.IsCleared) { ShowDetailPopup(node); return; }
        if (node.IsUnlocked) { SelectedLevelContext.Set(node.ChapterId, node.LevelId); BattleFlowLauncher.TryStartBattleLoading(); return; }
        UIManager.Instance?.ShowToast("通关前置关卡后可解锁", 1f);
    }

    // ═══════════════ 详情弹窗 ═══════════════

    private void ShowDetailPopup(RoadmapNodeView node)
    {
        // 先关闭旧弹窗
        CloseDetailPopup();

        PlayerProfileService.Instance.TryGetProgress(node.LevelId, out var prog);

        // 全屏透明遮罩（点击关闭）—— 存为 _activePopup，关闭时整体销毁（包括子物体）
        _activePopup = Mk("RoadmapDetailOverlay", transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        var overlayRt = _activePopup.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero; overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        overlayRt.SetAsLastSibling(); // 确保在 scroll rect 之上渲染
        _activePopup.AddComponent<Button>().onClick.AddListener(CloseDetailPopup);

        // ── 面板尺寸 ──
        float pnlW = popupPanelSize.x, pnlH = popupPanelSize.y;
        var pnl = Mk("Popup", _activePopup.transform, popupPanelSize, Vector2.zero, new Color(0.06f, 0.06f, 0.1f, 0.96f));
        var pnlRt = pnl.GetComponent<RectTransform>();

        // ── 计算定位：获取节点在 overlay 坐标系中的位置 ──
        var nodeRt = node.GetComponent<RectTransform>();
        Vector3 nodeScreenPos = nodeRt.position; // Screen Space Overlay 下 position 即为屏幕坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRt, nodeScreenPos, null, out Vector2 nodeLocal);

        // overlayH = overlay 全高；local 坐标原点在中心，yMin=-H/2, yMax=+H/2
        float overlayH = overlayRt.rect.height;
        float overlayW = overlayRt.rect.width;
        float halfPnlH = pnlH * 0.5f;
        float halfPnlW = pnlW * 0.5f;
        float gap = 40f; // 面板与节点的间距

        float spaceAbove = overlayH * 0.5f - nodeLocal.y; // 节点→overlay 顶部
        float spaceBelow = overlayH * 0.5f + nodeLocal.y; // 节点→overlay 底部

        bool placeAbove = spaceAbove >= pnlH + gap; // 上方空间够就放上面
        float panelY = placeAbove
            ? nodeLocal.y + halfPnlH + gap  // 面板在节点上方：面板底 = nodeLocal.y + gap
            : nodeLocal.y - halfPnlH - gap; // 面板在节点下方：面板顶 = nodeLocal.y - gap

        // 边界钳制（local 坐标系原点在中心）
        panelY = Mathf.Clamp(panelY, -overlayH * 0.5f + halfPnlH + 20f, overlayH * 0.5f - halfPnlH - 20f);

        // 横向：跟随节点 X，钳制不超出屏幕边界
        float panelX = Mathf.Clamp(nodeLocal.x, -overlayW * 0.5f + halfPnlW + 20f, overlayW * 0.5f - halfPnlW - 20f);

        pnlRt.anchoredPosition = new Vector2(panelX, panelY);

        // ── 内容 ──
        var inn = Mk("Inn", pnl.transform, Vector2.zero, Vector2.zero, Color.clear);
        var ir = inn.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.04f, 0.04f); ir.anchorMax = new Vector2(0.96f, 0.96f); ir.sizeDelta = Vector2.zero;

        Pop(ir, ChapterLevelDisplay.FormatLevelName(node.LevelId), popupTitleFontSize, Color.white, new Vector2(0, 90));
        int earned = prog != null ? Mathf.Clamp(prog.stars, 0, 3) : 0;
        int unearned = 3 - earned;
        string starStr = earned > 0
            ? new string('★', earned) + new string('☆', unearned)
            : new string('☆', 3);
        Pop(ir, starStr, popupStarFontSize, new Color(1, 0.82f, 0.15f), new Vector2(0, 35));
        string tm = prog?.bestTimeSec > 0
            ? $"最佳时间：{Mathf.FloorToInt(prog.bestTimeSec / 60):00}:{Mathf.FloorToInt(prog.bestTimeSec % 60):00}"
            : "最佳时间：--:--";
        string kl = prog != null ? $"最佳击杀：{prog.bestKills}" : "最佳击杀：--";
        Pop(ir, tm, popupDetailFontSize, new Color(0.65f, 0.65f, 0.7f), new Vector2(-90, -20));
        Pop(ir, kl, popupDetailFontSize, new Color(0.65f, 0.65f, 0.7f), new Vector2(90, -20));

        var btn = Mk("Btn", ir, popupButtonSize, new Vector2(0, -80), new Color(0.22f, 0.48f, 0.85f));
        btn.AddComponent<Button>();
        var bl = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI));
        bl.transform.SetParent(btn.transform, false);
        var br = bl.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.sizeDelta = Vector2.zero;
        var bt = bl.GetComponent<TextMeshProUGUI>();
        bt.text = "再次挑战"; bt.alignment = TextAlignmentOptions.Center; bt.fontSize = popupButtonFontSize; bt.color = Color.white;
        BattleChineseFontRuntime.EnsureLoaded(); BattleChineseFontRuntime.ApplyToTMP(bt);

        int cl = node.LevelId, cc = node.ChapterId;
        btn.GetComponent<Button>().onClick.AddListener(() =>
        {
            CloseDetailPopup();
            SelectedLevelContext.Set(cc, cl); BattleFlowLauncher.TryStartBattleLoading();
        });
    }

    private void CloseDetailPopup()
    {
        if (_activePopup != null) { Destroy(_activePopup); _activePopup = null; }
    }

    private static GameObject Mk(string n, Transform p, Vector2 sz, Vector2 pos, Color c)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(p, false);
        var r = g.GetComponent<RectTransform>(); r.sizeDelta = sz; r.anchoredPosition = pos;
        g.GetComponent<Image>().color = c; return g;
    }

    private static void Pop(RectTransform p, string t, float fs, Color c, Vector2 pos)
    {
        var g = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        g.transform.SetParent(p, false);
        g.GetComponent<RectTransform>().sizeDelta = new Vector2(280, 36);
        g.GetComponent<RectTransform>().anchoredPosition = pos;
        var tmp = g.GetComponent<TextMeshProUGUI>();
        tmp.text = t; tmp.fontSize = fs; tmp.alignment = TextAlignmentOptions.Center; tmp.color = c;
        BattleChineseFontRuntime.EnsureLoaded(); BattleChineseFontRuntime.ApplyToTMP(tmp);
    }

    // ═══════════════ 数据 ═══════════════

    private struct LevelData
    { public int levelId, chapterId, stars; public string mapName; public bool unlocked, cleared; }

    private struct ChapterMeta { public int chapterId; }

    private int FindChapterIndex(List<ChapterMeta> chapters, int levelId)
    {
        for (int ci = 0; ci < chapters.Count; ci++)
        {
            var levels = GetChapterLevels(chapters[ci].chapterId);
            foreach (var lv in levels)
                if (lv.levelId == levelId)
                    return ci;
        }
        return 0; // 兜底：第一章
    }

    private List<ChapterMeta> GetOrderedChapters()
    {
        var set = new Dictionary<int, ChapterMeta>();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        if (dict != null)
            foreach (var kv in dict)
                if (kv.Value is ProtoTable.ChapterLevel cl && cl.chapterId > 0 && !set.ContainsKey(cl.chapterId))
                    set[cl.chapterId] = new ChapterMeta { chapterId = cl.chapterId };
#endif
        var list = new List<ChapterMeta>(set.Values); list.Sort((a, b) => a.chapterId.CompareTo(b.chapterId));
        return list;
    }

    private List<LevelData> GetChapterLevels(int chId)
    {
        var list = new List<LevelData>();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        if (dict == null) return list;
        foreach (var kv in dict)
        {
            if (kv.Value is ProtoTable.ChapterLevel cl && cl.chapterId == chId)
            {
                bool u = PlayerProfileService.Instance.IsLevelUnlocked(cl.levelId);
                bool c = PlayerProfileService.Instance.HasCleared(cl.levelId);
                int s = 0;
                if (c && PlayerProfileService.Instance.TryGetProgress(cl.levelId, out var p)) s = Mathf.Clamp(p.stars, 1, 3);
                list.Add(new LevelData { levelId = cl.levelId, chapterId = cl.chapterId, mapName = cl.mapName ?? "", unlocked = u, cleared = c, stars = s });
            }
        }
#endif
        list.Sort((a, b) => a.levelId.CompareTo(b.levelId)); return list;
    }

    private static int ResolveCurrentLevelId()
    {
        var all = new List<int>();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        if (dict != null) foreach (var kv in dict) if (kv.Value is ProtoTable.ChapterLevel cl) all.Add(cl.levelId);
#endif
        all.Sort();
        foreach (int id in all) if (PlayerProfileService.Instance.IsLevelUnlocked(id) && !PlayerProfileService.Instance.HasCleared(id)) return id;
        return all.Count > 0 ? all[^1] : 101;
    }

    // ═══════════════ 清理 ═══════════════

    private void ClearAll()
    {
        if (content != null)
            for (int i = content.childCount - 1; i >= 0; i--)
                DestroyImmediate(content.GetChild(i).gameObject);
        _layoutRoot = null;
        _nodes.Clear();
        CloseDetailPopup();
    }

    // ═══════════════ 圆点 ═══════════════

    private static Sprite GenerateWhitePixelSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var c = new Color32(255, 255, 255, 255);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = c;
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f, r2 = r * r, aa = 1.5f, inner = r - aa;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f, d2 = dx * dx + dy * dy;
                float a = d2 <= inner * inner ? 1f : d2 >= r2 ? 0f : 1f - (Mathf.Sqrt(d2) - inner) / aa;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
