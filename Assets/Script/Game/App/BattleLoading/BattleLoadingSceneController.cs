using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 进局前全屏 Loading 场景：微信分包占位 →（可选）<see cref="TableManager.Init"/> → 异步进 <see cref="nextSceneName"/>。
/// 挂在本场景任意物体上；与 <see cref="BattleFlowLauncher"/> 配合使用。
/// </summary>
[DisallowMultipleComponent]
public class BattleLoadingSceneController : MonoBehaviour
{
    [Tooltip("分包名列表，须与微信导出 game.json 一致；留空则跳过「分包」步骤。")]
    [SerializeField] private string[] subpackageNames = { "battle" };

    [Tooltip("编辑器下模拟分包耗时（秒），0 表示不等待。")]
    [SerializeField] private float editorSimulatedSubpackageDelay = 0.25f;

    [Tooltip("局内场景名，须在 Build Settings 中。")]
    [SerializeField] private string nextSceneName = "Game";

    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Button retryButton;
    [SerializeField] private BattleLoadingPresentation presentation;

    [Tooltip("表若在首包已 Init，此处仍为幂等；若表在分包内，应在分包成功后再勾选或在此处 Init。")]
    [SerializeField] private bool initTableManagerInLoading = true;

    private Coroutine _run;
    private AsyncOperation _sceneLoad;
    private System.IDisposable _battlePause;
    private float _progress;
    private static BattleLoadingSceneController _instance;
    public static bool IsTransitionVisible => _instance != null;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_sceneLoad != null && scene.name == nextSceneName && _battlePause == null && UIManager.Instance != null)
            _battlePause = UIManager.Instance.AcquirePauseLock();
    }

    private void Start()
    {
        // 兜底：上局可能残留非 1 的 timeScale，重置后再开始加载流程
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextSceneName))
            GameObjectPool.BattleSceneNameForPoolClear = nextSceneName;

        EnsureMinimalUi();
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
            retryButton.gameObject.SetActive(false);
        }

        BeginFlow();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _battlePause?.Dispose(); _battlePause = null;
        if (_sceneLoad != null && !_sceneLoad.isDone) _sceneLoad.allowSceneActivation = true;
        if (_instance == this) _instance = null;
        if (retryButton != null)
            retryButton.onClick.RemoveListener(OnRetryClicked);
    }

    private void OnRetryClicked()
    {
        UiClickSound.Play();
        if (retryButton != null)
            retryButton.gameObject.SetActive(false);
        BeginFlow();
    }

    private void BeginFlow()
    {
        if (!string.IsNullOrEmpty(nextSceneName)) GameObjectPool.BattleSceneNameForPoolClear = nextSceneName;
        if (_run != null)
            StopCoroutine(_run);
        _battlePause?.Dispose(); _battlePause = null;
        _progress = 0;
        if (presentation != null) presentation.ResetProgress();
        _run = StartCoroutine(RunFlow());
    }

    private IEnumerator RunFlow()
    {
        SetStatus("正在整理行囊…");
        SetProgress(0f);
        // Let the fully authored page render before any synchronous data work.
        yield return null;

        string failMessage = null;
        yield return WeChatSubpackagePlaceholder.LoadSubpackagesRoutine(
            subpackageNames,
            editorSimulatedSubpackageDelay,
            p => SetProgress(p * .28f),
            () => { },
            err => { failMessage = err; });

        if (!string.IsNullOrEmpty(failMessage))
        {
            ShowFailure("资源准备失败，请重试。", failMessage);
            yield break;
        }

        SetProgress(.28f);
        SetStatus("正在准备关卡…");
        yield return null;

        if (initTableManagerInLoading && TableManager.Instance != null)
            TableManager.Instance.Init();

        BattleChineseFontRuntime.EnsureLoaded();
        if (presentation != null) presentation.RefreshSelectedContent();
        SetProgress(.36f);

        SetStatus("正在准备战斗…");
        yield return null;

        var audio = AudioService.Ensure();
        if (!audio.IsGroupLoaded(AudioLoadGroup.Battle))
        {
            yield return audio.LoadGroupAsync(AudioLoadGroup.Battle);
        }

        SetProgress(.46f);
        SetStatus("正在布置战场…");

        AsyncOperation op = null;
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (op == null)
        {
            ShowFailure("关卡准备失败，请重试。", "Scene not available: " + nextSceneName);
            yield break;
        }

        _sceneLoad = op;
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            SetProgress(.46f + .48f * Mathf.Clamp01(op.progress / .9f));
            yield return null;
        }

        SetProgress(.94f);
        SetStatus("马上开始…");
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        // Keep the page opaque across activation and the new scene's Start callbacks.
        yield return null;
        yield return null;
        if (BattleMapLoader.Instance == null || BattleMapLoader.Instance.GroundTilemap == null || FindObjectOfType<PlayerController>() == null)
        {
            ShowFailure("战场还未准备好，请重试。", "Game activated without its map or player.");
            yield break;
        }

        SensitiveWordFilter.Instance.LoadLocalBlacklist();
        SensitiveWordFilter.Instance.LoadRemoteCache();
        // This host survives after the loading page has been destroyed.
        audio.StartCoroutine(SensitiveWordFilter.Instance.BackgroundCheckAll(null));
        SetProgress(1f);
        if (presentation != null) yield return presentation.RevealBattle();
        _battlePause?.Dispose(); _battlePause = null;
        _run = null;
        Destroy(gameObject);
    }

    private void ShowFailure(string message, string detail)
    {
        GameLog.Warning("[BattleLoading] " + detail);
        SetStatus(message);
        if (presentation != null) presentation.ShowError(message);
        else if (retryButton != null) retryButton.gameObject.SetActive(true);
        _run = null;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        GameLog.Info("[BattleLoading] " + msg);
    }

    private void SetProgress(float p)
    {
        _progress = Mathf.Max(_progress, Mathf.Clamp01(p));
        if (presentation != null) presentation.Report(_progress);
        else if (progressSlider != null) progressSlider.value = _progress;
    }

    private void EnsureMinimalUi()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        if (statusText != null && progressSlider != null && retryButton != null)
            return;

        var canvasGo = new GameObject("BattleLoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var bg = panelGo.GetComponent<Image>();
        bg.color = new Color(1, .95f, .83f, 1);

        if (statusText == null)
        {
            var textGo = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            var tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.1f, 0.55f);
            tr.anchorMax = new Vector2(0.9f, 0.75f);
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            statusText = textGo.GetComponent<TextMeshProUGUI>();
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 28;
            statusText.color = new Color(.29f, .21f, .15f, 1);
            statusText.text = "正在准备关卡…";
            BattleChineseFontRuntime.ApplyToTMP(statusText);
        }

        if (progressSlider == null)
        {
            var sliderGo = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(panelGo.transform, false);
            var str = sliderGo.GetComponent<RectTransform>();
            str.anchorMin = new Vector2(0.15f, 0.42f);
            str.anchorMax = new Vector2(0.85f, 0.48f);
            str.offsetMin = str.offsetMax = Vector2.zero;
            progressSlider = sliderGo.GetComponent<Slider>();
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
            progressSlider.interactable = false;
            var trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(sliderGo.transform, false);
            var trackRt = trackGo.GetComponent<RectTransform>(); trackRt.anchorMin = Vector2.zero; trackRt.anchorMax = Vector2.one; trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;
            trackGo.GetComponent<Image>().color = new Color(.3f, .23f, .16f, 1);
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fillGo.transform.SetParent(trackGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>(); fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(5, 5); fillRt.offsetMax = new Vector2(-5, -5);
            fillGo.GetComponent<Image>().color = new Color(1, .72f, .26f, 1); progressSlider.fillRect = fillRt;
        }

        if (retryButton == null)
        {
            var btnGo = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panelGo.transform, false);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.35f, 0.28f);
            brt.anchorMax = new Vector2(0.65f, 0.36f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            btnGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.75f, 1f);
            retryButton = btnGo.GetComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(btnGo.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var lt = labelGo.GetComponent<TextMeshProUGUI>();
            lt.alignment = TextAlignmentOptions.Center;
            lt.fontSize = 24;
            lt.color = Color.white;
            lt.text = "重试";
            BattleChineseFontRuntime.ApplyToTMP(lt);
        }
    }
}
