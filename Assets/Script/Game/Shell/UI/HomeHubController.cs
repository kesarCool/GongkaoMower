using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home 大厅编排：页签、快捷入口；统一在打开选关等弹窗前保证 <see cref="TableManager.Init"/>。
/// 挂在 Home 场景 Canvas 下任意物体上，将按钮与 <see cref="UIManager"/> 配置好。
/// </summary>
[DisallowMultipleComponent]
public class HomeHubController : MonoBehaviour
{
    [Header("选关")]
    [Tooltip("打开选关弹窗的按钮（如「选关」「关卡」）。")]
    [SerializeField] private Button openLevelSelectButton;

    [Tooltip("为 true 时打开选关使用 ModalDefault（会参与暂停栈）；大厅一般有动画则建议 false 使用 NonPausingModal）。")]
    [SerializeField] private bool pauseWhenLevelSelectOpen;

    [Header("开始游戏")]
    [Tooltip("直接进入当前关卡（默认 = 最大已解锁关卡）。")]
    [SerializeField] private Button startGameButton;

    [Header("选角")]
    [Tooltip("打开角色选择弹窗。")]
    [SerializeField] private Button openCharacterSelectionButton;

    [Header("词汇预览")]
    [Tooltip("打开词汇表预览弹窗（ThemePackId / CategoryTag 页签）。")]
    [SerializeField] private Button openLexiconPreviewButton;

    [Tooltip("为 true 时打开词汇预览使用 ModalDefault。")]
    [SerializeField] private bool pauseWhenLexiconPreviewOpen;

    [Header("背包")]
    [SerializeField] private Button openBackpackButton;

    [Header("货币 HUD")]
    [Tooltip("金币数量文本（TMP）。")]
    [SerializeField] private TMPro.TextMeshProUGUI goldHudText;
    [Tooltip("钻石数量文本（TMP），预留。")]
    [SerializeField] private TMPro.TextMeshProUGUI diamondHudText;

    private void Start()
    {
        // Start 在所有 Awake/OnEnable 之后，UIManager 已就绪，可以安全弹框
        PlayerProfileService.Instance.ConsumeCorruptionAlert();
    }

    private void OnEnable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.AddListener(OpenLevelSelect);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartCurrentLevel);
        if (openCharacterSelectionButton != null)
            openCharacterSelectionButton.onClick.AddListener(OpenCharacterSelection);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.AddListener(OpenLexiconPreview);
        if (openBackpackButton != null)
            openBackpackButton.onClick.AddListener(OpenBackpack);

        RefreshCurrencyHud();
    }

    private void OnDisable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.RemoveListener(OpenLevelSelect);
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(StartCurrentLevel);
        if (openCharacterSelectionButton != null)
            openCharacterSelectionButton.onClick.RemoveListener(OpenCharacterSelection);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.RemoveListener(OpenLexiconPreview);
        if (openBackpackButton != null)
            openBackpackButton.onClick.RemoveListener(OpenBackpack);
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

    /// <summary>
    /// 开始游戏：将 <see cref="SelectedLevelContext"/> 设为最大已解锁关卡并进入 BattleLoading。
    /// </summary>
    public void StartCurrentLevel()
    {
        UiClickSound.Play();
        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (!ChapterLevelNavigation.TryGetMaxUnlockedLevel(out int chapterId, out int levelId))
        {
            GameErrorPresenter.Show(GameErrorCodes.TableManagerMissing);
            return;
        }

        SelectedLevelContext.Set(chapterId, levelId);
        BattleFlowLauncher.TryStartBattleLoading();
    }

    /// <summary>打开角色选择弹窗。</summary>
    public void OpenCharacterSelection()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null)
        {
            GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing);
            return;
        }

        UIManager.Instance.Open<CharacterSelectionPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开背包面板。</summary>
    public void OpenBackpack()
    {
        UiClickSound.Play();
        if (UIManager.Instance == null) { GameErrorPresenter.Show(GameErrorCodes.UiManagerMissing); return; }
        UIManager.Instance.Open<BackPackPanel>(null, UiOpenOptions.NonPausingModal);
    }

    /// <summary>打开词汇预览：先 <see cref="TableManager.Init"/> 再弹窗。</summary>
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

    /// <summary>刷新顶部货币 HUD（场景加载 / 从局内返回时调用）。</summary>
    public void RefreshCurrencyHud()
    {
        PlayerProfileService.Instance.LoadOrCreate();
        int gold = PlayerProfileService.Instance.Gold;
        int diamond = PlayerProfileService.Instance.Diamond;

        if (goldHudText != null)
            goldHudText.text = $"金币 {PlayerProfileService.FormatGold(gold)}";
        if (diamondHudText != null)
            diamondHudText.text = $"钻石 {diamond}";
    }
}
