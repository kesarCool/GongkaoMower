using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 肉鸽选卡系统：处理抽卡、UI显示、防卡死、刷新逻辑
/// </summary>
public class CardSelectionSystem : MonoBehaviour
{
    [Header("数据配置")]
    [Tooltip("卡组配置")]
    public CardDeck deck;

    [Tooltip("技能目录（用于读取技能详情显示）")]
    public SkillCatalog skillCatalog;

    [Tooltip("当前关卡进度（由外部更新）")]
    public int currentLevel = 1;

    [Header("UI引用")]
    [Tooltip("选卡面板（含3个卡槽位）")]
    public CardSelectionPanel panel;

    [Header("刷新配置")]
    [Tooltip("每次选卡允许免费刷新次数")]
    public int freeRefreshCount = 1;

    [Tooltip("是否有消耗货币刷新（预留后期）")]
    public bool allowPaidRefresh = false;

    // 状态
    private bool _isSelecting = false;
    private Queue<CardSelectionRequest> _pendingRequests = new Queue<CardSelectionRequest>();
    private List<CardDeck.DrawResult> _currentCards;
    private List<SkillId> _excludedSkills = new List<SkillId>();
    private int _remainingRefresh;

    private PlayerSkills _playerSkills;

    /// <summary>若 Inspector 指向 Prefab 资源（非场景实例），运行时 <see cref="EnsurePanelInActiveSceneAndUnderCanvas"/> 会 <see cref="Object.Instantiate"/> 一次并复用此引用，禁止对资源 Transform 作 SetParent/Move。</summary>
    private CardSelectionPanel _panelRuntime;

    /// <summary>本次选卡是否由 <see cref="UIManager"/> 打开（影响暂停与关闭方式）。</summary>
    private bool _selectionUsesUIManager;

    private void Awake()
    {
        _playerSkills = GetComponent<PlayerSkills>();
        if (_playerSkills == null)
            _playerSkills = FindObjectOfType<PlayerSkills>();
    }

    /// <summary>
    /// 外部调用：设置当前关卡（由SpawnerWaves或GameLayer更新）
    /// </summary>
    public void SetCurrentLevel(int level)
    {
        currentLevel = level;
        if (RoguelikeCardManager.Instance != null)
            RoguelikeCardManager.Instance.CurrentLevel = level;
    }

    /// <summary>
    /// 由 <see cref="RoguelikeCardManager"/> 在能量事件时调用
    /// </summary>
    public void BeginSelectionFromManager()
    {
        if (_playerSkills == null)
        {
            Debug.LogWarning("[CardSelectionSystem] PlayerSkills not found");
            return;
        }

        if (_playerSkills.AllSlotsFullAndMaxLevel)
        {
            Debug.Log("[CardSelectionSystem] All skills maxed, skipping");
            return;
        }

        if (_isSelecting)
        {
            _pendingRequests.Enqueue(new CardSelectionRequest());
            Debug.Log($"[CardSelectionSystem] Queued request, pending={_pendingRequests.Count}");
            return;
        }

        StartSelection();
    }

    private void StartSelection()
    {
        _selectionUsesUIManager = false;
        _isSelecting = true;
        _remainingRefresh = freeRefreshCount;
        _excludedSkills.Clear();

        // 抽卡 + 显示 UI；UIManager 路径下暂停由框架管理，异常时 EndSelection 会收尾
        try
        {
            DrawCards();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[CardSelectionSystem] Draw/Show failed: " + ex);
            EndSelection();
        }
    }

