using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic joystick for mobile using legacy Input.GetTouch.
/// Creates (and reuses) a simple joystick UI (background + handle) on a Screen Space - Overlay canvas.
/// Controls player movement via Rigidbody2D.velocity.
/// </summary>
public class DynamicJoystick : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("可选：拖入主角的 Rigidbody2D；如果不填，会尝试通过 Tag=Player 自动查找。")]
    public Rigidbody2D playerRigidbody;

    [Tooltip("当未手动指定主角 Rigidbody2D 时，用于查找主角的 Tag。")]
    public string playerTag = "Player";

    [Tooltip("主角移动速度。留空时自动从 PlayerController 读取。")]
    public float moveSpeedFallback = 5f;

    private PlayerController _playerController;
    private float MoveSpeed
    {
        get
        {
            if (_playerController != null) return _playerController.moveSpeed;
            if (playerRigidbody != null)
            {
                _playerController = playerRigidbody.GetComponent<PlayerController>();
                if (_playerController != null) return _playerController.moveSpeed;
            }
            return moveSpeedFallback;
        }
    }

    [Header("Joystick UI")]
    [Tooltip("可选：你的摇杆预制体（根为 RectTransform，建议包含子物体 Outer 和 Inner）。\n" +
             "如果拖入的是 Project 里的 prefab 资源：运行时会实例化 1 个并复用。\n" +
             "如果拖入的是场景里的摇杆对象：将直接复用该对象（不会重复生成）。")]
    public RectTransform joystickPrefab;

    [Tooltip("可选：摇杆实例要挂在哪个父物体下（例如 TouchLayerCanvas/TouchPanel）。不填则挂到找到的 Overlay Canvas 根节点下。")]
    public RectTransform joystickParent;

    [Tooltip("可选：外圈 RectTransform（当你的预制体层级/命名不是 Outer 时需要手动指定）。")]
    public RectTransform outerOverride;

    [Tooltip("可选：内点/拇指 RectTransform（当你的预制体层级/命名不是 Inner 时需要手动指定）。")]
    public RectTransform innerOverride;

    [Tooltip("外圈半径（像素）。用于限制内点移动范围与计算方向。")]
    public float outerRadius = 80f;

    [Tooltip("内点/拇指半径（像素）。仅用于自动调整 UI 尺寸（可关闭）。")]
    public float innerRadius = 20f;

    [Tooltip("手指抬起后延迟多少秒隐藏摇杆。")]
    public float hideDelay = 1f;

    [Tooltip("是否根据半径自动调整外圈/内点的 UI 尺寸（直径=半径*2）。如果你已在 prefab 里做了尺寸，建议关闭。")]
    public bool autoResizeFromRadius = false;

    public bool JoystickActive => _isTouching;

    private Canvas _canvas;
    private RectTransform _canvasRect;

    private RectTransform _joystickRoot;
    private RectTransform _outerRect;
    private RectTransform _innerRect;
    private Image _outerImg;
    private Image _innerImg;

    private bool _isTouching;
    private int _fingerId = -1;
    private Vector2 _startScreenPos;
    private Vector2 _currentInput; // normalized direction (0..1 length)

    private float _hideTimer = -1f;

    // 如果你使用 TouchLayer（UI 事件）来驱动摇杆，可以在外部通过 UnityEvent 调用下面三个方法，
    // 这样就不需要 DynamicJoystick 自己在 Update 里用 Input.GetTouch 轮询。
    [Header("Optional External Input (TouchLayer)")]
    [Tooltip("勾选后：摇杆不再在 Update 中读取 Input.GetTouch，而是依赖外部（如 TouchLayer）调用 OnTouchDown/OnTouchMove/OnTouchUp。")]
    public bool useExternalTouchEvents = false;

    [Header("Debug")]
    [Tooltip("在 Console 打印摇杆收到的事件与显示/隐藏信息，用于排查摇杆不出现")]
    public bool debugLogs = true;

    private void Start()
    {
        EnsureCanvas();
        BuildJoystickUI();
        HideJoystickImmediate();

        if (playerRigidbody == null)
            TryFindPlayerRigidbody();
        else
            EnsurePlayerRotationFrozen();
    }

    private void Update()
    {
        if (playerRigidbody == null)
            TryFindPlayerRigidbody();

        if (!useExternalTouchEvents)
            HandleTouches();
        ApplyMovement();
        HandleHideTimer();
    }

    /// <summary>
    /// 供 TouchLayer 绑定：触摸按下（screen position）
    /// </summary>
    public void OnTouchDown(Vector2 screenPos)
    {
        if (debugLogs)
            Debug.Log($"[DynamicJoystick] OnTouchDown pos={screenPos} self={name} joystickRoot={(_joystickRoot == null ? "null" : _joystickRoot.name)} active={( _joystickRoot != null && _joystickRoot.gameObject.activeSelf)}");
        BeginTouchSynthetic(screenPos);
    }

    /// <summary>
    /// 供 TouchLayer 绑定：触摸移动（screen position）
    /// </summary>
    public void OnTouchMove(Vector2 screenPos)
    {
        if (debugLogs) Debug.Log($"[DynamicJoystick] OnTouchMove pos={screenPos}");
        UpdateTouchSynthetic(screenPos);
    }

    /// <summary>
    /// 供 TouchLayer 绑定：触摸抬起
    /// </summary>
    public void OnTouchUp()
    {
        if (debugLogs) Debug.Log("[DynamicJoystick] OnTouchUp");
        EndTouch();
    }

    private void EnsureCanvas()
    {
        // If user specified a parent, prefer its canvas (common when you have separate GameLayer/TouchLayer canvases).
        if (joystickParent != null)
        {
            Canvas parentCanvas = joystickParent.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                _canvas = parentCanvas;
                _canvasRect = parentCanvas.GetComponent<RectTransform>();
                return;
            }
        }

        // Unity 2021 compatible: find an existing Screen Space - Overlay canvas if possible.
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        if (canvases != null)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    _canvas = canvases[i];
                    _canvasRect = _canvas.GetComponent<RectTransform>();
                    return;
                }
            }
        }

        // Prefer an existing ScreenSpaceOverlay canvas; if none exists, create one.
        GameObject canvasGo = new GameObject("DynamicJoystickCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // 设计分辨率：1080x1920（竖屏）
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRect = canvasGo.GetComponent<RectTransform>();
    }

    private void BuildJoystickUI()
    {
        RectTransform parent = joystickParent != null ? joystickParent : _canvasRect;

        // Important: joystickPrefab can be either:
        // - a prefab asset (Project) -> instantiate once
        // - an existing scene instance (Hierarchy) -> reuse it (do NOT instantiate again)
        if (joystickPrefab != null && joystickPrefab.gameObject.scene.IsValid())
        {
            _joystickRoot = joystickPrefab;
            _joystickRoot.SetParent(parent, false);
            if (debugLogs) Debug.Log("[DynamicJoystick] Using scene joystick instance (reuse, no Instantiate).");
        }
        else if (joystickPrefab != null)
        {
            _joystickRoot = Instantiate(joystickPrefab, parent);
            _joystickRoot.name = joystickPrefab.name;
            if (debugLogs) Debug.Log("[DynamicJoystick] Instantiated joystick prefab once.");
        }
        else
        {
            _joystickRoot = new GameObject("DynamicJoystick", typeof(RectTransform)).GetComponent<RectTransform>();
            _joystickRoot.SetParent(parent, false);
        }

        _joystickRoot.anchorMin = new Vector2(0f, 0f);
        _joystickRoot.anchorMax = new Vector2(0f, 0f);
        _joystickRoot.pivot = new Vector2(0.5f, 0.5f);
        _joystickRoot.anchoredPosition = Vector2.zero;
        _joystickRoot.sizeDelta = Vector2.zero;
        _joystickRoot.SetAsLastSibling(); // ensure it's on top within its parent

        // 关键：让摇杆根节点使用“屏幕中心锚点”，这样 ScreenPointToLocalPointInRectangle
        // 得到的 localPos（基于父节点 pivot）可以直接作为 anchoredPosition 使用，不会出现左下偏移。
        _joystickRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _joystickRoot.anchorMax = new Vector2(0.5f, 0.5f);

        // Resolve outer/inner references:
        _outerRect = outerOverride != null ? outerOverride : _joystickRoot.Find("Outer") as RectTransform;
        _innerRect = innerOverride != null ? innerOverride : _joystickRoot.Find("Inner") as RectTransform;

        // If prefab does not use the expected names, try a fallback search.
        if (_outerRect == null || _innerRect == null)
        {
            RectTransform[] rts = _joystickRoot.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rts.Length; i++)
            {
                if (_outerRect == null && rts[i].name.ToLower().Contains("outer")) _outerRect = rts[i];
                if (_innerRect == null && rts[i].name.ToLower().Contains("inner")) _innerRect = rts[i];
                if (_innerRect == null && rts[i].name.ToLower().Contains("thumb")) _innerRect = rts[i];
            }
        }

        // If we are not using prefab, create default images as before.
        if (joystickPrefab == null)
        {
            if (_outerRect == null)
            {
                _outerRect = new GameObject("Outer", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                _outerRect.SetParent(_joystickRoot, false);
                _outerRect.anchorMin = new Vector2(0.5f, 0.5f);
                _outerRect.anchorMax = new Vector2(0.5f, 0.5f);
                _outerRect.pivot = new Vector2(0.5f, 0.5f);
                _outerRect.anchoredPosition = Vector2.zero;
            }

            if (_innerRect == null)
            {
                _innerRect = new GameObject("Inner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                _innerRect.SetParent(_joystickRoot, false);
                _innerRect.anchorMin = new Vector2(0.5f, 0.5f);
                _innerRect.anchorMax = new Vector2(0.5f, 0.5f);
                _innerRect.pivot = new Vector2(0.5f, 0.5f);
                _innerRect.anchoredPosition = Vector2.zero;
            }

            _outerImg = _outerRect.GetComponent<Image>();
            if (_outerImg == null) _outerImg = _outerRect.gameObject.AddComponent<Image>();
            _outerImg.raycastTarget = false;
            _outerImg.sprite = GetBuiltinUISprite();
            _outerImg.type = Image.Type.Sliced;
            _outerImg.color = new Color(1f, 1f, 1f, 0.25f);

            _innerImg = _innerRect.GetComponent<Image>();
            if (_innerImg == null) _innerImg = _innerRect.gameObject.AddComponent<Image>();
            _innerImg.raycastTarget = false;
            _innerImg.sprite = GetBuiltinUISprite();
            _innerImg.type = Image.Type.Sliced;
            _innerImg.color = new Color(1f, 1f, 1f, 0.6f);
        }

        if (_outerRect == null || _innerRect == null)
        {
            Debug.LogError("DynamicJoystick: Cannot find Outer/Inner RectTransform. " +
                           "Please set outerOverride/innerOverride or name children as 'Outer' and 'Inner'.");
            return;
        }

        // Ensure the visuals can actually move as expected (common prefab pitfall: stretch anchors).
        ForceCenterAnchors(_outerRect);
        ForceCenterAnchors(_innerRect);

        if (autoResizeFromRadius)
        {
            _outerRect.sizeDelta = new Vector2(outerRadius * 2f, outerRadius * 2f);
            _innerRect.sizeDelta = new Vector2(innerRadius * 2f, innerRadius * 2f);
        }

        if (debugLogs)
        {
            string cName = _canvas != null ? _canvas.name : "null";
            Debug.Log($"[DynamicJoystick] UI ready. canvas={cName}, root={_joystickRoot.name}, outer={_outerRect.name}, inner={_innerRect.name}");
        }
    }

    private void ForceCenterAnchors(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private Sprite GetBuiltinUISprite()
    {
        return RuntimeSprites.GetUiPlaceholderSprite();
    }

    private void HandleTouches()
    {
        if (Input.touchCount <= 0)
            return;

        // If we are not currently tracking, try to lock to the first Began touch.
        if (!_isTouching)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    BeginTouch(t);
                    break;
                }
            }
            return;
        }

        // We are tracking a finger id: find that touch.
        bool found = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId != _fingerId) continue;

            found = true;
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                UpdateTouch(t);
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                EndTouch();

            break;
        }

        // If finger is no longer present, treat as ended.
        if (!found)
            EndTouch();
    }

    private void BeginTouch(Touch t)
    {
        _isTouching = true;
        _fingerId = t.fingerId;
        _startScreenPos = t.position;
        _currentInput = Vector2.zero;

        ShowJoystickAtScreenPos(_startScreenPos);
        ResetInnerToCenter();

        // Cancel pending hide
        _hideTimer = -1f;
    }

    // 用于外部事件驱动的“伪 Touch Begin”
    private void BeginTouchSynthetic(Vector2 screenPos)
    {
        if (_joystickRoot == null)
        {
            if (debugLogs) Debug.Log("[DynamicJoystick] BeginTouchSynthetic: joystickRoot is null, rebuilding UI.");
            EnsureCanvas();
            BuildJoystickUI();
        }

        _isTouching = true;
        _fingerId = 0;
        _startScreenPos = screenPos;
        _currentInput = Vector2.zero;

        ShowJoystickAtScreenPos(_startScreenPos);
        ResetInnerToCenter();
        _hideTimer = -1f;
    }

    private void UpdateTouch(Touch t)
    {
        Vector2 delta = t.position - _startScreenPos; // in screen pixels
        Vector2 clamped = Vector2.ClampMagnitude(delta, outerRadius);

        // inner knob position in local space equals the same pixel delta (Overlay canvas)
        _innerRect.anchoredPosition = clamped;

        float mag01 = Mathf.Clamp01(clamped.magnitude / Mathf.Max(1f, outerRadius));
        if (mag01 <= 0.0001f)
        {
            _currentInput = Vector2.zero;
        }
        else
        {
            // Requirement: "direction normalized"
            _currentInput = clamped.normalized; // ignore distance for speed
        }
    }

    // 用于外部事件驱动的“伪 Touch Move”
    private void UpdateTouchSynthetic(Vector2 screenPos)
    {
        if (!_isTouching) return;

        Vector2 delta = screenPos - _startScreenPos;
        Vector2 clamped = Vector2.ClampMagnitude(delta, outerRadius);

        if (_innerRect != null)
            _innerRect.anchoredPosition = clamped;

        float mag01 = Mathf.Clamp01(clamped.magnitude / Mathf.Max(1f, outerRadius));
        if (mag01 <= 0.0001f) _currentInput = Vector2.zero;
        else _currentInput = clamped.normalized;
    }

    private void EndTouch()
    {
        _isTouching = false;
        _fingerId = -1;
        _currentInput = Vector2.zero;
        ResetInnerToCenter();

        if (hideDelay <= 0f)
        {
            HideJoystickImmediate();
        }
        else
        {
            _hideTimer = hideDelay;
        }
    }

    private float _lastLoggedSpeed = -1f;
    private void ApplyMovement()
    {
        if (playerRigidbody == null) return;

        if (_isTouching && _currentInput.sqrMagnitude > 0.0001f)
        {
            float spd = MoveSpeed;
            playerRigidbody.velocity = _currentInput * spd;
            if (debugLogs && Mathf.Abs(spd - _lastLoggedSpeed) > 0.01f)
            {
                Debug.Log($"[DynamicJoystick] ApplyMovement MoveSpeed={spd:F1} vel={playerRigidbody.velocity.magnitude:F1}");
                _lastLoggedSpeed = spd;
            }
        }
        else
        {
            playerRigidbody.velocity = Vector2.zero;
        }
    }

    private void HandleHideTimer()
    {
        if (_hideTimer < 0f) return;

        _hideTimer -= Time.deltaTime;
        if (_hideTimer <= 0f)
        {
            _hideTimer = -1f;
            HideJoystickImmediate();
        }
    }

    private void ShowJoystickAtScreenPos(Vector2 screenPos)
    {
        if (_joystickRoot == null)
        {
            if (debugLogs) Debug.Log($"[DynamicJoystick] ShowJoystickAtScreenPos: joystickRoot is null. self={name}");
            return;
        }

        _joystickRoot.gameObject.SetActive(true);
        _joystickRoot.SetAsLastSibling();

        // Convert screen point to canvas local position
        Vector2 localPos;
        // Use the root's parent rect as reference (works even if joystickParent is a panel, not canvas root).
        RectTransform refRect = _joystickRoot.parent as RectTransform;
        if (refRect == null) refRect = _canvasRect;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(refRect, screenPos, null, out localPos))
        {
            localPos = ClampLocalPosToBounds(localPos, refRect);
            _joystickRoot.anchoredPosition = localPos;
        }
        else
        {
            Vector2 fallback = ClampLocalPosToBounds(screenPos, refRect);
            _joystickRoot.anchoredPosition = fallback;
        }

        if (debugLogs)
            Debug.Log($"[DynamicJoystick] Show at screen={screenPos} local={_joystickRoot.anchoredPosition} active={_joystickRoot.gameObject.activeSelf}");
    }

    /// <summary>
    /// 边界检测：保证摇杆外圈不会出屏幕（或出父面板）范围。
    /// localPos 是 refRect 的本地坐标（以 refRect 的 pivot 为原点）。
    /// </summary>
    private Vector2 ClampLocalPosToBounds(Vector2 localPos, RectTransform refRect)
    {
        if (refRect == null) return localPos;

        // 留边：至少保证外圈完整可见
        float padX = outerRadius;
        float padY = outerRadius;
        if (_outerRect != null)
        {
            // 使用实际 UI 尺寸作为留边更准确（兼容你自己做的 prefab 缩放/尺寸）
            float halfW = _outerRect.rect.width * 0.5f;
            float halfH = _outerRect.rect.height * 0.5f;
            if (halfW > 0.01f) padX = halfW;
            if (halfH > 0.01f) padY = halfH;
        }

        Rect r = refRect.rect; // 本地空间矩形（以 pivot 为原点）
        float minX = r.xMin + padX;
        float maxX = r.xMax - padX;
        float minY = r.yMin + padY;
        float maxY = r.yMax - padY;

        localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, minY, maxY);
        return localPos;
    }

    private void HideJoystickImmediate()
    {
        if (_joystickRoot != null)
            _joystickRoot.gameObject.SetActive(false);

        if (debugLogs)
            Debug.Log("[DynamicJoystick] Hide");
    }

    private void ResetInnerToCenter()
    {
        if (_innerRect != null)
            _innerRect.anchoredPosition = Vector2.zero;
    }

    private void TryFindPlayerRigidbody()
    {
        if (string.IsNullOrWhiteSpace(playerTag)) return;
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p == null) return;
        playerRigidbody = p.GetComponent<Rigidbody2D>();
        EnsurePlayerRotationFrozen();
    }

    private void EnsurePlayerRotationFrozen()
    {
        if (playerRigidbody == null) return;
        playerRigidbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
        playerRigidbody.angularVelocity = 0f;
    }
}

