using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选关弹窗：由 <see cref="UIManager"/> 打开；列表绑定可在 <see cref="OnOpen"/> 中接 <see cref="TableManager"/>。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectPanel : UIPanelBase
{
    [Header("基础交互")]
    [Tooltip("点击后关闭本层弹窗（委托 UIManager.CloseTop）。")]
    [SerializeField] private Button closeButton;

    [Header("进局")]
    [Tooltip("确认已选关卡后进入 BattleLoading（须已在 Build Settings 中加入 BattleLoading、Game）。")]
    [SerializeField] private Button enterBattleButton;

    public override void OnOpen(object payload)
    {
        var simple = GetComponentInChildren<LevelSelectSimpleScrollList>(true);
        if (simple != null)
        {
            simple.RefreshFromTable();
            return;
        }

        var loopDriver = GetComponentInChildren<LevelSelectLoopScrollDriver>(true);
        if (loopDriver != null)
            loopDriver.RefreshFromTable();
    }

    private void OnEnable()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (enterBattleButton != null)
            enterBattleButton.onClick.AddListener(OnEnterBattleClicked);
    }

    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        if (enterBattleButton != null)
            enterBattleButton.onClick.RemoveListener(OnEnterBattleClicked);
    }

    private void OnEnterBattleClicked()
    {
        BattleFlowLauncher.TryStartBattleLoading();
    }

    private void OnCloseClicked()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.CloseTop();
    }
}