    private void DrawCards()
    {
        if (RoguelikeCardManager.Instance != null)
        {
            RoguelikeCardManager.Instance.CurrentLevel = currentLevel;
            _currentCards = RoguelikeCardManager.Instance.DrawFromPool(currentLevel, _playerSkills, _excludedSkills);
        }
        else if (deck != null)
        {
            _currentCards = deck.Draw(currentLevel, _playerSkills, _excludedSkills);
        }
        else
        {
            _currentCards = null;
        }

        if (_currentCards == null || _currentCards.Count == 0)
        {
            Debug.LogWarning("[CardSelectionSystem] No cards available");
            EndSelection();
            return;
        }

        if (UIManager.Instance != null)
        {
            var payload = new CardSelectionOpenPayload
            {
                Cards = _currentCards,
                OnCardSelected = OnCardSelected,
                OnRefreshRequested = OnRefreshRequested,
                RemainingRefresh = _remainingRefresh
            };
            var opts = new UiOpenOptions
            {
                PauseTime = true,
                UseUnscaledTime = true,
                CloseOnBack = false
            };
            var opened = UIManager.Instance.Open<CardSelectionPanel>(payload, opts);
            if (opened != null)
            {
                _selectionUsesUIManager = true;
                return;
            }
            Debug.LogWarning("[CardSelectionSystem] UIManager 未能打开 CardSelectionPanel，请在 UIManager.panelPrefabs 中注册选卡 Prefab。将回退到 CardSelectionSystem.panel。");
        }

        _selectionUsesUIManager = false;
        if (panel != null)
        {
            EnsurePanelInActiveSceneAndUnderCanvas();
            EnsureTransformHierarchyActive(panel.transform);
            Time.timeScale = 0f;
            var showOk = panel.Show(_currentCards, OnCardSelected, OnRefreshRequested, _remainingRefresh);
            if (!showOk)
                EndSelection();
        }
        else
        {
            ApplyCard(_currentCards[0]);
            EndSelection();
        }
    }

    /// <summary>
    /// 玩家点击刷新
    /// </summary>
    private void OnRefreshRequested()
    {
        if (_remainingRefresh <= 0 && !allowPaidRefresh)
        {
            Debug.Log("[CardSelectionSystem] No refresh remaining");
            return;
        }

        _remainingRefresh--;

        // 将当前3张卡加入排除列表
        foreach (var c in _currentCards)
            _excludedSkills.Add(c.skillId);

        // 重新抽卡
        DrawCards();

        if (_selectionUsesUIManager && UIManager.Instance != null &&
            UIManager.Instance.TryGetInstance(out CardSelectionPanel csp))
            csp.UpdateRefreshCount(_remainingRefresh);
        else
            panel?.UpdateRefreshCount(_remainingRefresh);
    }

    /// <summary>
    /// 玩家选择了某张卡
    /// </summary>
    private void OnCardSelected(int index)
    {
        if (index < 0 || index >= _currentCards.Count) return;

        var selected = _currentCards[index];
        ApplyCard(selected);
        EndSelection();
    }

    /// <summary>
    /// 应用卡牌效果
    /// </summary>
    private void ApplyCard(CardDeck.DrawResult card)
    {
        if (card.currentLevel == 0)
        {
            // 新技能
            bool added = _playerSkills.TryAddSkill(card.skillId);
            Debug.Log($"[CardSelectionSystem] New skill {card.skillId}, added={added}");
        }
        else
        {
            // 升级
            bool leveled = _playerSkills.TryLevelUp(card.skillId);
            Debug.Log($"[CardSelectionSystem] Upgrade {card.skillId} to Lv.{card.targetLevel}, success={leveled}");
        }
    }

    /// <summary>
    /// 结束选卡，恢复游戏
    /// </summary>
    private void EndSelection()
    {
        _isSelecting = false;
        try
        {
            if (_selectionUsesUIManager && UIManager.Instance != null)
            {
                if (UIManager.Instance.Top is CardSelectionPanel)
                    UIManager.Instance.CloseTop();
            }
            else
                panel?.Hide();
        }
        finally
        {
            if (!_selectionUsesUIManager)
                Time.timeScale = 1f;
        }

        // 检查队列中是否有待处理请求
        if (_pendingRequests.Count > 0)
        {
            _pendingRequests.Dequeue();
            StartCoroutine(DelayedNextSelection());
        }
    }

