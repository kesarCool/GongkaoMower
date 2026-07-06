using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HomeRoadmapView : MonoBehaviour
{
    [Header("滚动")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    [Header("节点")]
    [SerializeField] private RoadmapNodeView nodePrefab;

    [Header("布局")]
    [SerializeField] private float nodeSize = 130f;
    [SerializeField] private float rowHeight = 220f;
    [SerializeField] private float sideOffset = 300f;
    [SerializeField] private float dividerHeight = 50f;
    [SerializeField] private float chapterGap = 60f;
    [SerializeField] private float paddingBottom = 300f;
    [SerializeField] private float paddingTop = 300f;

    [Header("节点外观")]
    [SerializeField] private Sprite nodeCircleSprite;
    [SerializeField] private int circleTexSize = 128;

    [Header("当前英雄")]
    [SerializeField] private CharacterCatalog characterCatalog;
    [SerializeField] private Sprite currentHeroSprite;

    [Header("颜色")]
    [SerializeField] private Color lineActiveColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    [SerializeField] private Color lineDimColor = new Color(0.2f, 0.2f, 0.22f, 1f);

    [Header("详情弹窗")]
    [SerializeField] private Vector2 popupPanelSize = new Vector2(460f, 300f);
    [SerializeField] private Vector2 popupButtonSize = new Vector2(210f, 54f);
    [SerializeField] private float popupTitleFontSize = 34f;
    [SerializeField] private float popupStarFontSize = 40f;
    [SerializeField] private float popupDetailFontSize = 24f;
    [SerializeField] private float popupButtonFontSize = 26f;

    private Sprite _circleSprite;
    private Sprite _whitePixelSprite;
    private float _contentHeight;
    private int _currentLevelId;
    private bool _built;
    private bool _startDone;
    private RectTransform _layoutRoot;
    private GameObject _activePopup;
    private readonly List<RoadmapNodeView> _nodes = new List<RoadmapNodeView>();

    private static void L(string msg) { }

    // ═══════════════ 生命周期 ═══════════════

    private IEnumerator Start()
    {
        L($"[R] Start BEGIN f={Time.frameCount}");
        if (_circleSprite == null) _circleSprite = GenerateCircleSprite(circleTexSize);
        if (_whitePixelSprite == null) _whitePixelSprite = GenerateWhitePixelSprite();
        PlayerProfileService.Instance.LoadOrCreate();
        TableManager.Instance.Init();
        ResolveCurrentHeroPortrait();
        // if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollValueChanged);  // [DEBUG]

        yield return null;
        yield return null;
        L($"[R] Start after yields f={Time.frameCount}");

        content.anchorMin = new Vector2(0, 0);
        content.anchorMax = new Vector2(1, 0);
        content.pivot = new Vector2(0f, 0f);

        if (scrollRect != null) scrollRect.horizontal = false;

        BuildAll();
        L($"[R] Start END _sd=true _b={_built} _n={_nodes.Count}");
        _startDone = true;
    }

    private void OnEnable()
    {
        L($"[R] OnEnable f={Time.frameCount} _sd={_startDone} _b={_built}");
        if (_startDone)
        {
            RefreshAll();
        }
    }

    private void OnDestroy()
    {
        if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    public void RefreshAll()
    {
        if (!_startDone) { L($"[R] RefreshAll SKIP _sd=false"); return; }
        var stack = new System.Diagnostics.StackTrace(1, true);
        L($"[R] RefreshAll BEGIN f={Time.frameCount} _sd={_startDone} _b={_built} caller={stack.GetFrame(0)?.GetMethod()?.Name}");
        PlayerProfileService.Instance.LoadOrCreate();
        ResolveCurrentHeroPortrait();
        ClearAll();
        _built = false;
        BuildAll();
        L($"[R] RefreshAll END _b={_built} _n={_nodes.Count}");
    }

    private void ResolveCurrentHeroPortrait()
    {
        var cat = characterCatalog;
        if (cat == null) cat = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
        string charId = PlayerProfileService.Instance.EquippedCharacterId;
        var def = cat?.Get(charId);
        currentHeroSprite = def != null ? def.portrait : null;
    }

    public void SetHeroSprite(Sprite s) { currentHeroSprite = s; RefreshAll(); }

    // ═══════════════ 构建（从下往上：第一章在底，末章在顶） ═══════════════

    private void BuildAll()
    {
        L($"[R] BuildAll BEGIN f={Time.frameCount} _b={_built}");
        if (_built || content == null || nodePrefab == null) { L($"[R] BuildAll SKIP"); return; }

        for (int i = content.childCount - 1; i >= 0; i--)
            DestroyImmediate(content.GetChild(i).gameObject);
        _nodes.Clear();

        var allChapters = GetOrderedChapters();
        if (allChapters.Count == 0) { L($"[R] BuildAll no chapters"); return; }
        _currentLevelId = ResolveCurrentLevelId();
        L($"[R] BuildAll currentLevelId={_currentLevelId}");

        int currentChapterIdx = FindChapterIndex(allChapters, _currentLevelId);
        int maxShowChapterIdx = Mathf.Min(currentChapterIdx + 1, allChapters.Count - 1);
        if (currentChapterIdx < 0) currentChapterIdx = 0;

        var allLevels = new List<(LevelData d, int chIdx, string chapterName)>();
        for (int ci = 0; ci <= maxShowChapterIdx; ci++)
        {
            var levels = GetChapterLevels(allChapters[ci].chapterId);
            if (levels.Count == 0) continue;
            if (ci <= currentChapterIdx && !levels[0].unlocked) break;
            string chName = FormatChapterName(allChapters[ci].chapterId);
            foreach (var lv in levels)
                allLevels.Add((lv, ci, chName));
        }
        if (allLevels.Count == 0) { L($"[R] BuildAll no levels"); return; }

        int totalRows = allLevels.Count;
        int dividerCount = 0;
        int lastCh = -1;
        for (int i = 0; i < allLevels.Count; i++)
        {
            if (allLevels[i].chIdx != lastCh) dividerCount++;
            lastCh = allLevels[i].chIdx;
        }

        float vh = scrollRect?.viewport?.rect.height ?? 1920f;
        float vw = scrollRect?.viewport?.rect.width ?? 1080f;
        L($"[R] BuildAll vh={vh:F0} vw={vw:F0}");

        float rawContentHeight = paddingBottom + paddingTop + totalRows * rowHeight + dividerCount * (dividerHeight + chapterGap);
        _contentHeight = rawContentHeight + vh;
        // Content 拉伸到 viewport 宽度 (sizeDelta.x=0)，不再手工算 cw
        content.sizeDelta = new Vector2(0, _contentHeight);

        // Content 上的 VerticalLayoutGroup 会把 _layoutRoot 的 anchoredPosition 强制改掉，
        // 底锚时尤为致命（930 变成 -2760），必须关闭手动控制位置。
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) { vlg.enabled = false; L($"[R] BuildAll disabled VerticalLayoutGroup on content"); }

        var lrGo = new GameObject("LayoutRoot", typeof(RectTransform));
        lrGo.transform.SetParent(content, false);
        _layoutRoot = lrGo.GetComponent<RectTransform>();
        // 水平拉伸填满 content，垂直固定在底部
        _layoutRoot.anchorMin = new Vector2(0, 0);
        _layoutRoot.anchorMax = new Vector2(1, 0);
        _layoutRoot.pivot = new Vector2(0, 0);
        _layoutRoot.sizeDelta = new Vector2(0, rawContentHeight);

        float contentCenterX = vw * 0.5f;
        float yCursor = paddingBottom;
        lastCh = -1;
        int globalRow = 0;

        for (int i = 0; i < allLevels.Count; i++)
        {
            var (d, chIdx, chName) = allLevels[i];

            if (chIdx != lastCh)
            {
                yCursor += chapterGap;
                MakeDivider(yCursor, contentCenterX, vw, chName, d.unlocked);
                yCursor += dividerHeight;
            }
            lastCh = chIdx;

            float ny = yCursor + rowHeight * 0.5f;
            bool onLeft = (globalRow % 2 == 0);
            float nx = onLeft ? contentCenterX - sideOffset : contentCenterX + sideOffset;

            var node = Instantiate(nodePrefab, _layoutRoot, false);
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(nx, ny);
            rt.sizeDelta = new Vector2(180, 90);

            Sprite circle = nodeCircleSprite ? nodeCircleSprite : _circleSprite;
            node.SetCircleSprite(circle);
            node.Bind(d.levelId, d.chapterId, d.unlocked, d.cleared, d.stars,
                d.mapName, d.levelId == _currentLevelId,
                d.levelId == _currentLevelId ? currentHeroSprite : null,
                OnNodeClicked);
            _nodes.Add(node);

            yCursor += rowHeight;
            globalRow++;
        }

        SetNodeLines();

        // _layoutRoot 已拉伸到 content 宽度，x 偏移为 0
        float offsetY = Mathf.Max(0, (_contentHeight - rawContentHeight) * 0.5f);
        _layoutRoot.anchoredPosition = new Vector2(0, offsetY);

        _built = true;

        Canvas.ForceUpdateCanvases();
        ScrollToCurrentLevel();

        var n0 = _nodes[0].GetComponent<RectTransform>();
        var nN = _nodes[_nodes.Count - 1].GetComponent<RectTransform>();
        L($"[R] BuildAll END _n={_nodes.Count} contentH={_contentHeight:F0} rawH={rawContentHeight:F0} offsetY={offsetY:F0} vh={vh:F0} vw={vw:F0} contentW={content.rect.width:F0}");
        L($"[R] BuildAll NODE0 x={n0.anchoredPosition.x:F0} y={n0.anchoredPosition.y:F0} NODE_N x={nN.anchoredPosition.x:F0} y={nN.anchoredPosition.y:F0}");
    }

    private void SetNodeLines()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (i == _nodes.Count - 1) { _nodes[i].HideLines(); continue; }
            bool lineActive = _nodes[i + 1].IsUnlocked;
            Color color = lineActive ? lineActiveColor : lineDimColor;
            bool nextOnRight = (i % 2 == 0);
            _nodes[i].SetLineState(leftActive: !nextOnRight, rightActive: nextOnRight, color, color, _whitePixelSprite);
        }
    }

    private void MakeDivider(float yBase, float cx, float cw, string chapterName, bool chapterUnlocked)
    {
        Color color = chapterUnlocked ? lineActiveColor : lineDimColor;
        float y = yBase + dividerHeight * 0.5f;
        var go = new GameObject("Div", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_layoutRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(cx, y);
        rt.sizeDelta = new Vector2(cw * 0.7f, 2f);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;

        var lblGo = new GameObject("ChName", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(_layoutRoot, false);
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(cx, y + 24f);
        lrt.sizeDelta = new Vector2(cw * 0.7f, 50f);
        var tmp = lblGo.GetComponent<TextMeshProUGUI>();
        tmp.text = chapterName;
        tmp.fontSize = 42;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = color;
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToTMP(tmp);
    }

    // ═══════════════ 滚动 ═══════════════

    private void OnScrollValueChanged(Vector2 _)
    {
        L($"[R] OnScrollChanged vnp={scrollRect.verticalNormalizedPosition:F4} aY={content.anchoredPosition.y:F1}");
    }

    /// <summary>滚动到当前关卡节点，使其居中在视口。</summary>
    private void ScrollToCurrentLevel()
    {
        if (_nodes.Count == 0) { L("[R] ScrollTo SKIP nodes=0"); return; }
        if (scrollRect == null) { L("[R] ScrollTo SKIP sr=null"); return; }
        if (content == null) { L("[R] ScrollTo SKIP ct=null"); return; }

        RoadmapNodeView tgt = null;
        for (int i = 0; i < _nodes.Count; i++)
            if (_nodes[i].IsCurrent) { tgt = _nodes[i]; break; }
        if (tgt == null) { L($"[R] ScrollTo SKIP tgt=null curId={_currentLevelId}"); return; }

        float vh = scrollRect.viewport?.rect.height ?? 800f;
        float contentH = content.rect.height;
        float scrollable = contentH - vh;
        L($"[R] ScrollTo IN lv={tgt.LevelId} vh={vh:F0} contentH={contentH:F0} scrollable={scrollable:F0} anchor=bottom");
        if (scrollable <= 0f) { L("[R] ScrollTo SKIP scrollable<=0"); return; }

        // Content 锚定底部 (0,0), pivot (0.5,0)。
        // _layoutRoot 在 content 底部上方 offsetY = vh/2 处。
        // 节点距 content 底部 = lrOffsetY + nodeLocalY。
        // content 底部位置使节点居中视口：anchoredY = vh/2 - nodeFromContentBottom。
        float lrOffsetY = _layoutRoot != null ? _layoutRoot.anchoredPosition.y : vh * 0.5f;
        float nodeLocalY = tgt.GetComponent<RectTransform>().anchoredPosition.y;
        float nodeFromContentBottom = lrOffsetY + nodeLocalY;
        float anchoredY = vh * 0.5f - nodeFromContentBottom;
        anchoredY = Mathf.Clamp(anchoredY, -scrollable, 0f);

        L($"[R] ScrollTo SET lrOffsetY={lrOffsetY:F0} nodeLocalY={nodeLocalY:F0} nodeFromBottom={nodeFromContentBottom:F0} anchoredY={anchoredY:F0}");
        content.anchoredPosition = new Vector2(0, anchoredY);
        L($"[R] ScrollTo AFTER aY={content.anchoredPosition.y:F1} vnpNow={scrollRect.verticalNormalizedPosition:F4}");
    }

    // ═══════════════ 点击 / 弹窗 / 数据 ═══════════════

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

    private void ShowDetailPopup(RoadmapNodeView node)
    {
        CloseDetailPopup();
        PlayerProfileService.Instance.TryGetProgress(node.LevelId, out var prog);

        _activePopup = Mk("RoadmapDetailOverlay", transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        var overlayRt = _activePopup.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero; overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;
        overlayRt.SetAsLastSibling();
        _activePopup.AddComponent<Button>().onClick.AddListener(CloseDetailPopup);

        float pnlW = popupPanelSize.x, pnlH = popupPanelSize.y;
        var pnl = Mk("Popup", _activePopup.transform, popupPanelSize, Vector2.zero, new Color(0.06f, 0.06f, 0.1f, 0.96f));
        var pnlRt = pnl.GetComponent<RectTransform>();

        var nodeRt = node.GetComponent<RectTransform>();
        Vector3 nodeScreenPos = nodeRt.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRt, nodeScreenPos, null, out Vector2 nodeLocal);

        float overlayH = overlayRt.rect.height, overlayW = overlayRt.rect.width;
        float halfPnlH = pnlH * 0.5f, halfPnlW = pnlW * 0.5f, gap = 40f;
        float spaceAbove = overlayH * 0.5f - nodeLocal.y;
        float spaceBelow = overlayH * 0.5f + nodeLocal.y;
        bool placeAbove = spaceAbove >= pnlH + gap;
        float panelY = placeAbove ? nodeLocal.y + halfPnlH + gap : nodeLocal.y - halfPnlH - gap;
        panelY = Mathf.Clamp(panelY, -overlayH * 0.5f + halfPnlH + 20f, overlayH * 0.5f - halfPnlH - 20f);
        float panelX = Mathf.Clamp(nodeLocal.x, -overlayW * 0.5f + halfPnlW + 20f, overlayW * 0.5f - halfPnlW - 20f);
        pnlRt.anchoredPosition = new Vector2(panelX, panelY);

        var inn = Mk("Inn", pnl.transform, Vector2.zero, Vector2.zero, Color.clear);
        var ir = inn.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.04f, 0.04f); ir.anchorMax = new Vector2(0.96f, 0.96f); ir.sizeDelta = Vector2.zero;

        Pop(ir, ChapterLevelDisplay.FormatLevelName(node.LevelId), popupTitleFontSize, Color.white, new Vector2(0, 90));
        int earned = prog != null ? Mathf.Clamp(prog.stars, 0, 3) : 0;
        string starStr = earned > 0 ? new string('★', earned) + new string('☆', 3 - earned) : new string('☆', 3);
        Pop(ir, starStr, popupStarFontSize, new Color(1, 0.82f, 0.15f), new Vector2(0, 35));
        string tm = prog?.bestTimeSec > 0 ? $"最佳时间：{Mathf.FloorToInt(prog.bestTimeSec / 60):00}:{Mathf.FloorToInt(prog.bestTimeSec % 60):00}" : "最佳时间：--:--";
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
        btn.GetComponent<Button>().onClick.AddListener(() => { CloseDetailPopup(); SelectedLevelContext.Set(cc, cl); BattleFlowLauncher.TryStartBattleLoading(); });
    }

    private void CloseDetailPopup() { if (_activePopup != null) { Destroy(_activePopup); _activePopup = null; } }

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

    private struct LevelData { public int levelId, chapterId, stars; public string mapName; public bool unlocked, cleared; }
    private struct ChapterMeta { public int chapterId; }

    private int FindChapterIndex(List<ChapterMeta> chapters, int levelId)
    {
        for (int ci = 0; ci < chapters.Count; ci++)
        {
            var levels = GetChapterLevels(chapters[ci].chapterId);
            foreach (var lv in levels) if (lv.levelId == levelId) return ci;
        }
        return 0;
    }

    private List<ChapterMeta> GetOrderedChapters()
    {
        var set = new Dictionary<int, ChapterMeta>();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
        L($"[R] GetChapters FB=1 dictNull={dict == null} dictCount={dict?.Count}");
        if (dict != null)
            foreach (var kv in dict)
                if (kv.Value is ProtoTable.ChapterLevel cl && cl.chapterId > 0 && !set.ContainsKey(cl.chapterId))
                    set[cl.chapterId] = new ChapterMeta { chapterId = cl.chapterId };
#else
        L($"[R] GetChapters FB=0 (USE_FB_TABLE not defined)");
#endif
        var list = new List<ChapterMeta>(set.Values); list.Sort((a, b) => a.chapterId.CompareTo(b.chapterId));
        L($"[R] GetChapters result={list.Count}");
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

    /// <summary>
    /// 章节显示名：第X章（主题名）。主题未知时仅返回"第X章"。
    /// </summary>
    private static string FormatChapterName(int chapterId)
    {
        string theme = LexiconPreviewCatalog.GetThemeTabLabel(chapterId);
        if (string.IsNullOrEmpty(theme) || theme.StartsWith("主题 "))
            return $"第{chapterId}章";
        return $"第{chapterId}章（{theme}）";
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

    // ═══════════════ 贴图 ═══════════════

    private static Sprite GenerateWhitePixelSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var c = new Color32(255, 255, 255, 255);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = c;
        tex.SetPixels32(px); tex.Apply();
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
