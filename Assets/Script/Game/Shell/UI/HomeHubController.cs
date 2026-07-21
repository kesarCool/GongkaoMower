using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home 大厅编排：页签、快捷入口；统一在打开选关等弹窗前保证 <see cref="TableManager.Init"/>。
/// 挂在 Home 场景 Canvas 下任意物体上，将按钮与 <see cref="UIManager"/> 配置好。
/// </summary>
[DisallowMultipleComponent]
public class HomeHubController : MonoBehaviour
{
    [Header("页签栏")]
    [Tooltip("HomeTabBar：管理底部按钮与 viewContainer 内视图切换。")]
    [SerializeField] private HomeTabBar homeTabBar;

    [Header("路线图（兼容旧引用）")]
    [Tooltip("Home 场景中的路线图视图（挂 HomeRoadmapView）。TabBar 会将其移入 viewContainer。")]
    [SerializeField] private HomeRoadmapView roadmapView;

    [Header("选关")]
    [Tooltip("打开选关弹窗的按钮（如「选关」「关卡」）。")]
    [SerializeField] private Button openLevelSelectButton;

    [Tooltip("为 true 时打开选关使用 ModalDefault（会参与暂停栈）；大厅一般有动画则建议 false 使用 NonPausingModal）。")]
    [SerializeField] private bool pauseWhenLevelSelectOpen;

    [Header("背包（可选）")]
    [SerializeField] private Button openBackpackButton;

    [Header("主界面按钮组")]
    [Tooltip("Home 主界面下的按钮容器节点（开始游戏/选关/背包/设置等）。Tab 切换到 battle 时显示，其余隐藏。")]
    [SerializeField] private GameObject homeButtonGroup;

    [Header("开始游戏")]
    [Tooltip("点击直接进入当前最新解锁关卡。")]
    [SerializeField] private Button startGameButton;

    [Header("设置")]
    [Tooltip("打开设置弹窗的按钮（右上角齿轮）。")]
    [SerializeField] private Button openSettingsButton;

    [Header("数据")]
    [Tooltip("角色目录（供 RedDotService 等使用，非 Resources 路径）。")]
    [SerializeField] private CharacterCatalog characterCatalog;

    [Header("货币 HUD")]
    [Tooltip("金币数量文本（TMP）。")]
    [SerializeField] private TMPro.TextMeshProUGUI goldHudText;
    [Tooltip("钻石数量文本（TMP），预留。")]
    [SerializeField] private TMPro.TextMeshProUGUI diamondHudText;

    private void Start()
    {
        // Start 在所有 Awake/OnEnable 之后，UIManager 已就绪，可以安全弹框
        PlayerProfileService.Instance.ConsumeCorruptionAlert();

        // 成就系统：登录天数（Init 已由 RuntimeInitializeOnLoadMethod 自动完成）
        AchievementService.Instance.OnEnterHome();
    }

    private void OnEnable()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.AddListener(OpenLevelSelect);
        if (openBackpackButton != null)
            openBackpackButton.onClick.AddListener(OpenBackpack);
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettings);

        // 战斗/角色切换按钮由 HomeTabBar 管理（Inspector 中拖入 HomeTabBar.tabs[i].button）

        // 主界面按钮显隐跟随页签切换
        if (homeTabBar != null)
            homeTabBar.OnTabChanged += OnHomeTabChanged;

        // 注入 CharacterCatalog（不在 Resources 下），并触发首次红点重算
        if (characterCatalog != null)
        {
            RedDotService.Instance.SetCharacterCatalog(characterCatalog);
            // 修复历史数据：itemIds 有但 characterFragmentKeys 缺失的碎片同步
            PlayerProfileService.Instance.HealFragmentData(characterCatalog);
            CharacterUnlockEvaluator.HealCharacterUnlocks(characterCatalog);
        }

        RefreshCurrencyHud();

        EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);

        if (homeTabBar != null)
            homeTabBar.OnTabChanged -= OnHomeTabChanged;

        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.RemoveListener(OpenLevelSelect);
        if (openBackpackButton != null)
            openBackpackButton.onClick.RemoveListener(OpenBackpack);
        if (openSettingsButton != null)
            openSettingsButton.onClick.RemoveListener(OpenSettings);
    }

    private void OnPlayerDataChanged(PlayerDataChangedEvent _)
    {
        RefreshCurrencyHud();
    }

    private void OnHomeTabChanged(string tabId)
    {
        if (homeButtonGroup != null)
            homeButtonGroup.SetActive(tabId == "battle");
    }

    /// <summary>供其它入口（页签等）复用：先表后弹窗。</summary>
    public void OpenLevelSelect()
    {
        UiClickSound.Play();
        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (UIManager.Instance == null)
        {
            GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing);
            return;
        }

        var opt = pauseWhenLevelSelectOpen ? UiOpenOptions.ModalDefault : UiOpenOptions.NonPausingModal;
        UIManager.Instance.Open<LevelSelectPanel>(null, opt);
    }

    /// <summary>开始游戏：取当前最新解锁关卡，直接进入战斗。</summary>
    private void OnStartGameClicked()
    {
        UiClickSound.Play();

        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (!ChapterLevelNavigation.TryGetMaxUnlockedLevel(out int chapterId, out int levelId))
        {
            UIManager.Instance?.ShowToast("关卡数据加载失败", 1.5f);
            return;
        }

        SelectedLevelContext.Set(chapterId, levelId);
        BattleFlowLauncher.TryStartBattleLoading();
    }

    /// <summary>打开设置面板（模态弹窗）。</summary>
    public void OpenSettings()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<SettingsPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开背包面板（模态弹窗）。</summary>
    public void OpenBackpack()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<BackPackPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>刷新顶部货币 HUD + 路线图 + 当前活跃页签。</summary>
    public void RefreshCurrencyHud()
    {
        PlayerProfileService.Instance.LoadOrCreate();
        int gold = PlayerProfileService.Instance.Gold;
        int diamond = PlayerProfileService.Instance.Diamond;

        if (goldHudText != null)
            goldHudText.text = $"{PlayerProfileService.FormatGold(gold)}";
        if (diamondHudText != null)
            diamondHudText.text = $"{PlayerProfileService.FormatGold(diamond)}";

        if (roadmapView != null)
            roadmapView.RefreshAll();

        if (homeTabBar != null)
            homeTabBar.RefreshActive();
    }
}
