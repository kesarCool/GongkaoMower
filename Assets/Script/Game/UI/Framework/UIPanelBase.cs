using UnityEngine;

/// <summary>
/// 弹窗基类：由 UIManager 负责遮罩、栈序、返回键与唯一实例；业务重写 OnOpen / OnClose 与数据绑定。
/// <para>动画过渡：子类重写 <see cref="GetOpenTransition"/> / <see cref="GetCloseTransition"/> 定制风格，返回 <see cref="TransitionType.None"/> 则跳过动画。</para>
/// </summary>
public abstract class UIPanelBase : MonoBehaviour
{
    [Header("可选：全屏挡点击")]
    [Tooltip("若为 null，UIManager 可使用全局 stackBackdrop。建议在 Prefab 根上挂一块全屏 Image（raycastTarget=true）。")]
    public UnityEngine.UI.Graphic backgroundBlocker;

    /// <summary>本次打开时由框架写入，可在 OnOpen 内读取。</summary>
    public UiOpenOptions LastOptions { get; internal set; }

    /// <summary>框架在动画播完后调用；payload 由 Open&lt;T&gt;(object) 传入，自行强转。</summary>
    public virtual void OnOpen(object payload) { }

    /// <summary>关闭动画播完后调用（含被上层挤掉、CloseTop、CloseAll）。</summary>
    public virtual void OnClose() { }

    /// <summary>是否使用 unscaled 时间（便于 Update 里写动画）。</summary>
    protected bool UseUnscaledTime => LastOptions.UseUnscaledTime;

    /// <summary>供框架查询：本 Panel 打开时是否申请了暂停锁。</summary>
    internal bool AppliedPauseLock { get; set; }

    internal void ResetForPoolOrReuse()
    {
        AppliedPauseLock = false;
        _canvasGroup = null;
    }

    // ── 动画过渡 ──

    /// <summary>打开动画配置。子类重写以定制风格；返回 TransitionType.None 则跳过动画。</summary>
    public virtual PanelTransition GetOpenTransition() =>
        new PanelTransition(TransitionType.FadeAndScale, 0.25f);

    /// <summary>关闭动画配置。子类重写以定制风格；返回 TransitionType.None 则跳过动画。</summary>
    public virtual PanelTransition GetCloseTransition() =>
        new PanelTransition(TransitionType.FadeAndScale, 0.2f);

    private CanvasGroup _canvasGroup;

    /// <summary>过渡动画用的 CanvasGroup（懒加载，无则 AddComponent）。</summary>
    internal CanvasGroup TransitionCanvasGroup
    {
        get
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            return _canvasGroup;
        }
    }
}
