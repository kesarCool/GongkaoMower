using UnityEngine;

/// <summary>
/// 弹窗基类：由 UIManager 负责遮罩、栈序、返回键与唯一实例；业务重写 OnOpen / OnClose 与数据绑定。
/// </summary>
public abstract class UIPanelBase : MonoBehaviour
{
    [Header("可选：全屏挡点击")]
    [Tooltip("若为 null，UIManager 可使用全局 stackBackdrop。建议在 Prefab 根上挂一块全屏 Image（raycastTarget=true）。")]
    public UnityEngine.UI.Graphic backgroundBlocker;

    /// <summary>本次打开时由框架写入，可在 OnOpen 内读取。</summary>
    public UiOpenOptions LastOptions { get; internal set; }

    /// <summary>框架在入栈并显示前调用；payload 由 Open&lt;T&gt;(object) 传入，自行强转。</summary>
    public virtual void OnOpen(object payload) { }

    /// <summary>关闭时调用（含被上层挤掉、CloseTop、CloseAll）。</summary>
    public virtual void OnClose() { }

    /// <summary>是否使用 unscaled 时间（便于 Update 里写动画）。</summary>
    protected bool UseUnscaledTime => LastOptions.UseUnscaledTime;

    /// <summary>供框架查询：本 Panel 打开时是否申请了暂停锁。</summary>
    internal bool AppliedPauseLock { get; set; }

    internal void ResetForPoolOrReuse()
    {
        AppliedPauseLock = false;
    }
}
