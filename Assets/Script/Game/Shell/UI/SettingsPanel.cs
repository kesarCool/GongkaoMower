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

    [Header("关于")]
    [SerializeField] private Button aboutButton;

    [Header("按钮")]
    [SerializeField] private Button closeButton;

    [Header("礼包码")]
    [Tooltip("微信小游戏必须用 Unity 原生 InputField（非 TMP），否则 WeixinMiniGameInput 键盘不弹。")]
    [SerializeField] private InputField giftCodeInput;
    [SerializeField] private Button claimButton;

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

        if (aboutButton != null)
            aboutButton.onClick.AddListener(OnAboutClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        // 礼包码
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimClicked);
        RefreshClaimState();
    }

    public override void OnClose()
    {
        if (soundToggle != null)
            soundToggle.onValueChanged.RemoveListener(OnBgmToggled);
        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSfxToggled);
        if (aboutButton != null)
            aboutButton.onClick.RemoveListener(OnAboutClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        if (claimButton != null)
            claimButton.onClick.RemoveListener(OnClaimClicked);
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

    private void OnAboutClicked()
    {
        UiClickSound.Play();
        UIManager.Instance.Open<AboutPanel>(null, UiOpenOptions.NonPausingModal);
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }

    private void OnClaimClicked()
    {
        UiClickSound.Play();

        string input = giftCodeInput != null ? giftCodeInput.text : string.Empty;
        string error = GiftCodeService.TryRedeem(input);

        if (error == null)
        {
            // 成功
            UIManager.Instance.ShowToast("兑换成功！100钻石已到账");
            if (giftCodeInput != null)
                giftCodeInput.text = string.Empty;
            RefreshClaimState();
        }
        else
        {
            UIManager.Instance.ShowToast(error);
        }
    }

    /// <summary>根据今日是否已兑换刷新输入框和按钮的交互态。</summary>
    private void RefreshClaimState()
    {
        bool canClaim = GiftCodeService.CanClaimToday;
        if (giftCodeInput != null)
            giftCodeInput.interactable = canClaim;
        if (claimButton != null)
        {
            claimButton.interactable = canClaim;
            var label = claimButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null)
                label.text = canClaim ? "兑 换" : "今日已兑";
        }
    }
}
