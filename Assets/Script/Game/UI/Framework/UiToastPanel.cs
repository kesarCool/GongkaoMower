using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>轻提示 Toast（Overlay，不暂停战斗）。淡入 + 上飘 + 淡出。</summary>
[DisallowMultipleComponent]
public class UiToastPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float defaultDuration = 1f;
    [SerializeField] private float fadeInTime = 0.15f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private float floatSpeed = 30f;

    private Coroutine _hideRoutine;
    private CanvasGroup _canvasGroup;
    private RectTransform _rt;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _rt = GetComponent<RectTransform>();
    }

    public void Show(string message, float durationSeconds)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // 重置位置
        if (_rt != null) _rt.anchoredPosition = Vector2.zero;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(ToastRoutine(Mathf.Max(0.5f, durationSeconds)));
    }

    public void HideImmediate()
    {
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
        gameObject.SetActive(false);
    }

    private IEnumerator ToastRoutine(float totalDuration)
    {
        float holdTime = totalDuration - fadeInTime - fadeOutTime;
        if (holdTime < 0f) holdTime = 0f;

        // 淡入
        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(t / fadeInTime);
            if (_rt != null) _rt.anchoredPosition = Vector2.up * (floatSpeed * t);
            yield return null;
        }

        // 保持（不动）
        if (holdTime > 0f)
        {
            if (_rt != null) _rt.anchoredPosition = Vector2.up * floatSpeed * fadeInTime;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(holdTime);
        }

        // 淡出 + 上飘
        t = 0f;
        float yBase = _rt != null ? _rt.anchoredPosition.y : 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutTime);
            if (_rt != null) _rt.anchoredPosition = new Vector2(_rt.anchoredPosition.x, yBase + floatSpeed * t);
            yield return null;
        }

        _hideRoutine = null;
        gameObject.SetActive(false);
    }

    private void Reset()
    {
        if (messageText == null)
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);
    }
}
