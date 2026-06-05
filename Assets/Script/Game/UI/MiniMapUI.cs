using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 左下角小地图：Tilemap 缩略图 + 红点标记玩家位置。
/// 用独立 Camera 渲染 Tilemap 到小 RenderTexture，每 4 帧更新一次（省性能）。
/// 挂在 GameLayer Canvas 下，或任意场景 UI。
/// </summary>
[DisallowMultipleComponent]
public class MiniMapUI : MonoBehaviour
{
    [Header("显示")]
    [Tooltip("小地图 RawImage（挂到 Canvas 左下角）")]
    [SerializeField] private RawImage mapImage;

    [Tooltip("玩家红点 RectTransform")]
    [SerializeField] private RectTransform playerDot;

    [Tooltip("小地图渲染尺寸（像素）")]
    [SerializeField] private int mapResolution = 256;

    [Tooltip("小地图面板大小（像素）")]
    [SerializeField] private float panelSize = 256f;

    [Header("相机")]
    [Tooltip("小地图专用摄像机（脚本自动创建）")]
    [SerializeField] private Camera miniCamera;

    [Tooltip("小地图渲染层（只显示该层的物体）。把 Tilemap 单独设一层如 Map 即可隔离特效。")]
    [SerializeField] private LayerMask cullingMask = -1;

    [Header("目标")]
    [SerializeField] private Transform player;

    private RenderTexture _rt;
    private RectTransform _mapRt;
    private Tilemap _tilemap;
    private Vector2 _mapWorldMin;
    private Vector2 _mapWorldSize;
    private int _frameSkip = 4;
    private int _frameCount;

    private void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        FindTilemap();

        // 如果面板已存在，删掉重建（适配 Inspector 改过 panelSize / mapResolution）
        var oldPanel = transform.Find("MiniMap");
        if (oldPanel != null) Destroy(oldPanel.gameObject);
        if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
        if (mapImage != null) mapImage = null;

        BuildUI();
        _mapRt = mapImage != null ? mapImage.rectTransform : null;
        SetupCamera();
    }

    private void FindTilemap()
    {
        var loader = BattleMapLoader.Instance;
        if (loader != null && loader.GroundTilemap != null)
        {
            _tilemap = loader.GroundTilemap;
        }
        else
        {
            _tilemap = FindObjectOfType<Tilemap>();
        }

        if (_tilemap != null)
        {
            _tilemap.CompressBounds();
            BoundsInt bounds = _tilemap.cellBounds;
            Vector3 min = _tilemap.CellToWorld(bounds.min);
            Vector3 max = _tilemap.CellToWorld(bounds.max);
            _mapWorldMin = new Vector2(min.x, min.y);
            _mapWorldSize = new Vector2(max.x - min.x, max.y - min.y);
        }
    }

    private void BuildUI()
    {
        Debug.Log($"[MiniMap] BuildUI: panelSize={panelSize} mapResolution={mapResolution}");

        // 找 Canvas
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        // 面板
        var panelGo = new GameObject("MiniMap", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvas != null ? canvas.transform : transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.02f, 0.02f);
        panelRt.anchorMax = new Vector2(0.02f, 0.02f);
        panelRt.pivot = Vector2.zero;
        panelRt.sizeDelta = new Vector2(panelSize, panelSize);
        var panelBg = panelGo.GetComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.6f);

        // 地图图
        var imgGo = new GameObject("MapView", typeof(RectTransform), typeof(RawImage));
        imgGo.transform.SetParent(panelGo.transform, false);
        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(1, 1);
        imgRt.offsetMax = new Vector2(-1, -1);
        mapImage = imgGo.GetComponent<RawImage>();

        // 玩家红点
        var dotGo = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(panelGo.transform, false);
        playerDot = dotGo.GetComponent<RectTransform>();
        playerDot.sizeDelta = new Vector2(10, 10);
        playerDot.anchorMin = Vector2.zero;
        playerDot.anchorMax = Vector2.zero;
        var dotImg = dotGo.GetComponent<Image>();
        dotImg.color = Color.red;
        dotImg.raycastTarget = false;
    }

    private void SetupCamera()
    {
        if (_tilemap == null) return;

        Debug.Log($"[MiniMap] tilemap={_tilemap.name} bounds={_tilemap.cellBounds} worldMin={_mapWorldMin} worldSize={_mapWorldSize}");

        // 删旧相机（如果 Inspector 改参数后重建）
        var oldCam = GameObject.Find("MiniMapCamera");
        if (oldCam != null) Destroy(oldCam);

        var camGo = new GameObject("MiniMapCamera");
        miniCamera = camGo.AddComponent<Camera>();
        miniCamera.orthographic = true;
        miniCamera.orthographicSize = Mathf.Max(_mapWorldSize.x, _mapWorldSize.y) * 0.55f;
        camGo.transform.position = new Vector3(
            _mapWorldMin.x + _mapWorldSize.x * 0.5f,
            _mapWorldMin.y + _mapWorldSize.y * 0.5f,
            -50f);
        camGo.transform.rotation = Quaternion.identity;

        miniCamera.cullingMask = cullingMask;
        miniCamera.clearFlags = CameraClearFlags.SolidColor;
        miniCamera.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        miniCamera.depth = -100;
        miniCamera.enabled = false;

        _rt = new RenderTexture(mapResolution, mapResolution, 16, RenderTextureFormat.ARGB32);
        _rt.filterMode = FilterMode.Point;
        _rt.Create();

        if (mapImage != null)
            mapImage.texture = _rt;

        // 首帧渲染
        RenderMiniMap();
    }

    private void RenderMiniMap()
    {
        if (miniCamera == null || _rt == null) return;
        var prevTarget = miniCamera.targetTexture;
        miniCamera.targetTexture = _rt;
        miniCamera.Render();
        miniCamera.targetTexture = prevTarget;
    }

    private void LateUpdate()
    {
        if (playerDot == null || _mapRt == null || _tilemap == null || player == null)
            return;

        // 更新玩家红点位置
        Vector2 worldPos = player.position;
        float u = Mathf.Clamp01((worldPos.x - _mapWorldMin.x) / _mapWorldSize.x);
        float v = Mathf.Clamp01((worldPos.y - _mapWorldMin.y) / _mapWorldSize.y);

        float panelW = _mapRt.rect.width;
        float panelH = _mapRt.rect.height;
        playerDot.anchoredPosition = new Vector2(u * panelW, v * panelH);

        // 每 N 帧更新一次小地图
        _frameCount++;
        if (_frameCount >= _frameSkip)
        {
            _frameCount = 0;
            RenderMiniMap();
        }
    }

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
        }
        if (miniCamera != null)
            Destroy(miniCamera.gameObject);
    }
}
