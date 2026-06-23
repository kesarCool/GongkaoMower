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

    [Header("背景音乐")]
    [Tooltip("BGM 开关 Toggle（复用原有音效开关，Inspector 标签请改为“背景音乐”）。")]
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private TMPro.TextMeshProUGUI soundText;

    [Header("音效")]
    [Tooltip("音效开关 Toggle。")]
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private TMPro.TextMeshProUGUI sfxText;

    [Header("按钮")]
    [SerializeField] private Button closeButton;

    public override void OnOpen(object payload)
    {
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        if (versionText != null)
            versionText.text = $"版本号：v{Application.version}";

        if (soundToggle != null)
        {
            soundToggle.isOn = !AudioService.Instance.BgmMute;
            soundToggle.onValueChanged.AddListener(OnBgmToggled);
        }
        RefreshBgmLabel();

        if (sfxToggle != null)
        {
            sfxToggle.isOn = !AudioService.Instance.SfxMute;
            sfxToggle.onValueChanged.AddListener(OnSfxToggled);
        }
        RefreshSfxLabel();

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    public override void OnClose()
    {
        if (soundToggle != null)
            soundToggle.onValueChanged.RemoveListener(OnBgmToggled);
        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSfxToggled);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnBgmToggled(bool isOn)
    {
        AudioService.Instance.BgmMute = !isOn;
        RefreshBgmLabel();
    }

    private void OnSfxToggled(bool isOn)
    {
        AudioService.Instance.SfxMute = !isOn;
        RefreshSfxLabel();
    }

    private void RefreshBgmLabel()
    {
        if (soundText != null)
            soundText.text = AudioService.Instance.BgmMute ? "音乐关" : "音乐开";
    }

    private void RefreshSfxLabel()
    {
        if (sfxText != null)
            sfxText.text = AudioService.Instance.SfxMute ? "音效关" : "音效开";
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }
}
