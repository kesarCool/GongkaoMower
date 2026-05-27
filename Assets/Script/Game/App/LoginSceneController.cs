using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 登录场景：检测首次玩家 → 快速入局（跳 Home）；老玩家 → 正常进 Home。
/// </summary>
[DisallowMultipleComponent]
public class LoginSceneController : MonoBehaviour
{
    [Header("导航")]
    [Tooltip("点击后进入 Home 场景（老玩家流程）。")]
    [SerializeField] private Button startGameButton;

    [Tooltip("Home 场景名，须在 Build Settings 中。")]
    [SerializeField] private string homeSceneName = "Home";

    [Tooltip("为 true 时使用 LoadSceneAsync。")]
    [SerializeField] private bool loadAsync = true;

    [Header("首次玩家快速入局")]
    [Tooltip("开启后，首次玩家（本地无存档）点击'开始游戏'直接进入 BattleLoading → 关卡 101，跳过 Home。关闭则所有玩家统一进 Home。")]
    [SerializeField] private bool enableQuickStartForNewPlayer = true;

    [Tooltip("快速入局的目标关卡 ID（默认 101）。")]
    [SerializeField] private int quickStartLevelId = 101;

    [Tooltip("快速入局的目标场景名，默认 BattleLoading。")]
    [SerializeField] private string quickStartSceneName = "BattleLoading";

    private void OnEnable()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
    }

    private void OnDisable()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
    }

    private void OnStartGameClicked()
    {
        UiClickSound.Play();
        PlayerProfileService.Instance.LoadOrCreate();

        // 首次玩家快速入局：无存档 → 跳过 Home，直接加载 BattleLoading 进关卡 101。
        // 目的是减少首次玩家的"点击开始→加载大厅→再点开始"的步骤，让玩家最快进入战斗。
        if (enableQuickStartForNewPlayer && IsNewPlayer())
        {
            QuickStartBattle();
            return;
        }

        GoToHome();
    }

    /// <summary>
    /// 本地没有存档文件 == 首次玩家（从未通关过任何关卡）。
    /// 注意：<see cref="PlayerProfileService.LoadOrCreate"/> 在内存中创建了空存档但未持久化，
    /// 所以只要 PlayerPrefs 中没有 SaveKey 就是真正的首次。
    /// </summary>
    private static bool IsNewPlayer()
    {
        return !PlayerPrefs.HasKey(PlayerProfileService.SaveKey);
    }

    /// <summary>
    /// 设置关卡上下文为 101（第一章第 1 关），直接进入 BattleLoading 场景。
    /// BattleLoading 负责分包加载、TableManager.Init、音频加载，然后进入 Game 场景。
    /// 结算后"退出"按钮会加载 Home，玩家自此进入正常流程。
    /// </summary>
    private void QuickStartBattle()
    {
        int chapterId = quickStartLevelId / 100;
        SelectedLevelContext.Set(chapterId, quickStartLevelId);

        if (loadAsync)
            SceneManager.LoadSceneAsync(quickStartSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(quickStartSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// 老玩家正常流程：进入 Home 大厅，自己选关或点开始。
    /// </summary>
    private void GoToHome()
    {
        if (string.IsNullOrWhiteSpace(homeSceneName))
        {
            GameErrorPresenter.Show(GameErrorCodes.LoginSceneNameEmpty);
            return;
        }

        if (loadAsync)
            SceneManager.LoadSceneAsync(homeSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(homeSceneName, LoadSceneMode.Single);
    }
}
