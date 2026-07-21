using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 微信小游戏 InputField 键盘拉起。
/// 用 .jslib bridge 直调 wx.showKeyboard / wx.onKeyboardInput，
/// 完全绕过 Unity TouchScreenKeyboard（isSupported=False 不可用）
/// 和 WX C# API（需要 WeixinMiniGame 独立构建目标，此项目无）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InputField))]
public class WechatInputFieldEnabler : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private InputField _inputField;
    private bool _active;

    private void Awake()
    {
        _inputField = GetComponent<InputField>();
    }

    void ISelectHandler.OnSelect(BaseEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _active = true;
        WxKeyboard_Clear();
        WxKeyboard_Show(_inputField != null ? _inputField.text ?? "" : "", 50);
        Debug.Log("[Keyboard] Show");
#endif
    }

    void IDeselectHandler.OnDeselect(BaseEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _active = false;
        WxKeyboard_Hide();
        Debug.Log("[Keyboard] Hide (deselect)");
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_active) return;
        string value = ReadKeyboardValue();
        if (!string.IsNullOrEmpty(value) && _inputField != null && _inputField.text != value)
            _inputField.text = value;
#endif
    }

    private void OnDisable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_active)
        {
            _active = false;
            WxKeyboard_Hide();
        }
#endif
    }

    private void OnDestroy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_active)
        {
            _active = false;
            WxKeyboard_Hide();
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WxKeyboard_Show(string defaultValue, int maxLength);

    [DllImport("__Internal")]
    private static extern int WxKeyboard_GetValue(IntPtr buffer, int bufferSize);

    [DllImport("__Internal")]
    private static extern void WxKeyboard_Hide();

    [DllImport("__Internal")]
    private static extern void WxKeyboard_Clear();

    private static string ReadKeyboardValue()
    {
        IntPtr ptr = Marshal.AllocHGlobal(1024);
        try
        {
            int len = WxKeyboard_GetValue(ptr, 1024);
            if (len <= 0) return "";
            byte[] buf = new byte[Math.Min(len, 1024)];
            Marshal.Copy(ptr, buf, 0, buf.Length);
            return Encoding.UTF8.GetString(buf);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
#endif
}
