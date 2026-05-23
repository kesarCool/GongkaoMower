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
    }

    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        if (UIManager.Instance != null)
            UIManager.Instance.CloseTop();
    }
}
