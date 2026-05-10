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

    private Camera _cam;
    private Vector3 _vel;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null) TryFindTarget();
        if (_cam == null) return;
        if (target == null) return;

        Vector3 desired = target.position + followOffset;
        desired.z = followOffset.z;

        Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref _vel, Mathf.Max(0.001f, smoothTime));

        if (clampToBounds && boundsTilemap != null)
            smoothed = ClampCameraToTilemapBounds(smoothed, boundsTilemap);

        transform.position = smoothed;
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
    private Vector3 ClampCameraToTilemapBounds(Vector3 camPos, Tilemap tm)
    {
        // Tilemap.localBounds 是 Tilemap Transform 空间，需要转为世界空间 bounds
        Bounds local = tm.localBounds;
        Vector3 center = tm.transform.TransformPoint(local.center);
        Vector3 ext = Vector3.Scale(local.extents, tm.transform.lossyScale);
        Bounds world = new Bounds(center, ext * 2f);

        float halfH = _cam.orthographicSize;
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

