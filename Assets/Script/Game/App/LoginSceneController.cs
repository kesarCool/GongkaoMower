using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 登录场景：所有玩家统一进入 Home，再由大厅的新手引导引导开始挑战。
/// </summary>
[DisallowMultipleComponent]
public class LoginSceneController : MonoBehaviour
{
    [Header("导航")]
    [Tooltip("点击后进入 Home 场景，新玩家与老玩家使用相同入口。")]
    [SerializeField] private Button startGameButton;

    [Header("版本号")]
    [Tooltip("登录页版本号显示（TMP 或 Text），运行时自动填入 Application.version。")]
    [SerializeField] private TMPro.TextMeshProUGUI versionText;

    [Tooltip("Home 场景名，须在 Build Settings 中。")]
    [SerializeField] private string homeSceneName = "Home";

    [Tooltip("为 true 时使用 LoadSceneAsync。")]
    [SerializeField] private bool loadAsync = true;

    private void Start()
    {
        if (versionText != null)
            versionText.text = $"版本号：v{Application.version}";
    }

    private void OnEnable()
    {
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
    }

    private void OnDisable()
    {
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
    }

    private void OnStartGameClicked()
    {
        UiClickSound.Play();
        PlayerProfileService.Instance.LoadOrCreate();

        GoToHome();
    }

    /// <summary>
    /// 所有玩家进入 Home 大厅，自己选关或通过新手引导点开始。
    /// </summary>
    private void GoToHome()
    {
        if (string.IsNullOrWhiteSpace(homeSceneName))
        {
            GameErrorPresenter.Show(GameErrorCodes.LoginSceneNameEmpty);
            return;
        }

        if (loadAsync)
            SceneManager.LoadSceneAsync(homeSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(homeSceneName, LoadSceneMode.Single);
    }
}
