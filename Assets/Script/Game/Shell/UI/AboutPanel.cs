using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关于弹窗：署名第三方资源（技能图标、美术素材等）。
/// 从设置页"关于"按钮打开。
/// </summary>
[DisallowMultipleComponent]
public class AboutPanel : UIPanelBase
{
    [Header("显示")]
    [SerializeField] private TMPro.TextMeshProUGUI versionText;

    [Header("按钮")]
    [SerializeField] private Button closeButton;

    public override void OnOpen(object payload)
    {
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        if (versionText != null)
            versionText.text = $"版本号：v{Application.version}";

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    public override void OnClose()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }
}
