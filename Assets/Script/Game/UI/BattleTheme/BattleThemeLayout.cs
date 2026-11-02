using UnityEngine;

/// <summary>Fits the approved paper surface inside the safe area; HUD controls stay at the edges.</summary>
[DisallowMultipleComponent]
public sealed class BattleThemeLayout : MonoBehaviour
{
    public RectTransform panel;
    public float width = 952, height = 1570;
    public bool hud, pauseButtonOnly;
    public RectTransform top, minimap;
    private Vector2 lastSize;
    private Rect lastSafe;
    private Rect lastMenu;
    private void OnEnable() { Refresh(); }
    private void LateUpdate()
    {
        var root = transform as RectTransform;
        var bounds = pauseButtonOnly ? root.parent as RectTransform : root;
        if (bounds != null && (bounds.rect.size != lastSize || Screen.safeArea != lastSafe ||
            ((hud || pauseButtonOnly) && PlatformMenuExclusion.MenuRect != lastMenu))) Refresh();
    }
    public void Refresh()
    {
        var root = transform as RectTransform;
        var bounds = pauseButtonOnly ? root.parent as RectTransform : root;
        if (bounds == null || bounds.rect.width <= 0 || bounds.rect.height <= 0) return;
        lastSize = bounds.rect.size; lastSafe = Screen.safeArea;
        float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        float left = lastSafe.xMin / sw * bounds.rect.width;
        float right = (sw - lastSafe.xMax) / sw * bounds.rect.width;
        float upper = (sh - lastSafe.yMax) / sh * bounds.rect.height;
        float lower = lastSafe.yMin / sh * bounds.rect.height;
        float availableWidth = bounds.rect.width - left - right;
        float availableHeight = bounds.rect.height - upper - lower;
        float scale = Mathf.Min(1, availableWidth / 1080f);
        lastMenu = PlatformMenuExclusion.MenuRect;
        float pauseTop = upper + 210 * scale;
        if (lastMenu.width > 0)
            pauseTop = Mathf.Max(pauseTop, lastMenu.yMax * bounds.rect.height + 24 * scale);
        float hudShift = pauseTop - (upper + 210 * scale);
        if (pauseButtonOnly)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 1);
            root.anchoredPosition = new Vector2(-right - 40 * scale, -pauseTop);
            root.sizeDelta = new Vector2(92, 92); root.localScale = Vector3.one * scale;
        }
        else if (hud)
        {
            if (top != null)
            {
                top.anchorMin = top.anchorMax = top.pivot = new Vector2(.5f, 1);
                top.anchoredPosition = new Vector2((left - right) / 2, -upper - hudShift);
                top.localScale = Vector3.one * scale;
                FitHeaderBeforeMenu(bounds.rect.width, left, right, scale);
                var gradient = top.Find("TopGradient") as RectTransform;
                if (gradient != null && scale > 0)
                {
                    gradient.anchoredPosition = new Vector2(gradient.anchoredPosition.x, hudShift / scale);
                    gradient.sizeDelta = new Vector2(gradient.sizeDelta.x, 390 + hudShift / scale);
                }
            }
            if (minimap != null)
            {
                minimap.anchorMin = minimap.anchorMax = minimap.pivot = Vector2.zero;
                minimap.anchoredPosition = new Vector2(left + 40 * scale, lower + 54 * scale);
                minimap.localScale = Vector3.one * scale;
            }
        }
        else if (panel != null)
        {
            scale = Mathf.Max(.1f, Mathf.Min(scale, (availableHeight - 96) / height));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = new Vector2((left - right) / 2, (lower - upper) / 2);
            panel.sizeDelta = new Vector2(width, height); panel.localScale = Vector3.one * scale;
        }
    }

    private void FitHeaderBeforeMenu(float boundsWidth, float left, float right, float scale)
    {
        float timerX = 530;
        if (lastMenu.width > 0 && scale > 0)
        {
            float topLeft = boundsWidth / 2 + (left - right) / 2 - 540 * scale;
            float menuLeft = (lastMenu.xMin * boundsWidth - topLeft) / scale;
            timerX = Mathf.Clamp(menuLeft - 24 - 226, 320, 530);
        }
        MoveX(top.Find("TimerFace") as RectTransform, timerX);
        MoveX(top.Find("TimerIcon") as RectTransform, timerX + 18);
        MoveX(top.Find("Timer") as RectTransform, timerX + 56);
        var level = top.Find("Level") as RectTransform;
        if (level != null) level.sizeDelta = new Vector2(Mathf.Min(465, timerX - 64), level.sizeDelta.y);
    }

    private static void MoveX(RectTransform target, float x)
    {
        if (target != null) target.anchoredPosition = new Vector2(x, target.anchoredPosition.y);
    }
}
