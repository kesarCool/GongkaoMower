using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TouchLayer
/// - 挂在一个 Canvas（Screen Space - Overlay）下的全屏透明 UI 上，用于“接收触摸/鼠标拖拽”。
/// - 通过 UnityEvent 将触摸的按下/移动/抬起事件广播给外部（例如 DynamicJoystick）。
///
/// 设计目标：
/// - 只跟踪第一个有效触摸点（多点触摸时忽略后续手指）。
/// - 事件坐标使用“屏幕坐标”（Input/Pointer 的 position），方便摇杆/其它系统直接使用。
///
/// 使用方式（推荐）：
/// 1) 在 TouchLayer 的 GameObject 上确保有 Image 组件（全屏），颜色 Alpha=0，Raycast Target=true。
/// 2) 在 Inspector 中将 OnTouchDown/OnTouchMove/OnTouchUp 绑定到外部脚本的方法（如 DynamicJoystick.OnTouchDown 等）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class TouchLayer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [System.Serializable]
    public class Vector2Event : UnityEvent<Vector2> { }

    [Header("Events (Screen Position)")]
    [Tooltip("手指/鼠标按下时触发（屏幕坐标）")]
    public Vector2Event OnTouchDown = new Vector2Event();

    [Tooltip("手指/鼠标拖动/滑动时持续触发（屏幕坐标）")]
    public Vector2Event OnTouchMove = new Vector2Event();

    [Tooltip("手指/鼠标抬起时触发")]
    public UnityEvent OnTouchUp = new UnityEvent();

    [Header("Behaviour")]
    [Tooltip("是否允许鼠标模拟触摸（编辑器/PC 调试用）")]
    public bool allowMouse = true;

    [Header("Debug")]
    [Tooltip("在 Console 打印按下/移动/抬起的屏幕坐标，用于排查摇杆不显示问题")]
    public bool debugLogPositions = true;

    [Tooltip("限制移动日志频率（秒）。0 表示每次 OnDrag 都打印。")]
    public float moveLogInterval = 0.05f;

    private Image _img;
    private int _activePointerId = int.MinValue; // 当前正在跟踪的 pointerId（多点触摸只保留第一个）
    private float _nextMoveLogTime;

    public bool HasActiveTouch => _activePointerId != int.MinValue;

    private void Awake()
    {
        // 确保这是一个“透明但可接收射线”的 UI 面板
        _img = GetComponent<Image>();
        _img.raycastTarget = true;
        if (_img.color.a > 0.001f)
        {
            // 不是强制，但常见需求是透明：避免挡住画面
            _img.color = new Color(_img.color.r, _img.color.g, _img.color.b, 0f);
        }
    }

    private void OnDisable()
    {
        // 如果触摸层被禁用（例如切场景/隐藏 UI），主动结束当前跟踪，避免外部状态卡住
        ForceRelease();
    }

    /// <summary>
    /// 当有指针按下（触摸/鼠标）：
    /// - 如果当前没有在跟踪任何指针，则锁定此 pointerId 作为“第一根有效手指”
    /// - 触发 OnTouchDown，并立即触发一次 OnTouchMove（让外部立刻得到位置）
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!allowMouse && eventData.pointerId < 0) return; // pointerId<0 通常是鼠标
        if (HasActiveTouch) return;

        _activePointerId = eventData.pointerId;
        Vector2 pos = eventData.position;
        if (debugLogPositions)
            GameLog.Info($"[TouchLayer] Down id={eventData.pointerId} pos={pos}");
        OnTouchDown.Invoke(pos);
        OnTouchMove.Invoke(pos);
    }

    /// <summary>
    /// 指针拖拽：
    /// - 只处理正在跟踪的那根手指/鼠标
    /// - 持续发送当前位置（屏幕坐标）
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!allowMouse && eventData.pointerId < 0) return;
        if (!HasActiveTouch) return;
        if (eventData.pointerId != _activePointerId) return;

        if (debugLogPositions)
        {
            if (moveLogInterval <= 0f || Time.unscaledTime >= _nextMoveLogTime)
            {
                _nextMoveLogTime = Time.unscaledTime + Mathf.Max(0f, moveLogInterval);
                GameLog.Info($"[TouchLayer] Move id={eventData.pointerId} pos={eventData.position}");
            }
        }
        OnTouchMove.Invoke(eventData.position);
    }

    /// <summary>
    /// 指针抬起：
    /// - 只响应当前正在跟踪的 pointerId
    /// - 触发 OnTouchUp 并释放
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!allowMouse && eventData.pointerId < 0) return;
        if (!HasActiveTouch) return;
        if (eventData.pointerId != _activePointerId) return;

        if (debugLogPositions)
            GameLog.Info($"[TouchLayer] Up id={eventData.pointerId} pos={eventData.position}");
        ForceRelease();
    }

    /// <summary>
    /// 强制释放当前触摸跟踪（会触发 OnTouchUp）
    /// </summary>
    public void ForceRelease()
    {
        if (!HasActiveTouch) return;

        _activePointerId = int.MinValue;
        if (debugLogPositions)
            GameLog.Info("[TouchLayer] Released");
        OnTouchUp.Invoke();
    }
}

