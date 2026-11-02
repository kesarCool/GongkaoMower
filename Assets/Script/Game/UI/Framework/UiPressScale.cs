using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 通用按压缩放反馈：按下缩小、抬起/移出回弹（unscaled 时间，暂停界面同样生效）。
/// Button.interactable=false 时 Unity 仍会向本组件派发 Pointer 事件，
/// 因此业务置灰（锁定）按钮同样有按压表现；父级整体禁用（CanvasGroup.interactable=false）则不做反馈。
/// 注意：不实现 Drag 系列接口，避免抢占父级 ScrollRect 的拖动事件路由。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("按压缩放系数：相对原始缩放的乘数（0.92=压到 92%；典型 0.88~0.96，越小按压感越重）")]
    [SerializeField, Range(0.7f, 1f)] private float pressScale = 0.92f;

    [Tooltip("按下与回弹各自的过渡时长（秒）；走 unscaled 时间")]
    [SerializeField, Range(0.02f, 0.3f)] private float duration = 0.08f;

    [Tooltip("缩放目标：为空时缩放按钮自身。若按钮根节点有布局/射线敏感逻辑，可指定纯视觉子节点，保持命中区域稳定")]
    [SerializeField] private RectTransform visualTarget;

    private Button _button;
    private RectTransform _target;
    private Coroutine _routine;
    private Vector3 _baseScale = Vector3.one;
    private int _activePointerId = int.MinValue;
    private bool _recovering; // 回弹进行中：连点时沿用原基准，防止尺寸被逐步踩小

    private void Awake()
    {
        _button = GetComponent<Button>();
        _target = visualTarget != null ? visualTarget : (RectTransform)transform;
    }

    private void OnDisable() => ResetImmediately();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsPrimaryPointer(eventData)) return;
        if (_activePointerId != int.MinValue && eventData.pointerId != _activePointerId) return;
        // 本地可点但整体被父级禁用：视为全局输入屏蔽，不做按压反馈
        if (_button.interactable && !_button.IsInteractable()) return;

        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (!_recovering) _baseScale = _target.localScale;
        _recovering = false;
        _activePointerId = eventData.pointerId;
        _routine = StartCoroutine(ScaleTo(_baseScale * pressScale, recovering: false));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId) return;
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId) return;
        Release();
    }

    private void Release()
    {
        _activePointerId = int.MinValue;
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        if (!isActiveAndEnabled) { ResetImmediately(); return; }
        _recovering = true;
        _routine = StartCoroutine(ScaleTo(_baseScale, recovering: true));
    }

    /// <summary>立即复位（禁用/回收/重开面板时调用），不残留缩放。</summary>
    private void ResetImmediately()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        bool hadActiveCycle = _activePointerId != int.MinValue || _recovering;
        _activePointerId = int.MinValue;
        _recovering = false;
        if (hadActiveCycle && _target != null) _target.localScale = _baseScale;
    }

    private IEnumerator ScaleTo(Vector3 targetScale, bool recovering)
    {
        Vector3 start = _target.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _target.localScale = Vector3.Lerp(start, targetScale, t);
            yield return null;
        }
        _target.localScale = targetScale;
        _routine = null;
        if (recovering) _recovering = false;
    }

    private static bool IsPrimaryPointer(PointerEventData eventData)
        => eventData != null && eventData.button == PointerEventData.InputButton.Left;
}
