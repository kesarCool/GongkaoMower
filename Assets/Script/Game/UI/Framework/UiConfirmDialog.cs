using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弱 B：叠在主栈顶之上的小型确认框（单独一层，不弹出主栈 Panel）。
/// </summary>
[DisallowMultipleComponent]
public class UiConfirmDialog : UIPanelBase
{
    [Tooltip("标题（可为空）")]
    public TextMeshProUGUI titleText;

    [Tooltip("正文")]
    public TextMeshProUGUI messageText;

    [Tooltip("确认按钮")]
    public Button okButton;

    [Tooltip("取消按钮（可为 null，仅单按钮）")]
    public Button cancelButton;

    private Action<bool> _callback;
    private bool _singleButtonMode;

    /// <summary>单按钮告警（隐藏取消）；返回键等同点确认。</summary>
    public bool IsSingleButtonMode => _singleButtonMode;

    public void Show(string title, string message, Action<bool> onClosed, bool showCancel = true)
    {
        _singleButtonMode = !showCancel;
        _callback = onClosed;
        if (titleText != null)
        {
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            titleText.text = title ?? "";
        }
        if (messageText != null) messageText.text = message ?? "";

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(showCancel);

        gameObject.SetActive(true);

        if (okButton != null)
        {
            okButton.onClick.RemoveListener(OnOk);
            okButton.onClick.AddListener(OnOk);
        }
        if (cancelButton != null && showCancel)
        {
            cancelButton.onClick.RemoveListener(OnCancel);
            cancelButton.onClick.AddListener(OnCancel);
        }
    }

    public void Show(string title, string message, Action<bool> onClosed)
    {
        Show(title, message, onClosed, showCancel: true);
    }

    public override void OnClose()
    {
        _callback = null;
        _singleButtonMode = false;
        if (okButton != null) okButton.onClick.RemoveListener(OnOk);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancel);
    }

    /// <summary>返回键 / Escape：双按钮视为取消；单按钮告警视为确认。</summary>
    internal void InvokeCancelIfPending()
    {
        if (_callback == null) return;
        if (_singleButtonMode)
        {
            OnOk();
            return;
        }

        var cb = _callback;
        _callback = null;
        cb.Invoke(false);
    }

    private void OnOk()
    {
        var cb = _callback;
        _callback = null;
        cb?.Invoke(true);
        if (UIManager.Instance != null)
            UIManager.Instance.CloseConfirm();
    }

    private void OnCancel()
    {
        var cb = _callback;
        _callback = null;
        cb?.Invoke(false);
        if (UIManager.Instance != null)
            UIManager.Instance.CloseConfirm();
    }
}