    private IEnumerator DelayedNextSelection()
    {
        // 延迟0.3秒让玩家喘息，也避免连续timeScale切换造成卡顿
        yield return new WaitForSecondsRealtime(0.3f);

        // 再次检查是否还需要选卡（可能刚才升级后已满级）
        if (!_playerSkills.AllSlotsFullAndMaxLevel)
        {
            StartSelection();
        }
    }

    private struct CardSelectionRequest { }

    private static bool InLoadedPlayScene(GameObject g) =>
        g != null && g.scene.IsValid() && g.scene.isLoaded;

    /// <summary>Project 里拖的 Prefab 有时 isLoaded 为真但 <see cref="Scene.name"/> 为空，<see cref="InLoadedPlayScene"/> 会误判，仍不可 SetParent，必须 Instantiate。</summary>
    private static bool NeedsSceneResolve(GameObject g) =>
        g == null || !InLoadedPlayScene(g) || string.IsNullOrEmpty(g.scene.name);

    private bool TryMakeRuntimePanelOnCanvas(Canvas c)
    {
        if (panel == null) return false;
        var inst = Object.Instantiate(panel.gameObject, c.transform, false);
        var csp = inst.GetComponent<CardSelectionPanel>();
        if (csp == null)
        {
            Object.Destroy(inst);
            Debug.LogError("[CardSelectionSystem] 实例上缺少 CardSelectionPanel 组件。");
            return false;
        }
        _panelRuntime = csp;
        panel = csp;
        return true;
    }

    /// <summary>2021.3 无 IsInPrefabAsset：Project Prefab 在 isLoaded+有场景名时仍可能不可 SetParent，故**禁止**对引用做 SetParent，缺 Canvas 时只 Instantiate 到 canvas 下。</summary>
    private void EnsurePanelInActiveSceneAndUnderCanvas()
    {
        if (panel == null) return;
        if (_panelRuntime != null && NeedsSceneResolve(panel.gameObject))
            panel = _panelRuntime;

        var p = panel;
        var go = p.gameObject;
        if (go == null) return;

        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[CardSelectionSystem] 场景中无 Canvas，选卡面可能不显示为 UI。");
            return;
        }

        if (NeedsSceneResolve(go))
        {
            if (_panelRuntime != null)
            {
                panel = _panelRuntime;
                p = panel;
                go = p.gameObject;
            }
            if (!InLoadedPlayScene(go))
            {
                try
                {
                    var act = SceneManager.GetActiveScene();
                    if (act.IsValid() && act.isLoaded)
                        SceneManager.MoveGameObjectToScene(go, act);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[CardSelectionSystem] MoveGameObjectToScene: " + ex.Message);
                }
                go = p.gameObject;
            }

            if (NeedsSceneResolve(go))
            {
                if (_panelRuntime == null)
                {
                    if (!TryMakeRuntimePanelOnCanvas(canvas))
                        return;
                    p = panel;
                    go = p.gameObject;
                }
                else
                {
                    panel = _panelRuntime;
                    p = panel;
                    go = p.gameObject;
                }
            }
        }

        p = panel;
        go = p.gameObject;
        if (p.GetComponentInParent<Canvas>(true) == null)
        {
            if (_panelRuntime == null)
            {
                if (!TryMakeRuntimePanelOnCanvas(canvas))
                    return;
            }
            else
            {
                panel = _panelRuntime;
                p = panel;
                go = p.gameObject;
            }
        }

    }

    /// <summary>自顶向下面激活整条父链，并保证 Canvas 已启用。</summary>
    private static void EnsureTransformHierarchyActive(Transform t)
    {
        if (t == null) return;
        if (NeedsSceneResolve(t.gameObject)) return;
        var chain = new List<Transform>(8);
        for (var x = t; x != null; x = x.parent)
            chain.Add(x);
        for (int i = chain.Count - 1; i >= 0; i--)
            chain[i].gameObject.SetActive(true);

        var cv = t.GetComponentInParent<Canvas>(true);
        if (cv != null) cv.enabled = true;
    }
}
