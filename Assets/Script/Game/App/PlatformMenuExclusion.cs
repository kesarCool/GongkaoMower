using UnityEngine;

/// <summary>WeChat capsule bounds, normalized to the game window with a top-left origin.</summary>
public static class PlatformMenuExclusion
{
#if UNITY_EDITOR
    public static Rect? EditorMenuRectOverride { get; set; }
#endif
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
    private static Rect cached;
    private static Vector2Int previousScreen;
    private static float nextProbe;
#endif

    public static Rect MenuRect
    {
        get
        {
#if UNITY_EDITOR
            return EditorMenuRectOverride ?? Rect.zero;
#elif UNITY_WEBGL || WEIXINMINIGAME
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size != previousScreen || Time.realtimeSinceStartup >= nextProbe)
            {
                if (size != previousScreen) cached = Rect.zero;
                previousScreen = size;
                nextProbe = Time.realtimeSinceStartup + 1;
                try
                {
                    var menu = WeChatWASM.WX.GetMenuButtonBoundingClientRect();
                    var window = WeChatWASM.WX.GetWindowInfo();
                    Rect normalized;
                    if (TryNormalize(new Rect((float)menu.left, (float)menu.top, (float)menu.width, (float)menu.height),
                        new Vector2((float)window.windowWidth, (float)window.windowHeight), out normalized))
                        cached = normalized;
                }
                catch (System.Exception) { /* Retry after SDK/window initialization. */ }
                if (cached.width <= 0) cached = FallbackRect();
            }
            return cached;
#else
            return Rect.zero;
#endif
        }
    }

    // CSS/window coordinates must be normalized before converting to Unity's canvas pixels.
    public static bool TryNormalize(Rect menu, Vector2 windowSize, out Rect normalized)
    {
        normalized = Rect.zero;
        if (windowSize.x <= 0 || windowSize.y <= 0 || menu.width <= 0 || menu.height <= 0 ||
            menu.xMin < 0 || menu.yMin < 0 || menu.xMax > windowSize.x || menu.yMax > windowSize.y) return false;
        normalized = new Rect(menu.x / windowSize.x, menu.y / windowSize.y,
            menu.width / windowSize.x, menu.height / windowSize.y);
        return true;
    }

#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
    private static Rect FallbackRect()
    {
        float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        float dp = sw / 375f;
        float safeTop = Screen.safeArea.height > 0 ? sh - Screen.safeArea.yMax : 0;
        float top = Mathf.Max(52 * dp, safeTop + 8 * dp);
        return new Rect((sw - 98 * dp) / sw, top / sh, 90 * dp / sw, 32 * dp / sh);
    }
#endif
}
