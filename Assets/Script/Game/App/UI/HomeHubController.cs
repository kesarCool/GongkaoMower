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

    private void OnEnable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.AddListener(OpenLevelSelect);
    }

    private void OnDisable()
    {
        if (openLevelSelectButton != null)
            openLevelSelectButton.onClick.RemoveListener(OpenLevelSelect);
    }

    /// <summary>供其它入口（页签等）复用：先表后弹窗。</summary>
    public void OpenLevelSelect()
    {
        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        if (UIManager.Instance == null)
        {
            Debug.LogWarning("[HomeHubController] UIManager.Instance 为空，请在 Home 场景配置 UIManager 并注册 LevelSelectPanel Prefab。");
            return;
        }

        var opt = pauseWhenLevelSelectOpen ? UiOpenOptions.ModalDefault : UiOpenOptions.NonPausingModal;
        UIManager.Instance.Open<LevelSelectPanel>(null, opt);
    }
}
