using UnityEngine;

/// <summary>Keep the poster proportional and fit login controls inside the device safe area.</summary>
[DisallowMultipleComponent]
public sealed class LoginScreenLayout : MonoBehaviour
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform safeContent;
    [SerializeField] private RectTransform topContent;
    [SerializeField] private RectTransform bottomContent;
    [SerializeField] private RectTransform bottomVeil;
    private Rect previousSafe;
    private Vector2 previousSize;
    private Vector2Int previousScreen;

    private void OnEnable() { Apply(); }
    private void LateUpdate()
    {
        var size = ((RectTransform)transform).rect.size;
        if (size != previousSize || Screen.safeArea != previousSafe ||
            previousScreen != new Vector2Int(Screen.width, Screen.height)) Apply();
    }

    public void Apply()
    {
        Rect safe = Screen.safeArea;
        Rect normalized = new Rect(0, 0, 1, 1);
        if (Screen.width > 0 && Screen.height > 0 && safe.width > 0 && safe.height > 0)
            normalized = new Rect(safe.x / Screen.width, safe.y / Screen.height,
                safe.width / Screen.width, safe.height / Screen.height);
        ApplyLayout(normalized);
        previousSafe = safe;
        previousScreen = new Vector2Int(Screen.width, Screen.height);
    }

    // Normalized coordinates also allow editor previews of devices with a notch/home indicator.
    public void ApplyLayout(Rect normalizedSafeArea)
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x <= 0 || size.y <= 0 || !background || !safeContent || !topContent || !bottomContent) return;
        float cover = Mathf.Max(size.x / 1080f, size.y / 1920f);
        background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
        background.anchoredPosition = Vector2.zero;
        background.sizeDelta = new Vector2(1080, 1920) * cover;
        background.localScale = Vector3.one;

        safeContent.anchorMin = normalizedSafeArea.min;
        safeContent.anchorMax = normalizedSafeArea.max;
        safeContent.offsetMin = safeContent.offsetMax = Vector2.zero;
        float fit = Mathf.Min(size.x * normalizedSafeArea.width / 1080f,
            size.y * normalizedSafeArea.height / 1920f);
        topContent.anchorMin = topContent.anchorMax = topContent.pivot = new Vector2(.5f, 1);
        topContent.anchoredPosition = Vector2.zero;
        topContent.sizeDelta = new Vector2(1080, 520);
        topContent.localScale = Vector3.one * fit;
        bottomContent.anchorMin = bottomContent.anchorMax = bottomContent.pivot = new Vector2(.5f, 0);
        bottomContent.anchoredPosition = Vector2.zero;
        bottomContent.sizeDelta = new Vector2(1080, 390);
        bottomContent.localScale = Vector3.one * fit;
        if (bottomVeil)
        {
            bottomVeil.anchorMin = new Vector2(0, 0);
            bottomVeil.anchorMax = new Vector2(1, 0);
            bottomVeil.pivot = new Vector2(.5f, 0);
            bottomVeil.anchoredPosition = Vector2.zero;
            bottomVeil.sizeDelta = new Vector2(0, size.y * normalizedSafeArea.yMin + 390 * fit);
        }
        previousSize = size;
    }
}
