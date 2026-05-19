using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 从 Home / 选关弹窗发起：校验 <see cref="SelectedLevelContext"/>，关闭栈顶弹窗，进入 <c>BattleLoading</c> 场景。
/// </summary>
public static class BattleFlowLauncher
{
    public const string BattleLoadingSceneName = "BattleLoading";

    /// <summary>若未选关返回 false；否则异步加载 BattleLoading。</summary>
    public static bool TryStartBattleLoading()
    {
        if (!SelectedLevelContext.HasSelection)
        {
            GameErrorPresenter.Show(GameErrorCodes.LevelNotSelected);
            return false;
        }

        PlayerProfileService.Instance.LoadOrCreate();
        int levelId = SelectedLevelContext.LevelId;
        if (!PlayerProfileService.Instance.IsLevelUnlocked(levelId))
        {
            GameErrorPresenter.Show(GameErrorCodes.LevelLocked,null, levelId);
            return false;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.CloseTop();

        SceneManager.LoadSceneAsync(BattleLoadingSceneName, LoadSceneMode.Single);
        return true;
    }
}
