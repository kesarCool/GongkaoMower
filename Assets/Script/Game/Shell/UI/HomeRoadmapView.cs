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
    [SerializeField] private float chapterGap = 60f; // 章末节点到分隔线的额外间距
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
    [SerializeField] private Color dividerColor = new Color(0.35f, 0.35f, 0.4f, 1f);

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

    // ═══════════════ 生命周期 ═══════════════

    private IEnumerator Start()
    {
        if (_circleSprite == null) _circleSprite = GenerateCircleSprite(circleTexSize);
        if (_whitePixelSprite == null) _whitePixelSprite = GenerateWhitePixelSprite();
        PlayerProfileService.Instance.LoadOrCreate();
        TableManager.Instance.Init();
        ResolveCurrentHeroPortrait();
        if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        yield return null;
        yield return null;

        // 强制设为 ScrollRect 标准垂直布局: content 顶部对齐视口顶部
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        BuildAll(); // 内部已调用 ScrollToCurrentLevel
        _startDone = true;
    }

    private void OnEnable() { if (_startDone && _built) RefreshAll(); }
    private void OnDestroy()
    {
        if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    public void RefreshAll()
    {
        PlayerProfileService.Instance.LoadOrCreate();
        ResolveCurrentHeroPortrait();
        ClearAll();
        _built = false;
        BuildAll(); // 内部已同步调用 ScrollToCurrentLevel
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
        if (_built || content == null || nodePrefab == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            DestroyImmediate(content.GetChild(i).gameObject);
        _nodes.Clear();

        var allChapters = GetOrderedChapters();
        if (allChapters.Count == 0) return;
        _currentLevelId = ResolveCurrentLevelId();

        int currentChapterIdx = FindChapterIndex(allChapters, _currentLevelId);
        int maxShowChapterIdx = Mathf.Min(currentChapterIdx + 1, allChapters.Count - 1);
        if (currentChapterIdx < 0) currentChapterIdx = 0;

        var allLevels = new List<(LevelData d, int chIdx, string chapterName)>();
        for (int ci = 0; ci <= maxShowChapterIdx; ci++)
        {
            var levels = GetChapterLevels(allChapters[ci].chapterId);
            if (levels.Count == 0) continue;
            if (ci <= currentChapterIdx && !levels[0].unlocked) break;
            string chName = $"第{allChapters[ci].chapterId}章";
            foreach (var lv in levels)
                allLevels.Add((lv, ci, chName));
        }
        if (allLevels.Count == 0) return;

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

        float rawContentHeight = paddingBottom + paddingTop + totalRows * rowHeight + dividerCount * (dividerHeight + chapterGap);
        _contentHeight = rawContentHeight + vh;
        float nodeAreaW = sideOffset * 2f + 200f;
        float cw = Mathf.Max(nodeAreaW, vw);
        content.sizeDelta = new Vector2(cw, _contentHeight);

        // LayoutRoot: 锚在 content 左下角，高=rawContentHeight，偏移=vh/2 在 content 内居中
        var lrGo = new GameObject("LayoutRoot", typeof(RectTransform));
        lrGo.transform.SetParent(content, false);
        _layoutRoot = lrGo.GetComponent<RectTransform>();
        _layoutRoot.anchorMin = _layoutRoot.anchorMax = new Vector2(0, 0);
        _layoutRoot.pivot = new Vector2(0, 0);
        _layoutRoot.sizeDelta = new Vector2(cw, rawContentHeight);

        float contentCenterX = cw * 0.5f;

        // ── 从下往上：第一章在底部（小 y），第三章在顶部（大 y）──
        float yCursor = paddingBottom;
        lastCh = -1;
        int globalRow = 0;

        for (int i = 0; i < allLevels.Count; i++)
        {
            var (d, chIdx, chName) = allLevels[i];

            if (chIdx != lastCh)
            {
                yCursor += chapterGap; // 章末节点上方留空，分隔线往上提
                MakeDivider(yCursor, contentCenterX, cw, chName);
                yCursor += dividerHeight;
            }
            lastCh = chIdx;

            float ny = yCursor + rowHeight * 0.5f;
            bool onLeft = (globalRow % 2 == 0);
            float nx = onLeft ? contentCenterX - sideOffset : contentCenterX + sideOffset;

            var node = Instantiate(nodePrefab, _layoutRoot, false);
            var rt = node.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(nx, ny);
            // ny = 距 layoutRoot 底部的距离（第一章底部、第三章顶部）
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

        float offsetX = Mathf.Max(0, (vw - nodeAreaW) * 0.5f);
        float offsetY = Mathf.Max(0, (_contentHeight - rawContentHeight) * 0.5f);
        _layoutRoot.anchoredPosition = new Vector2(offsetX, offsetY);

        _built = true;

        // ── 立刻定位到当前关卡（不等待下一帧，避免 ScrollRect 重置）──
        Canvas.ForceUpdateCanvases();
        ScrollToCurrentLevel();

        float firstY = _nodes.Count > 0 ? _nodes[0].GetComponent<RectTransform>().anchoredPosition.y : -1;
        float lastY = _nodes.Count > 0 ? _nodes[_nodes.Count - 1].GetComponent<RectTransform>().anchoredPosition.y : -1;
        // Debug.Log($"[Roadmap] 构建完成: {allLevels.Count} 节点, contentH={_contentHeight:F0}, rawH={rawContentHeight:F0}, " +
        //           $"firstNodeY={firstY:F0}, lastNodeY={lastY:F0}, offsetY={offsetY:F0}, vh={vh:F0}");
    }

    private void SetNodeLines()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            // 仅最后一章的最后一关不往上延伸
            if (i == _nodes.Count - 1) { _nodes[i].HideLines(); continue; }
            bool lineActive = _nodes[i + 1].IsUnlocked;
            Color color = lineActive ? lineActiveColor : lineDimColor;
            bool nextOnRight = (i % 2 == 0);
            _nodes[i].SetLineState(leftActive: !nextOnRight, rightActive: nextOnRight, color, color, _whitePixelSprite);
        }
    }

    private void MakeDivider(float yBase, float cx, float cw, string chapterName)
    {
        float y = yBase + dividerHeight * 0.5f;
        // 横线 — 锚在底部，y = 距 layoutRoot 底部
        var go = new GameObject("Div", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_layoutRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(cx, y);
        rt.sizeDelta = new Vector2(cw * 0.7f, 2f);
        go.GetComponent<Image>().color = dividerColor;
        go.GetComponent<Image>().raycastTarget = false;

        // 章节名 — 锚在底部
        var lblGo = new GameObject("ChName", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(_layoutRoot, false);
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
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
        // 仅打日志，不干预 ScrollRect
        // Debug.Log($"[Roadmap] scroll normalizedPos={scrollRect.verticalNormalizedPosition:F4}, content.anchoredPos.y={content.anchoredPosition.y:F1}");
    }

    private void ScrollToCurrentLevel()
    {
        if (_nodes.Count == 0 || scrollRect == null || content == null) return;

        RoadmapNodeView tgt = null;
        for (int i = 0; i < _nodes.Count; i++)
            if (_nodes[i].IsCurrent) { tgt = _nodes[i]; break; }
        if (tgt == null) return;

        float vh = scrollRect.viewport?.rect.height ?? 800f;
        if (_contentHeight <= vh) return;
        float scrollable = _contentHeight - vh;
        if (scrollable <= 0f) return;

        // 节点 anchor=(0.5,0) → anchoredPosition.y 直接 = 距 layoutRoot 底部距离
        float nodeLocalY = tgt.GetComponent<RectTransform>().anchoredPosition.y;
        float lrBottomInContent = vh * 0.5f; // layoutRoot 底部在 content 内的 y
        float posFromBottom = lrBottomInContent + nodeLocalY;

        // content 锚在顶部(0,1), verticalNormalizedPosition: 1=顶部, 0=底部
        float target = Mathf.Clamp(posFromBottom - vh * 0.5f, 0f, scrollable);
        float vnp = target / scrollable;
        scrollRect.verticalNormalizedPosition = vnp;

        // Debug.Log($"[Roadmap] ScrollTo: lv={tgt.LevelId}, nodeLocalY={nodeLocalY:F0}, lrBottom={lrBottomInContent:F0}, posFromBottom={posFromBottom:F0}, target={target:F0}, vnp={vnp:F4}");
    }

    // ═══════════════ 点击 / 弹窗 / 数据 =（不变）═══════════════

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
