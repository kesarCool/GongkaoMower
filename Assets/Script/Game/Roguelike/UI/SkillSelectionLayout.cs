using UnityEngine;

/// <summary>V4 design coordinates, fitted uniformly inside the actual canvas and safe area.</summary>
public sealed class SkillSelectionLayout : MonoBehaviour
{
    public RectTransform designRoot;
    public RectTransform paper;
    public RectTransform refreshArea;
    public RectTransform hint;

    private float designHeight = 1920;
    private Rect lastSafeArea;
    private Vector2 lastRootSize;
    private Vector3 lastScale;

    public void SetContentState(int bondedRows, bool hasRefresh)
    {
        float bondHeight = bondedRows > 0 ? 144 + Mathf.Max(0, bondedRows - 1) * 84 : 0;
        float delta = bondHeight - 144 - (hasRefresh ? 0 : 152);
        if (paper != null) paper.sizeDelta = new Vector2(972, 1624 + delta);
        if (refreshArea != null)
        {
            refreshArea.anchoredPosition = new Vector2(260, -(1582 + bondHeight - 144));
            refreshArea.gameObject.SetActive(hasRefresh);
        }
        if (hint != null) hint.anchoredPosition = new Vector2(100, -(1794 + delta));
        designHeight = 1920 + Mathf.Max(0, bondHeight - 144);
        FitToSafeArea();
    }

    private void OnEnable() { FitToSafeArea(); }

    private void LateUpdate()
    {
        var root = transform as RectTransform;
        if (root != null && (root.rect.size != lastRootSize || Screen.safeArea != lastSafeArea || root.lossyScale != lastScale))
            FitToSafeArea();
    }

    public void FitToSafeArea()
    {
        var root = transform as RectTransform;
        if (root == null || designRoot == null || root.rect.width <= 0 || root.rect.height <= 0) return;
        var canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 min, max;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safe.min, camera, out min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safe.max, camera, out max);
        min = Vector2.Max(min, root.rect.min);
        max = Vector2.Min(max, root.rect.max);
        Vector2 size = max - min;
        if (size.x <= 0 || size.y <= 0) return;
        float scale = Mathf.Min(size.x / 1080, size.y / designHeight);
        designRoot.anchorMin = designRoot.anchorMax = new Vector2(0.5f, 0.5f);
        designRoot.pivot = new Vector2(0.5f, 0.5f);
        designRoot.sizeDelta = new Vector2(1080, designHeight);
        designRoot.anchoredPosition = (min + max) * 0.5f - root.rect.center;
        designRoot.localScale = new Vector3(scale, scale, 1);
        lastSafeArea = Screen.safeArea;
        lastRootSize = root.rect.size;
        lastScale = root.lossyScale;
    }
}
