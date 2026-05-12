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

        if (!string.IsNullOrWhiteSpace(loadSceneOnStart))
            SceneManager.LoadSceneAsync(loadSceneOnStart, LoadSceneMode.Single);
    }
}
