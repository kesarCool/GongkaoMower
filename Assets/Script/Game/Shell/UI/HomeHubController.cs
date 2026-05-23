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

    [Header("词汇预览")]
    [Tooltip("打开词汇表预览弹窗（ThemePackId / CategoryTag 页签）。")]
    [SerializeField] private Button openLexiconPreviewButton;

    [Tooltip("为 true 时打开词汇预览使用 ModalDefault。")]
    [SerializeField] private bool pauseWhenLexiconPreviewOpen;

    private void OnEnable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.AddListener(OpenLevelSelect);
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartCurrentLevel);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.AddListener(OpenLexiconPreview);
    }

    private void OnDisable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.RemoveListener(OpenLevelSelect);
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(StartCurrentLevel);
        if (openLexiconPreviewButton != null)
            openLexiconPreviewButton.onClick.RemoveListener(OpenLexiconPreview);
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
}
