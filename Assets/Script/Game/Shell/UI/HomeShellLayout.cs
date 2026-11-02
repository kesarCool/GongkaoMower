using UnityEngine;

/// <summary>Apply the device safe area once to the Home shell; keep page content between shared bars.</summary>
public sealed class HomeShellLayout : MonoBehaviour
{
    private Rect previousSafe;
    private Rect previousMenu;
    private Vector2Int previousScreen;
    private Vector2 previousSize;
    private RectTransform header, content, gradient;
    private RectTransform[] headerEntries;
    private float baseHeaderHeight;
    private readonly float[] entryX = { 40, 175.82f, 442.58f };
    private readonly float[] entryHeight = { 113.14f, 82.56f, 82.56f };
    private void OnEnable() { Apply(); }
    private void LateUpdate()
    {
        if (previousSafe != Screen.safeArea || previousMenu != PlatformMenuExclusion.MenuRect ||
            previousScreen != new Vector2Int(Screen.width, Screen.height) || previousSize != ((RectTransform)transform).rect.size) Apply();
    }
    public void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        var rt = (RectTransform)transform;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var menu = PlatformMenuExclusion.MenuRect;
        if (header == null)
        {
            header = transform.Find("TopBar") as RectTransform;
            content = transform.Find("viewContainer") as RectTransform;
            if (header != null)
            {
                baseHeaderHeight = header.sizeDelta.y;
                gradient = header.Find("HeaderGradient") as RectTransform;
                headerEntries = new[] { header.Find("ButtonSetting") as RectTransform,
                    header.Find("gold") as RectTransform, header.Find("gem") as RectTransform };
            }
        }
        if (header != null && content != null)
        {
            float headerHeight = baseHeaderHeight;
            float entryScale = 1;
            if (menu.width > 0)
            {
                float fullHeight = rt.rect.height / Mathf.Max(.001f, safe.height / Screen.height);
                float safeTop = 1 - safe.yMax / Screen.height;
                headerHeight = Mathf.Max(headerHeight, (menu.yMax - safeTop) * fullHeight + 24);
                float fullWidth = rt.rect.width / Mathf.Max(.001f, safe.width / Screen.width);
                float menuLeft = (menu.xMin - safe.xMin / Screen.width) * fullWidth;
                entryScale = Mathf.Clamp((menuLeft - 24 - 40) / (686.66f - 40), .5f, 1);
            }
            header.sizeDelta = new Vector2(header.sizeDelta.x, headerHeight);
            content.offsetMax = new Vector2(content.offsetMax.x, -headerHeight);
            if (gradient != null) gradient.sizeDelta = new Vector2(gradient.sizeDelta.x, Mathf.Max(226, headerHeight + 5.2f));
            for (int i = 0; headerEntries != null && i < headerEntries.Length; i++)
            {
                var entry = headerEntries[i]; if (entry == null) continue;
                entry.localScale = Vector3.one * entryScale;
                entry.anchoredPosition = new Vector2(40 + (entryX[i] - 40) * entryScale,
                    -(113.28f + entryHeight[i] * (1 - entryScale) / 2));
            }
        }
        previousSafe = Screen.safeArea; previousScreen = new Vector2Int(Screen.width, Screen.height);
        previousMenu = menu; previousSize = rt.rect.size;
    }
}
