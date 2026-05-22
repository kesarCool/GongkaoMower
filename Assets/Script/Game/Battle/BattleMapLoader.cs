using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 局内按 <see cref="ChapterLevel.mapPath"/> 从 Resources 动态实例化 Tilemap Prefab；
/// 找不到资源时回退 <see cref="ChapterLevelCatalog.DefaultMapResourcesPath"/>（TileMap101）。
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class BattleMapLoader : MonoBehaviour
{
    public const string GroundTilemapObjectName = "Ground";

    [Tooltip("留空则使用本物体 Transform 作为地图父节点。")]
    [SerializeField] private Transform mapRoot;

    private GameObject _mapInstance;
    private Tilemap _groundTilemap;
    private static bool _sceneHookInstalled;

    public static BattleMapLoader Instance { get; private set; }
    public Tilemap GroundTilemap => _groundTilemap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        if (_sceneHookInstalled) return;
        _sceneHookInstalled = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsBattleScene(scene.name)) return;
        if (Object.FindObjectOfType<BattleMapLoader>(true) != null) return;

        var go = new GameObject(nameof(BattleMapLoader));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<BattleMapLoader>();
    }

    private static bool IsBattleScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (sceneName == "Game") return true;
        return sceneName == GameObjectPool.BattleSceneNameForPoolClear;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (mapRoot == null)
            mapRoot = transform;

        LoadMapForCurrentLevel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        UnloadMap();
    }

    public void LoadMapForCurrentLevel()
    {
        UnloadMap();

        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        int levelId = BattleLevelContext.LevelId;
        string requestedPath = ChapterLevelCatalog.ResolveMapResourcesPath(levelId);
        GameObject prefab = LoadMapPrefab(requestedPath, out string loadedPath);

        if (prefab == null)
        {
            Debug.LogError(
                $"[BattleMapLoader] 无法加载地图：levelId={levelId}，请求={requestedPath}，默认={ChapterLevelCatalog.DefaultMapResourcesPath}。" +
                "请确认 Prefab 在 Assets/Resources/Map/ 下。");
            return;
        }

        _mapInstance = Instantiate(prefab, mapRoot);
        _mapInstance.name = prefab.name;
        _mapInstance.transform.localPosition = Vector3.zero;
        _mapInstance.transform.localRotation = Quaternion.identity;
        _mapInstance.transform.localScale = Vector3.one;

        _groundTilemap = FindGroundTilemap(_mapInstance.transform);
        WireConsumers(_groundTilemap);

        if (loadedPath != requestedPath)
        {
            Debug.LogWarning(
                $"[BattleMapLoader] levelId={levelId} mapPath={requestedPath} 未找到，已回退 {loadedPath}。");
        }
        else
        {
            Debug.Log(
                $"[BattleMapLoader] levelId={levelId} 已加载地图 {loadedPath}，Ground={(_groundTilemap != null ? _groundTilemap.name : "null")}");
        }
    }

    private static GameObject LoadMapPrefab(string resourcesPath, out string loadedPath)
    {
        loadedPath = ChapterLevelCatalog.NormalizeMapResourcesPath(resourcesPath);
        GameObject prefab = Resources.Load<GameObject>(loadedPath);
        if (prefab != null) return prefab;

        if (loadedPath == ChapterLevelCatalog.DefaultMapResourcesPath)
            return null;

        loadedPath = ChapterLevelCatalog.DefaultMapResourcesPath;
        return Resources.Load<GameObject>(loadedPath);
    }

    private static Tilemap FindGroundTilemap(Transform mapRootTransform)
    {
        if (mapRootTransform == null) return null;

        var tilemaps = mapRootTransform.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm != null && tm.name == GroundTilemapObjectName)
                return tm;
        }

        return tilemaps.Length > 0 ? tilemaps[0] : null;
    }

    private static void WireConsumers(Tilemap ground)
    {
        if (ground == null)
        {
            Debug.LogWarning("[BattleMapLoader] 地图中未找到 Ground Tilemap，Spawner / 相机边界未绑定。");
            return;
        }

        var spawners = Object.FindObjectsOfType<SpawnerWaves>(true);
        for (int i = 0; i < spawners.Length; i++)
            spawners[i].mapBoundsTilemap = ground;

        var cameras = Object.FindObjectsOfType<CameraFollow2D>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].boundsTilemap = ground;
    }

    private void UnloadMap()
    {
        _groundTilemap = null;
        if (_mapInstance == null) return;

        Destroy(_mapInstance);
        _mapInstance = null;
    }
}
