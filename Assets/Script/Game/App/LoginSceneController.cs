using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 登录场景：将「开始游戏」等按钮与进入 <c>Home</c> 场景关联。挂在 Login 场景任意物体上（建议 Canvas 或空物体）。
/// </summary>
[DisallowMultipleComponent]
public class LoginSceneController : MonoBehaviour
{
    [Header("导航")]
    [Tooltip("点击后进入 Home 场景；须在 Build Settings 中加入 Home 场景。")]
    [SerializeField] private Button startGameButton;

    [Tooltip("与 Build Settings 中场景名一致，默认 Home。")]
    [SerializeField] private string homeSceneName = "Home";

    [Tooltip("为 true 时使用 LoadSceneAsync，减少卡顿感。")]
    [SerializeField] private bool loadAsync = true;

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
