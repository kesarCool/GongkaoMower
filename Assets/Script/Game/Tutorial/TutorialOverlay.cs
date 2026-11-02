using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space presentation with a rectangular aperture matching its raycast aperture.</summary>
public sealed class TutorialOverlay : MonoBehaviour
{
    RectTransform root, dialog, skipRect, actionRect, hand, trail;
    TMP_Text heading, body, counter, actionText;
    TutorialSpotlight spotlight;
    RectTransform[] targets;
    RectTransform pointerTarget;
    Transform movementSubject;
    Camera movementCamera;
    bool movement;
    float shownAt;
    readonly Vector3[] corners = new Vector3[4];
    public bool IsVisible => gameObject.activeSelf;
    public RectTransform Dialog => dialog;
    public RectTransform SkipButtonRect => skipRect;
    public RectTransform[] Targets => targets;
    public RectTransform PointerTarget => pointerTarget;
    public RectTransform MovementTrail => trail;
    public Rect HighlightRect => spotlight.Hole;

    static readonly Color Ink = new Color32(73, 54, 39, 255);
    static readonly Color Cream = new Color32(255, 248, 225, 255);
    static readonly Color Gold = new Color32(255, 199, 72, 255);

    public static TutorialOverlay Create(Transform parent, Action skip, Action next)
    {
        var go = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = .5f;
        var view = go.AddComponent<TutorialOverlay>();
        view.Build(skip, next);
        go.SetActive(false);
        return view;
    }

    void Build(Action skip, Action next)
    {
        root = (RectTransform)transform;
        BattleChineseFontRuntime.EnsureLoaded();
        var mask = Node(root, "Spotlight");
        Stretch(mask);
        spotlight = mask.gameObject.AddComponent<TutorialSpotlight>();

        trail = Node(root, "DragTrail");
        trail.sizeDelta = new Vector2(334, 130);
        trail.gameObject.AddComponent<TutorialGestureGraphic>().raycastTarget = false;

        hand = Node(root, "Hand");
        hand.sizeDelta = new Vector2(100, 140);
        hand.gameObject.AddComponent<TutorialHandGraphic>().raycastTarget = false;

        dialog = Node(root, "Dialogue");
        Shape(dialog, Cream, Ink, 4, 26).raycastTarget = true;
        var shadow = dialog.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .2f);
        shadow.effectDistance = new Vector2(0, -8);
        counter = Text(dialog, "Step", 24, new Color32(155, 104, 37, 255), true);
        heading = Text(dialog, "Title", 42, Ink, true);
        body = Text(dialog, "Message", 33, Ink, false);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.lineSpacing = 8;

        actionRect = Node(dialog, "Continue");
        actionRect.sizeDelta = new Vector2(250, 70);
        var actionFace = Shape(actionRect, Gold, Ink, 3, 18);
        var actionButton = actionRect.gameObject.AddComponent<Button>();
        actionButton.targetGraphic = actionFace;
        actionButton.onClick.AddListener(() => next());
        actionText = Text(actionRect, "Label", 30, Ink, true);
        Stretch((RectTransform)actionText.transform);
        actionText.alignment = TextAlignmentOptions.Center;

