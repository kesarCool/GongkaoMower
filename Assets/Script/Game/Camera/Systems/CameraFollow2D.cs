using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// CameraFollow2D
/// - 正交相机平滑跟随目标（SmoothDamp）
/// - 相机位置会被夹紧在“地图边界”内，保证不露出空白
///
/// 依赖：
/// - 需要一个 Orthographic Camera
/// - 需要指定一个用于计算地图边界的 Tilemap（通常是你的地面 Tilemap 或边界 Tilemap）
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow2D : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("要跟随的目标（通常是 Player）。不填则尝试用 Tag=Player 查找。")]
    public Transform target;

    [Tooltip("当 target 为空时，使用该 Tag 查找目标。")]
    public string targetTag = "Player";

    [Header("平滑跟随")]
    [Tooltip("SmoothDamp 平滑时间（越小越紧跟，越大越柔和）。建议 0.1~0.3。")]
    public float smoothTime = 0.15f;

    [Tooltip("相机跟随的世界空间偏移（一般保持 0）。")]
    public Vector3 followOffset = new Vector3(0f, 0f, -10f);

    [Header("地图边界（用于夹紧相机）")]
    [Tooltip("用于计算地图边界的 Tilemap（例如 GroundTilemap 或 BorderTilemap）。")]
    public Tilemap boundsTilemap;

    [Tooltip("是否启用边界夹紧（建议开启，避免显示空白）。")]
    public bool clampToBounds = true;

    [Header("Boss 波次镜头")]
    [Tooltip("Boss 波次镜头拉远倍率（>1=拉远，1=不变）。")]
    public float bossZoomMultiplier = 1.3f;
    [Tooltip("镜头缩放过渡速度（值越大越快）。")]
    public float zoomSpeed = 3f;

    private Camera _cam;
    private Vector3 _vel;
    private Vector3? _overrideTarget;
    private float _defaultOrthoSize;
    private float _targetOrthoSize;
    private bool _arenaTransition;
    private Vector3 _entryStart, _entryEnd;
    private float _entryZoomStart, _entryZoomEnd, _entryElapsed, _entryDelay, _entryDuration;
    public bool IsArenaTransitioning => _arenaTransition;

    /// <summary>Hold during departure, then pan and zoom while the player is hidden.</summary>
    public void BeginArenaTransition(Vector3 destination, float departureDuration, float travelDuration)
    {
        if (_cam == null) return;
        _overrideTarget = null;
        _vel = Vector3.zero;
        _arenaTransition = true;
        _entryStart = transform.position;
        _entryZoomStart = _cam.orthographicSize;
        _entryZoomEnd = _defaultOrthoSize * bossZoomMultiplier;
        _targetOrthoSize = _entryZoomEnd;
        _entryEnd = destination + followOffset;
        _entryEnd.z = followOffset.z;
        // Compute one stable endpoint at the final zoom. Clamping every intermediate
        // camera position to a moving zoom boundary creates visible edge jolts.
        if (clampToBounds && boundsTilemap != null)
            _entryEnd = ClampCameraToTilemapBounds(_entryEnd, boundsTilemap, _entryZoomEnd);
        _entryElapsed = 0f;
        _entryDelay = Mathf.Max(0, departureDuration);
        _entryDuration = Mathf.Max(.01f, travelDuration);
    }

    public void CancelArenaTransition()
    {
        _arenaTransition = false;
        _vel = Vector3.zero;
    }

    /// <summary>进入/退出 Boss 波次时切换镜头缩放。</summary>
    public void SetBossZoom(bool enterBoss)
    {
        _targetOrthoSize = enterBoss ? _defaultOrthoSize * bossZoomMultiplier : _defaultOrthoSize;
    }

    /// <summary>Boss 击杀后镜头锁定在指定世界坐标，直到 <see cref="ClearOverride"/>。</summary>
    public void OverrideFollow(Vector3 worldPos) => _overrideTarget = worldPos;
    public void ClearOverride() => _overrideTarget = null;

    /// <summary>瞬间跳转到目标位置并锁定镜头（Boss 击杀定格，跳过 SmoothDamp 延迟）。</summary>
    public void SnapAndLock(Vector3 worldPos)
    {
        CancelArenaTransition();
        _overrideTarget = worldPos;
        _vel = Vector3.zero;
        Vector3 desired = worldPos + followOffset;
        desired.z = followOffset.z;
        transform.position = desired;
    }

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            _defaultOrthoSize = _cam.orthographicSize;
            _targetOrthoSize = _defaultOrthoSize;
        }
    }

    /// <summary>Align the opening frame immediately, then keep following normally.</summary>
    public void SnapToTarget()
    {
        if (target == null) TryFindTarget();
        if (target == null || _cam == null) return;
        CancelArenaTransition();
        ClearOverride();
        Vector3 desired = target.position + followOffset;
        desired.z = followOffset.z;
        if (clampToBounds && boundsTilemap != null)
            desired = ClampCameraToTilemapBounds(desired, boundsTilemap, _cam.orthographicSize);
        transform.position = desired;
    }

    private void LateUpdate()
    {
        if (target == null) TryFindTarget();
        if (_cam == null) return;
        if (Time.timeScale <= 0f || Time.deltaTime <= 0f) return;

        if (_arenaTransition)
        {
            _entryElapsed += Time.deltaTime;
            float t = Mathf.Clamp01((_entryElapsed - _entryDelay) / _entryDuration);
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
            transform.position = Vector3.LerpUnclamped(_entryStart, _entryEnd, eased);
            _cam.orthographicSize = Mathf.LerpUnclamped(_entryZoomStart, _entryZoomEnd, eased);
            if (t >= 1f) CancelArenaTransition();
            return;
        }

        // Boss 镜头缩放
        if (Mathf.Abs(_cam.orthographicSize - _targetOrthoSize) > 0.005f)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(.01f, zoomSpeed) * Time.deltaTime);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, blend);
        }

        Vector3 desired;
        if (_overrideTarget.HasValue)
        {
            desired = _overrideTarget.Value + followOffset;
        }
        else
        {
            if (target == null) return;
            desired = target.position + followOffset;
        }
        desired.z = followOffset.z;

        if (clampToBounds && boundsTilemap != null)
            desired = ClampCameraToTilemapBounds(desired, boundsTilemap, _cam.orthographicSize);

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, Mathf.Max(0.001f, smoothTime));
    }

    private void TryFindTarget()
    {
        if (string.IsNullOrWhiteSpace(targetTag)) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;
    }

    /// <summary>
    /// 将相机位置夹紧到 Tilemap 的世界边界内，确保相机视口不会超出地图范围。
    /// </summary>
    private Vector3 ClampCameraToTilemapBounds(Vector3 camPos, Tilemap tm, float orthoSize)
    {
        // Tilemap.localBounds 是 Tilemap Transform 空间，需要转为世界空间 bounds
        Bounds local = tm.localBounds;
        Vector3 center = tm.transform.TransformPoint(local.center);
        Vector3 ext = Vector3.Scale(local.extents, tm.transform.lossyScale);
        Bounds world = new Bounds(center, ext * 2f);

        float halfH = orthoSize;
        float halfW = halfH * _cam.aspect;

        float minX = world.min.x + halfW;
        float maxX = world.max.x - halfW;
        float minY = world.min.y + halfH;
        float maxY = world.max.y - halfH;

        // 如果地图比相机视口还小，直接锁到中心，避免抖动/反向夹紧
        if (minX > maxX) camPos.x = world.center.x;
        else camPos.x = Mathf.Clamp(camPos.x, minX, maxX);

        if (minY > maxY) camPos.y = world.center.y;
        else camPos.y = Mathf.Clamp(camPos.y, minY, maxY);

        camPos.z = followOffset.z;
        return camPos;
    }
}

