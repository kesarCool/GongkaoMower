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
    [Tooltip("Boot 启动后异步加载的场景名（须已在 Build Settings 中）；留空则仅驻留 AppRoot，由其它逻辑切场景。")]
    [SerializeField] private string loadSceneOnStart = "Login";

    private void Awake()
    {
        var root = transform;
        if (root.parent != null)
            root.SetParent(null);

        DontDestroyOnLoad(root.gameObject);

#if !UNITY_EDITOR
        // 非 Editor 下关 Debug.Log / LogWarning，保留 LogError 用于异常定位
        Debug.unityLogger.logEnabled = false;
#endif

        // 全局性能设置
        //   Editor/Standalone: vSync=1 + targetFrameRate=30
        //   WebGL/微信小游戏: vSyncCount/targetFrameRate 在微信环境均无效，
        //     帧率由 WX.SetPreferredFramesPerSecond 通过 .jslib 桥直接调 wx API
        QualitySettings.vSyncCount = 1;
#if !UNITY_WEBGL || UNITY_EDITOR
        Application.targetFrameRate = 30;
#else
        WeChatWASM.WX.SetPreferredFramesPerSecond(30);
#endif

        Time.fixedDeltaTime = 1f / 30f;
        Time.maximumDeltaTime = 1f / 15f;

        PlayerProfileService.Instance.LoadOrCreate();

        if (!string.IsNullOrWhiteSpace(loadSceneOnStart))
            SceneManager.LoadSceneAsync(loadSceneOnStart, LoadSceneMode.Single);
    }
}
