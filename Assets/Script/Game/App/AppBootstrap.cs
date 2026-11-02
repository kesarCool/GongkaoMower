using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 壳入口：挂在 Boot 场景的 <c>AppRoot</c> 上。<c>Awake</c> 时对根物体执行 <see cref="Object.DontDestroyOnLoad"/>，
/// 便于跨场景保留后续挂载的会话、流程控制器等（与 <c>TableManager</c> 单例生命周期对齐时再扩展）。
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class AppBootstrap : MonoBehaviour
{
    public const int TargetFrameRate = 30;
    public const int EditorTargetFrameRate = 60;
    public const string PerformanceRevision = "ios-login-entry-20261028-r2";
    [Tooltip("Boot 启动后异步加载的场景名（须已在 Build Settings 中）；留空则仅驻留 AppRoot，由其它逻辑切场景。")]
    [SerializeField] private string loadSceneOnStart = "Login";

    private void Awake()
    {
        var root = transform;
        if (root.parent != null)
            root.SetParent(null);

        DontDestroyOnLoad(root.gameObject);

        // The WeChat converter maps Unity's loop timing to wx frame scheduling.
        // Keep Unity's target authoritative so loop setup/resume cannot restore
        // an unrelated 60 FPS target after a one-off wx API call.
        ApplyFrameRateSettings();
#if UNITY_WEBGL && !UNITY_EDITOR
        // Establish the SDK receiver once before features register native callbacks.
        WeChatWASM.WX.InitSDK(code =>
        {
            if (code == 200)
                ApplyFrameRateSettings();
            else
                Debug.LogWarning($"[WeChatSDK] Initialization failed: {code}");
        });
#endif

        Time.fixedDeltaTime = 1f / 30f;
        Time.maximumDeltaTime = 1f / 15f;

        PlayerProfileService.Instance.LoadOrCreate();

        if (!string.IsNullOrWhiteSpace(loadSceneOnStart))
            SceneManager.LoadSceneAsync(loadSceneOnStart, LoadSceneMode.Single);
    }

    public static void ApplyFrameRateSettings()
    {
        QualitySettings.vSyncCount = 0;
        // Unity owns the requested rate. The exported scheduler separately
        // preserves its logical timing value across platform pause/resume.
#if UNITY_EDITOR
        // Local preview is independent of the player's mobile CPU budget.
        Application.targetFrameRate = EditorTargetFrameRate;
#else
        Application.targetFrameRate = TargetFrameRate;
#endif
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) ApplyFrameRateSettings();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) ApplyFrameRateSettings();
    }
}
