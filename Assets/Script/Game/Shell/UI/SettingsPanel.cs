using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置弹窗：显示版本号 + 音效开关（PlayerPrefs 持久化）。
/// Home 大厅右上角齿轮图标打开。
/// </summary>
[DisallowMultipleComponent]
public class SettingsPanel : UIPanelBase
{
    [Header("显示")]
    [Tooltip("版本号文本（TMP 或 Text）。")]
    [SerializeField] private TMPro.TextMeshProUGUI versionText;

    [Tooltip("音效开关 Toggle。")]
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private TMPro.TextMeshProUGUI soundText;
    [Header("按钮")]
    [SerializeField] private Button closeButton;

    public override void OnOpen(object payload)
    {
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        if (versionText != null)
            versionText.text = $"版本号：v{Application.version}";

        if (soundToggle != null)
        {
            soundToggle.isOn = !AudioService.Instance.MasterMute;
            soundToggle.onValueChanged.AddListener(OnSoundToggled);
        }

        if (soundText != null)
            soundText.text = AudioService.Instance.MasterMute ? "音效关闭" : "音效开启";

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    public override void OnClose()
    {
        if (soundToggle != null)
            soundToggle.onValueChanged.RemoveListener(OnSoundToggled);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnSoundToggled(bool isOn)
    {
        AudioService.Instance.MasterMute = !isOn;
        if (soundText != null)
        {
            soundText.text = AudioService.Instance.MasterMute ? "音效关闭" : "音效开启";
        }
        
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }
}
