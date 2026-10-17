/// <summary>
/// 面板过渡动画类型。
/// </summary>
public enum TransitionType
{
    /// <summary>无动画，直接显示/隐藏（Toast、Confirm 等）。</summary>
    None,
    /// <summary>仅透明度淡入/淡出。</summary>
    Fade,
    /// <summary>仅缩放弹入/弹出。</summary>
    Scale,
    /// <summary>淡入 + 缩放（默认）。</summary>
    FadeAndScale,
}

/// <summary>
/// 面板过渡动画配置：由 <see cref="UIPanelBase.GetOpenTransition"/> / <see cref="GetCloseTransition"/> 返回。
/// <para>子类重写可定制动画风格与时长。</para>
/// </summary>
public struct PanelTransition
{
    public TransitionType type;
    /// <summary>动画时长（秒）。Open 推荐 0.2~0.3，Close 推荐 0.15~0.25。</summary>
    public float duration;

    public PanelTransition(TransitionType type, float duration)
    {
        this.type = type;
        this.duration = duration;
    }
}