        // Kept outside the dialogue so it stays available in every guide state.
        skipRect = Node(root, "SkipTutorial");
        skipRect.sizeDelta = new Vector2(236, 76);
        var skipFace = Shape(skipRect, Cream, Ink, 3, 22);
        var skipButton = skipRect.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipFace;
        skipButton.onClick.AddListener(() => skip());
        var skipText = Text(skipRect, "Label", 29, Ink, true);
        skipText.text = "跳过引导";
        Stretch((RectTransform)skipText.transform);
        skipText.alignment = TextAlignmentOptions.Center;
    }

    public void Show(string title, string message, int step, RectTransform[] highlighted, bool moving, string action,
        RectTransform preferredPointerTarget = null, Transform movementAnchor = null)
    {
        targets = highlighted;
        pointerTarget = preferredPointerTarget;
        movementSubject = movementAnchor;
        movementCamera = moving ? Camera.main : null;
        movement = moving;
        heading.text = title;
        body.text = message;
        counter.text = step > 0 ? "入门指引  " + step.ToString("00") + " / 05" : "进阶指引 · 羁绊突破";
        actionRect.gameObject.SetActive(!string.IsNullOrEmpty(action));
        actionText.text = action ?? string.Empty;
        shownAt = Time.unscaledTime;
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Layout();
    }

    public void Hide() { targets = null; pointerTarget = null; movementSubject = null; movementCamera = null; gameObject.SetActive(false); }

    public bool ContainsControl(Vector2 screenPoint) =>
        RectTransformUtility.RectangleContainsScreenPoint(dialog, screenPoint, null) ||
        RectTransformUtility.RectangleContainsScreenPoint(skipRect, screenPoint, null);

    public static bool TargetsAvailable(RectTransform[] items)
    {
        if (items == null || items.Length == 0) return false;
        foreach (var item in items)
        {
            if (item == null || !item.gameObject.activeInHierarchy || item.rect.width <= 1 || item.rect.height <= 1) return false;
            var selectable = item.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return false;
            var canvas = item.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var world = new Vector3[4];
            item.GetWorldCorners(world);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var point in world)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, point);
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            if (!new Rect(0, 0, Screen.width, Screen.height).Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y))) return false;
        }
        return true;
    }

    void LateUpdate() { Layout(); }

    void Layout()
    {
        if (root == null || root.rect.width <= 0) return;
        Rect safeScreen = Screen.safeArea;
        if (safeScreen.width <= 0 || safeScreen.height <= 0) safeScreen = new Rect(0, 0, Screen.width, Screen.height);
        Vector2 min, max;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safeScreen.min, null, out min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, safeScreen.max, null, out max);
        Rect safe = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        float skipY = safe.yMax - 58;
        var capsule = PlatformMenuExclusion.MenuRect;
        if (capsule.width > 0)
        {
            Vector2 capsuleBottom;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                new Vector2(Screen.width * capsule.xMin, Screen.height * (1 - capsule.yMax)), null, out capsuleBottom);
            skipY = Mathf.Min(skipY, capsuleBottom.y - 58);
        }
        skipRect.anchoredPosition = new Vector2(safe.xMax - 142, skipY);

        Rect hole = Rect.zero;
        bool hasHole = TargetsAvailable(targets);
        if (hasHole)
        {
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue), high = new Vector2(float.MinValue, float.MinValue);
            foreach (var target in targets)
            {
                var bounds = LocalBounds(target);
                low = Vector2.Min(low, bounds.min);
                high = Vector2.Max(high, bounds.max);
            }
            hole = Rect.MinMaxRect(Mathf.Max(root.rect.xMin, low.x - 14), Mathf.Max(root.rect.yMin, low.y - 14),
                Mathf.Min(root.rect.xMax, high.x + 14), Mathf.Min(root.rect.yMax, high.y + 14));
        }
        spotlight.SetHole(hole, hasHole, !movement, movement ? .10f : .68f);

        float width = Mathf.Min(820, safe.width - 56);
        float height = actionRect.gameObject.activeSelf ? 430 : 350;
        // Scale down only on unusually short/landscape windows; preserve readable portrait typography.
        float scale = Mathf.Min(1, Mathf.Min(width / 620, (safe.height - 180) / height));
        scale = Mathf.Max(.25f, scale);
        float designWidth = width / scale;
        dialog.localScale = Vector3.one * scale;
        dialog.sizeDelta = new Vector2(designWidth, height);
        float half = height * scale * .5f;
        float topLimit = skipY - 65;
        float y = movement ? topLimit - half - 34 : safe.center.y;
        if (hasHole)
        {
            float above = topLimit - hole.yMax, below = hole.yMin - safe.yMin;
            y = above >= height * scale + 30 || above >= below
                ? hole.yMax + half + 28 : hole.yMin - half - 28;
        }
        y = Mathf.Clamp(y, safe.yMin + half + 24, Mathf.Max(safe.yMin + half + 24, topLimit - half));
        dialog.anchoredPosition = new Vector2(safe.center.x, y);

        PlaceText(counter, designWidth, -37, 36);
        PlaceText(heading, designWidth, -96, 64);
        PlaceText(body, designWidth, -215, 142);
        actionRect.anchorMin = actionRect.anchorMax = new Vector2(.5f, 1);
        actionRect.anchoredPosition = new Vector2(0, -height + 64);

        hand.gameObject.SetActive(movement || hasHole);
        trail.gameObject.SetActive(movement);
        float elapsed = Time.unscaledTime - shownAt;
        if (movement)
        {
            float originY = safe.yMin + safe.height * .23f;
            if (movementSubject != null && movementCamera != null)
            {
                Vector2 player;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                    movementCamera.WorldToScreenPoint(movementSubject.position), null, out player);
                originY = Mathf.Min(originY, player.y - 220);
            }
            Vector2 origin = new Vector2(safe.center.x, Mathf.Clamp(originY, safe.yMin + 210, safe.yMax - 210));
            trail.anchoredPosition = origin;
            hand.anchoredPosition = origin + TutorialGestureGraphic.PathPoint(elapsed * 2.1f, trail.rect.size - Vector2.one * 14);
        }
        else if (hasHole)
        {
            var pointerBounds = pointerTarget != null && pointerTarget.gameObject.activeInHierarchy ? LocalBounds(pointerTarget) : hole;
            hand.anchoredPosition = new Vector2(Mathf.Clamp(pointerBounds.center.x, safe.xMin + 30, safe.xMax - 100),
                Mathf.Clamp(pointerBounds.yMin + pointerBounds.height * .55f, safe.yMin + 150, safe.yMax - 20)) + new Vector2(0, Mathf.Sin(elapsed * 5) * 9);
        }
    }

    Rect LocalBounds(RectTransform target)
    {
        var canvas = target.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var world in corners)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                RectTransformUtility.WorldToScreenPoint(camera, world), null, out local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static RectTransform Node(Transform parent, string name)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        return rt;
    }
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
    static TutorialShapeGraphic Shape(RectTransform rt, Color fill, Color stroke, float border, float radius)
    {
        var graphic = rt.gameObject.AddComponent<TutorialShapeGraphic>();
        graphic.color = fill; graphic.stroke = stroke; graphic.border = border; graphic.radius = radius;
        return graphic;
    }
    static TMP_Text Text(Transform parent, string name, float size, Color color, bool bold)
    {
        var text = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        BattleChineseFontRuntime.ApplyToTMP(text);
        text.fontSize = size; text.color = color; text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Left; text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }
    static void PlaceText(TMP_Text text, float width, float y, float height)
    {
        var rt = (RectTransform)text.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(width - 88, height);
    }
}
