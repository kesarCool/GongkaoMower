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

    [Header("UI引用")]
    [Tooltip("选卡面板（含3个卡槽位）")]
    public CardSelectionPanel panel;

    [Header("刷新配置")]
    [Tooltip("每局免费刷新次数")]
    public int freeRefreshCount = 1;
    [Tooltip("广告刷新次数")]
    public int adRefreshCount = 1;

    // 状态
    private bool _isSelecting = false;
    private Queue<CardSelectionRequest> _pendingRequests = new Queue<CardSelectionRequest>();
    private List<CardDeck.DrawResult> _currentCards;
    private List<SkillId> _excludedSkills = new List<SkillId>();
    private int _remainingFreeRefresh;
    private int _remainingAdRefresh;
    private bool _shareRefreshInFlight;
    private int _selectionVersion;
    private int _shareRefreshRequestVersion;

    private PlayerSkills _playerSkills;

    /// <summary>若 Inspector 指向 Prefab 资源（非场景实例），运行时 <see cref="EnsurePanelInActiveSceneAndUnderCanvas"/> 会 <see cref="Object.Instantiate"/> 一次并复用此引用，禁止对资源 Transform 作 SetParent/Move。</summary>
    private CardSelectionPanel _panelRuntime;

    /// <summary>本次选卡是否由 <see cref="UIManager"/> 打开（影响暂停与关闭方式）。</summary>
    private bool _selectionUsesUIManager;

    /// <summary>是否 Boss 触发（triggerCount=-1），防误触用更长阈值。</summary>
    public bool IsBossTrigger { get; set; }

    private void Awake()
    {
        _playerSkills = GetComponent<PlayerSkills>();
        if (_playerSkills == null)
            _playerSkills = FindObjectOfType<PlayerSkills>();

        // 每局只初始化一次刷新次数
        _remainingFreeRefresh = freeRefreshCount;
        _remainingAdRefresh = adRefreshCount;
    }

    /// <summary>
    /// 由 <see cref="RoguelikeCardManager"/> 在能量事件时调用
    /// </summary>
    public void BeginSelectionFromManager()
    {
        GameLog.Info("[CardTrace] CardSelectionSystem.BeginSelectionFromManager: enter");

        if (_playerSkills == null)
        {
            GameLog.Warning("[CardTrace] CardSelectionSystem: _playerSkills is null, abort");
            Debug.LogWarning("[CardSelectionSystem] PlayerSkills not found");
            return;
        }

        if (_playerSkills.AllSlotsFullAndMaxLevel)
        {
            GameLog.Info("[CardTrace] CardSelectionSystem: All skills maxed, skip");
            return;
        }

        if (_isSelecting)
        {
            GameLog.Info("[CardTrace] CardSelectionSystem: already selecting, queued");
            _pendingRequests.Enqueue(new CardSelectionRequest());
            GameLog.Info($"[CardSelectionSystem] Queued request, pending={_pendingRequests.Count}");
            return;
        }

        GameLog.Info("[CardTrace] CardSelectionSystem: calling StartSelection");
        StartSelection();
    }

    private void StartSelection()
    {
        _selectionVersion++;
        _shareRefreshInFlight = false;
        _selectionUsesUIManager = false;
        _isSelecting = true;
        _excludedSkills.Clear();

        try
        {
            DrawCards();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[CardSelectionSystem] Draw/Show failed: " + ex);
            EndSelection("exception");
        }
    }

    private void DrawCards()
    {
        int levelId = BattleLevelContext.LevelId;
        if (RoguelikeCardManager.Instance != null)
        {
            _currentCards = RoguelikeCardManager.Instance.DrawFromPool(levelId, _playerSkills, _excludedSkills);
        }
        else if (deck != null)
        {
            _currentCards = deck.Draw(levelId, _playerSkills, _excludedSkills);
        }
        else
        {
            _currentCards = null;
        }

        if (_currentCards == null || _currentCards.Count == 0)
        {
            Debug.LogWarning("[CardSelectionSystem] 无可用卡牌");
            // 必须走 EndSelection 清理 _isSelecting，否则后续选卡请求永久排队
            EndSelection("no-cards");
            return;
        }

        GameLog.Info($"[CardTrace] CardSelectionSystem: 抽到 {_currentCards.Count} 张卡，打开选卡面板");

        if (UIManager.Instance != null)
        {
            var payload = new CardSelectionOpenPayload
            {
                Cards = _currentCards,
                OnCardSelected = OnCardSelected,
                OnRefreshRequested = OnRefreshRequested,
                OnAdRefreshRequested = OnAdRefreshRequested,
                FreeRefreshCount = _remainingFreeRefresh,
                AdRefreshCount = _remainingAdRefresh,
                InputDelay = IsBossTrigger ? 1.2f : .3f
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
                GameLog.Info($"[CardTrace] UIManager 打开成功, panel.active={opened.gameObject.activeSelf} activeInHierarchy={opened.gameObject.activeInHierarchy}");
                _selectionUsesUIManager = true;
                return;
            }
            GameLog.Warning("[CardTrace] UIManager.Open 返回 null，回退 panel.Show");
            Debug.LogWarning("[CardSelectionSystem] UIManager 未能打开 CardSelectionPanel，请在 UIManager.panelPrefabs 中注册选卡 Prefab。将回退到 CardSelectionSystem.panel。");
        }

        _selectionUsesUIManager = false;
        if (panel != null)
        {
            EnsurePanelInActiveSceneAndUnderCanvas();
            EnsureTransformHierarchyActive(panel.transform);
            Time.timeScale = 0f;
            var showOk = panel.Show(_currentCards, OnCardSelected, OnRefreshRequested, OnAdRefreshRequested,
                _remainingFreeRefresh, _remainingAdRefresh, IsBossTrigger ? 1.2f : .3f);
            GameLog.Info($"[CardTrace] panel.Show 返回={showOk}, panel.active={panel.gameObject.activeSelf} activeInHierarchy={panel.gameObject.activeInHierarchy}");
            if (!showOk)
                EndSelection("panel.Show-false");
        }
        else
        {
            ApplyCard(_currentCards[0]);
            EndSelection("no-panel-auto-apply");
        }
    }

    private void OnRefreshRequested()
    {
        if (!_isSelecting || _shareRefreshInFlight) return;
        if (_remainingFreeRefresh <= 0 && _remainingAdRefresh <= 0) return;

        var prevCards = _currentCards;
        int prevFree = _remainingFreeRefresh;
        int prevAd = _remainingAdRefresh;

        foreach (var c in _currentCards)
            _excludedSkills.Add(c.skillId);

        if (_remainingFreeRefresh > 0)
            _remainingFreeRefresh--;
        else
            _remainingAdRefresh--;

        // 重新抽卡（先带排除，不足 3 张则清排除重抽）
        int levelId = BattleLevelContext.LevelId;
        _currentCards = DrawFromPoolInternal(levelId, _excludedSkills);

        if (_currentCards == null || _currentCards.Count < 3)
        {
            GameLog.Info("[CardSelectionSystem] 排除后不足 3 张，清排除重抽");
            _excludedSkills.Clear();
            _currentCards = DrawFromPoolInternal(levelId, null);
        }

        // 重抽仍无卡：回退，隐藏刷新按钮
        if (_currentCards == null || _currentCards.Count == 0)
        {
            _currentCards = prevCards;
            _remainingFreeRefresh = 0;
            _remainingAdRefresh = 0;
            Debug.LogWarning("[CardSelectionSystem] 刷新无可用卡牌，隐藏刷新按钮");
        }

        // 直接更新面板，不走 UIManager.Open（刷新不需要重建面板）
        CardSelectionPanel targetPanel = null;
        if (_selectionUsesUIManager && UIManager.Instance != null)
            UIManager.Instance.TryGetInstance(out targetPanel);
        if (targetPanel == null)
            targetPanel = panel;

        if (targetPanel != null)
        {
            targetPanel.Show(_currentCards, OnCardSelected, OnRefreshRequested, OnAdRefreshRequested,
                _remainingFreeRefresh, _remainingAdRefresh);
        }
    }

    /// <summary>
    /// 分享返回后刷新卡牌；调用失败或未观察到返回则次数不变，可重试。
    /// </summary>
    private void OnAdRefreshRequested()
    {
        if (!_isSelecting || _shareRefreshInFlight || _remainingAdRefresh <= 0) return;

        _shareRefreshInFlight = true;
        int version = _selectionVersion;
        int requestVersion = ++_shareRefreshRequestVersion;
        var targetPanel = CurrentPanel();
        targetPanel?.SetRewardRequestPending(true);
        UIManager.Instance?.ShowToast("分享返回后换一组", 2f);
        GameLog.Info("[CardSelectionSystem] 请求分享返回刷新…");
        System.Action<bool> complete = success =>
        {
            if (this == null || !_isSelecting || !_shareRefreshInFlight || _selectionVersion != version || _shareRefreshRequestVersion != requestVersion) return;
            _shareRefreshInFlight = false;
            CurrentPanel()?.SetRewardRequestPending(false);
            if (success)
            {
                GameLog.Info("[CardSelectionSystem] 分享返回，刷新卡牌");
                OnRefreshRequested(); // 内部扣减 _remainingAdRefresh 并重抽
            }
            else
            {
                Debug.LogWarning("[CardSelectionSystem] 未能确认分享返回");
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowToast("未能确认分享返回，可重试", 1.5f);
                // 次数不回退（面板端也未扣减）
            }
        };
        try { WeChatRewardedAdProvider.Instance.RequestShareReward("快来一起玩文字割草机！", complete); }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[CardSelectionSystem] 分享请求异常: " + ex.Message);
            complete(false);
        }
    }

    private CardSelectionPanel CurrentPanel()
    {
        if (_selectionUsesUIManager && UIManager.Instance != null && UIManager.Instance.TryGetInstance<CardSelectionPanel>(out var opened))
            return opened;
        return panel;
    }

    /// <summary>
    /// 玩家选择了某张卡
    /// </summary>
    private void OnCardSelected(int index)
    {
        if (!_isSelecting || _shareRefreshInFlight) return;
        if (index < 0 || index >= _currentCards.Count) return;

        // The panel checks input readiness before locking/animating a card. Rejecting
        // here used to leave every button disabled when an early click was ignored.

        var selected = _currentCards[index];
        if (ApplyCard(selected)) TutorialDirector.Ensure().NotifyCardApplied();
        EndSelection("card-picked");
    }

    /// <summary>
    /// 应用卡牌效果
    /// </summary>
    private bool ApplyCard(CardDeck.DrawResult card)
    {
        bool isPassive = card.skillId.IsPassive();
        if (card.currentLevel == 0)
        {
            // 新技能
            bool added = isPassive
                ? _playerSkills.TryAddPassive(card.skillId)
                : _playerSkills.TryAddSkill(card.skillId);
            GameLog.Info($"[CardSelectionSystem] New {(isPassive ? "passive" : "skill")} {card.skillId}, added={added}");
            return added;
        }
        else
        {
            // 升级
            bool leveled = isPassive
                ? _playerSkills.TryLevelUpPassive(card.skillId)
                : _playerSkills.TryLevelUp(card.skillId);
            GameLog.Info($"[CardSelectionSystem] Upgrade {(isPassive ? "passive" : "skill")} {card.skillId} to Lv.{card.targetLevel}, success={leveled}");
            return leveled;
        }
    }

    /// <summary>
    /// 结束选卡，恢复游戏
    /// </summary>
    private void EndSelection(string reason)
    {
        GameLog.Info($"[CardTrace] CardSelectionSystem.EndSelection reason={reason}");
        _isSelecting = false;
        _selectionVersion++;
        _shareRefreshInFlight = false;
        CurrentPanel()?.SetRewardRequestPending(false);
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

        EventBus.Publish(new CardSelectionEndedEvent());

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

    private List<CardDeck.DrawResult> DrawFromPoolInternal(int levelId, List<SkillId> excluded)
    {
        if (RoguelikeCardManager.Instance != null)
            return RoguelikeCardManager.Instance.DrawFromPool(levelId, _playerSkills, excluded);
        if (deck != null)
            return deck.Draw(levelId, _playerSkills, excluded);
        return null;
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
