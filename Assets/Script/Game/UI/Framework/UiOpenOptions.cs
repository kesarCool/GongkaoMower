using UnityEngine;

/// <summary>
/// 打开弹窗时的框架级选项（暂停、时间尺度、返回键等）。
/// </summary>
[System.Serializable]
public struct UiOpenOptions
{
    [Tooltip("为 true 时参与「暂停栈」：首层打开时记录并置 Time.timeScale=0，对应关闭时恢复。")]
    public bool PauseTime;

    [Tooltip("业务 Panel 内若需自行计时/动画，建议用此标志选用 unscaledDeltaTime（框架写入，可在 OnOpen 里读取 LastOptions）。")]
    public bool UseUnscaledTime;

    [Tooltip("为 true 时 Android 返回键 / Escape 会优先关闭本层（确认框优先于主栈顶）。")]
    public bool CloseOnBack;

    /// <summary>与旧版选卡、全屏模态一致：暂停 + 非缩放时间 + 返回关闭。</summary>
    public static UiOpenOptions ModalDefault => new UiOpenOptions
    {
        PauseTime = true,
        UseUnscaledTime = true,
        CloseOnBack = true
    };

    /// <summary>不暂停战斗，仅挡交互（例如轻提示壳子）。</summary>
    public static UiOpenOptions NonPausingModal => new UiOpenOptions
    {
        PauseTime = false,
        UseUnscaledTime = true,
        CloseOnBack = true
    };
}
