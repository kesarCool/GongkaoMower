using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>按钮锁定状态分类（供 <see cref="UiLockedHint"/> 决定展示何种反馈）。</summary>
public enum UiLockState
{
    /// <summary>可正常点击：不提示。</summary>
    Available,
    /// <summary>业务锁定（金币不足/未解锁等）：抖动 + 错误码提示。</summary>
    Locked,
    /// <summary>忙碌/静默禁用（交易中、请求中、父级整体屏蔽等）：仅保留按压表现，不弹提示。</summary>
    Suppressed,
}

/// <summary>
/// 置灰按钮的点击解释反馈：水平抖动 + 按错误码 Toast/对话框。
/// 挂在与 Button 同物体上：Button.interactable=false 时 Unity 不再触发 onClick，
/// 但本组件的 Pointer 事件仍会派发，从而补上"点了为什么没反应"的原因说明。
/// 动态 Provider 优先（多原因按钮由业务实时评估）；未绑 Provider 时用序列化固定码（单一原因控件）。
/// 判定规则：本地 interactable=false 视为业务锁定；本地可点但 IsInteractable()=false 视为父级整体屏蔽（静默）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UiLockedHint : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerExitHandler
{
    /// <summary>一次锁定评估的结果。</summary>
    public readonly struct Status
    {
        public readonly UiLockState State;
        public readonly string ErrorCode;
        public readonly object[] FormatArgs;

        private Status(UiLockState state, string errorCode, object[] formatArgs)
        {
            State = state;
            ErrorCode = errorCode;
            FormatArgs = formatArgs;
        }

        /// <summary>可正常点击：不提示。</summary>
        public static Status Available() => new Status(UiLockState.Available, null, null);

        /// <summary>忙碌/静默：只保留按压表现，不弹提示。</summary>
        public static Status Silent() => new Status(UiLockState.Suppressed, null, null);

        /// <summary>业务锁定：抖动并按错误码提示；errorCode 为空则只抖动。formatArgs 对应 CodeText 的 {0}… 占位。</summary>
        public static Status Deny(string errorCode, params object[] formatArgs) => new Status(UiLockState.Locked, errorCode, formatArgs);
    }

    /// <summary>动态锁定状态提供者：业务实时判断当前是可点/可解释锁定/忙碌。返回值优先于固定错误码。</summary>
    public delegate Status Provider();

    [Tooltip("固定错误码：单一锁定原因控件用（售罄/今日已兑/前置关卡未解锁等）；绑定动态 Provider 后以 Provider 为准。为空则锁定点击只抖动不提示")]
    [SerializeField] private string errorCode = "";

    [Tooltip("锁定抖动幅度（UI 单位，水平方向；随 Canvas 缩放实际观感可调）")]
    [SerializeField, Range(1f, 30f)] private float shakeAmplitude = 8f;

    [Tooltip("锁定抖动总时长（秒）；走 unscaled 时间")]
    [SerializeField, Range(0.05f, 0.6f)] private float shakeDuration = 0.15f;

    [Tooltip("锁定抖动往返次数")]
    [SerializeField, Range(1, 5)] private int shakeCount = 2;

    private Button _button;
    private RectTransform _target;
    private Provider _provider;
    private Coroutine _shakeRoutine;
    private Vector2 _shakeBasePosition;
    private int _activePointerId = int.MinValue;
    private bool _pressWasLocked;

    /// <summary>序列化固定码（编辑器校验用只读入口）。</summary>
    public string FixedErrorCode => errorCode;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _target = (RectTransform)transform;
    }

    private void OnDisable() => ResetShake();

    /// <summary>绑定/解绑动态状态提供者（随面板生命周期调用，解绑传 null）。</summary>
    public void Bind(Provider provider) => _provider = provider;

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressWasLocked = false;
        if (!IsPrimaryPointer(eventData)) return;
        if (_activePointerId != int.MinValue && eventData.pointerId != _activePointerId) return;
        _activePointerId = eventData.pointerId;
        // 按下时刻评估锁定资格（click 在 onClick 之后派发，正常点击若在回调内自行置灰，不得补发失败提示）
        _pressWasLocked = EvaluateCurrent().State == UiLockState.Locked;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId) return;
        _activePointerId = int.MinValue;
        _pressWasLocked = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId || !_pressWasLocked) return;
        _activePointerId = int.MinValue;
        _pressWasLocked = false;
        // 复核：点击时仍为有效业务锁定才提示（期间被业务重新启用的不再报锁定）
        Status status = EvaluateCurrent();
        if (status.State != UiLockState.Locked) return;
        PlayLockedFeedback(status);
    }

    private Status EvaluateCurrent()
    {
        // 本地可点但整体被父级禁用（CanvasGroup.interactable=false 等）：不算业务锁定，避免屏蔽期误报
        if (_button.interactable && !_button.IsInteractable()) return Status.Silent();
        if (_provider != null) return _provider();
        // 固定码路径：本地置灰即业务锁定（原因单一的控件）
        return _button.interactable ? Status.Available() : Status.Deny(errorCode);
    }

    private void PlayLockedFeedback(Status status)
    {
        StartShake();
        if (!string.IsNullOrEmpty(status.ErrorCode))
            GameErrorPresenter.Show(status.ErrorCode, null, status.FormatArgs);
    }

    /// <summary>围绕原始位置水平抖动；重复点击重启动画，不叠加位移。与 UiPressScale 分属 anchoredPosition/localScale，互不覆盖。</summary>
    private void StartShake()
    {
        if (!isActiveAndEnabled) return;
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _target.anchoredPosition = _shakeBasePosition;
        }
        _shakeBasePosition = _target.anchoredPosition;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private void ResetShake()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
            if (_target != null) _target.anchoredPosition = _shakeBasePosition;
        }
        _activePointerId = int.MinValue;
        _pressWasLocked = false;
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shakeDuration);
            // 完整正弦周期 × 次数，幅度随进度衰减，结束时自然回到原位
            float offset = Mathf.Sin(t * shakeCount * Mathf.PI * 2f) * shakeAmplitude * (1f - t);
            _target.anchoredPosition = _shakeBasePosition + new Vector2(offset, 0f);
            yield return null;
        }
        _target.anchoredPosition = _shakeBasePosition;
        _shakeRoutine = null;
    }

    private static bool IsPrimaryPointer(PointerEventData eventData)
        => eventData != null && eventData.button == PointerEventData.InputButton.Left;
}
