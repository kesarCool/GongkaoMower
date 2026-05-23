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

    [Tooltip("表若在首包已 Init，此处仍为幂等；若表在分包内，应在分包成功后再勾选或在此处 Init。")]
    [SerializeField] private bool initTableManagerInLoading = true;

    private Coroutine _run;

    private void Start()
    {
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
        if (_run != null)
            StopCoroutine(_run);
        _run = StartCoroutine(RunFlow());
    }

    private IEnumerator RunFlow()
    {
        SetStatus("加载分包…");
        SetProgress(0f);

        string failMessage = null;
        yield return WeChatSubpackagePlaceholder.LoadSubpackagesRoutine(
            subpackageNames,
            editorSimulatedSubpackageDelay,
            SetProgress,
            () => { },
            err => { failMessage = err; });

        if (!string.IsNullOrEmpty(failMessage))
        {
            SetStatus("分包失败：" + failMessage);
            SetProgress(0f);
            if (retryButton != null)
                retryButton.gameObject.SetActive(true);
            yield break;
        }

        SetProgress(1f);
        SetStatus("初始化数据…");

        if (initTableManagerInLoading && TableManager.Instance != null)
            TableManager.Instance.Init();

        BattleChineseFontRuntime.EnsureLoaded();

        SetStatus("加载音效…");
        SetProgress(0f);

        var audio = AudioService.Ensure();
        if (!audio.IsGroupLoaded(AudioLoadGroup.Battle))
        {
            yield return audio.LoadGroupAsync(AudioLoadGroup.Battle);
        }

        SetStatus("进入战斗…");
        SetProgress(0f);

        var op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (op == null)
        {
            SetStatus("无法加载场景：" + nextSceneName);
            if (retryButton != null)
                retryButton.gameObject.SetActive(true);
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(op.progress / 0.9f));
            yield return null;
        }

        SetProgress(1f);
        op.allowSceneActivation = true;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log("[BattleLoading] " + msg);
    }

    private void SetProgress(float p)
    {
        if (progressSlider != null)
            progressSlider.value = Mathf.Clamp01(p);
    }

    private void EnsureMinimalUi()
    {
        if (statusText != null && progressSlider != null && retryButton != null)
            return;

        var canvasGo = new GameObject("BattleLoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
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
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

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
            statusText.color = Color.white;
            statusText.text = "Loading…";
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
