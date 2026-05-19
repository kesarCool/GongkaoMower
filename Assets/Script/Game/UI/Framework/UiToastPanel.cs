using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>轻提示 Toast（Overlay，不暂停战斗）。由 <see cref="UIManager.ShowToast"/> 驱动。</summary>
[DisallowMultipleComponent]
public class UiToastPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float defaultDuration = 2.5f;

    private Coroutine _hideRoutine;

    public void Show(string message, float durationSeconds)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_hideRoutine != null)
            StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfter(Mathf.Max(0.5f, durationSeconds)));
    }

    public void HideImmediate()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
        gameObject.SetActive(false);
    }

    private IEnumerator HideAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
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
