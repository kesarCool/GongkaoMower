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

    [Header("词汇预览（可选）")]
    [Tooltip("打开词汇表预览弹窗（ThemePackId / CategoryTag 页签）。")]
    [SerializeField] private Button openLexiconPreviewButton;

    [Tooltip("为 true 时打开词汇预览使用 ModalDefault。")]
    [SerializeField] private bool pauseWhenLexiconPreviewOpen;

    [Header("背包（可选）")]
    [SerializeField] private Button openBackpackButton;

    [Header("设置")]
    [Tooltip("打开设置弹窗的按钮（右上角齿轮）。")]
    [SerializeField] private Button openSettingsButton;

    [Header("成就")]
    [Tooltip("打开成就面板的按钮。")]
    [SerializeField] private Button openAchievementButton;

    [Header("数据")]
    [Tooltip("角色目录（供 RedDotService 等使用，非 Resources 路径）。")]
    [SerializeField] private CharacterCatalog characterCatalog;

    [Header("红点角标")]
    [Tooltip("成就按钮的红点角标。")]
    [SerializeField] private RedDotBadge achievementBadge;

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
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.AddListener(OpenLevelSelect);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.AddListener(OpenLexiconPreview);
        if (openBackpackButton != null)
            openBackpackButton.onClick.AddListener(OpenBackpack);
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettings);
        if (openAchievementButton != null)
            openAchievementButton.onClick.AddListener(OpenAchievement);

        // 战斗/角色切换按钮由 HomeTabBar 管理（Inspector 中拖入 HomeTabBar.tabs[i].button）

        // 注入 CharacterCatalog（不在 Resources 下），并触发首次红点重算
        if (characterCatalog != null)
            RedDotService.Instance.SetCharacterCatalog(characterCatalog);

        RefreshCurrencyHud();

        if (achievementBadge != null)
            achievementBadge.Refresh();

        EventBus.Subscribe<PlayerDataChangedEvent>(OnPlayerDataChanged, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDataChangedEvent>(OnPlayerDataChanged);

        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.RemoveListener(OpenLevelSelect);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.RemoveListener(OpenLexiconPreview);
        if (openBackpackButton != null)
            openBackpackButton.onClick.RemoveListener(OpenBackpack);
        if (openSettingsButton != null)
            openSettingsButton.onClick.RemoveListener(OpenSettings);
        if (openAchievementButton != null)
            openAchievementButton.onClick.RemoveListener(OpenAchievement);
    }

    private void OnPlayerDataChanged(PlayerDataChangedEvent _)
    {
        RefreshCurrencyHud();
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

    /// <summary>打开设置面板（模态弹窗）。</summary>
    public void OpenSettings()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<SettingsPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开成就面板（模态弹窗）。</summary>
    public void OpenAchievement()
    {
        UiClickSound.Play();
        if (TableManager.Instance != null)
            TableManager.Instance.Init();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<AchievementPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开背包面板（模态弹窗）。</summary>
    public void OpenBackpack()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<BackPackPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开词汇预览（模态弹窗）：先 <see cref="TableManager.Init"/> 再弹窗。</summary>
    public void OpenLexiconPreview()
    {
        UiClickSound.Play();
        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (UIManager.Instance == null)
        {
            GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing);
            return;
        }

        var opt = pauseWhenLexiconPreviewOpen ? UiOpenOptions.ModalDefault : UiOpenOptions.NonPausingModal;
        UIManager.Instance.Open<LexiconPreviewPanel>(null, opt);
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
