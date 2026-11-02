using UnityEngine;

/// <summary>Installs frame control even when the player starts directly in Login.</summary>
[DisallowMultipleComponent]
public sealed class AppFrameRateController : MonoBehaviour
{
    private static AppFrameRateController instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance == null)
            instance = FindObjectOfType<AppFrameRateController>();
        if (instance == null)
            instance = new GameObject("AppFrameRateController").AddComponent<AppFrameRateController>();
        Apply("startup");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) Apply("focus-return");
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) Apply("resume");
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static void Apply(string source)
    {
        AppBootstrap.ApplyFrameRateSettings();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[FrameControl] revision={AppBootstrap.PerformanceRevision} source={source} targetFps={Application.targetFrameRate} vsync={QualitySettings.vSyncCount}");
#endif
    }
}
