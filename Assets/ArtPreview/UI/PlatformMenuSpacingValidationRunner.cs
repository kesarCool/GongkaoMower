#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PlatformMenuSpacingValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "PlatformMenuSpacing";
    protected override string ValidationNote => "Actual Home/Game scenes, simulated WeChat capsules and CSS window sizes. In-memory player profile. Native SDK geometry requires a fresh WeChat build/device check.";
    private bool previousBackground;
    private Canvas overlay;
    private RectTransform capsule;
    private void Awake() { previousBackground = Application.runInBackground; Application.runInBackground = true; }

    protected override IEnumerator RunChecks()
    {
        Rect normalized;
        Record("SDK CSS coordinates normalize independently of pixel density", PlatformMenuExclusion.TryNormalize(new Rect(278, 52, 87, 32), new Vector2(375, 812), out normalized) && Mathf.Abs(normalized.xMin - 278f / 375) < .00001f);
        Record("Invalid SDK dimensions rejected", !PlatformMenuExclusion.TryNormalize(new Rect(278, 52, 87, 32), Vector2.zero, out normalized));
        var shell = hub.GetComponent<HomeShellLayout>();
        PlatformMenuExclusion.EditorMenuRectOverride = null; shell.Apply(); Canvas.ForceUpdateCanvases();
        Record("Editor Home baseline preserved without platform capsule", Mathf.Abs(((RectTransform)shell.transform.Find("TopBar")).sizeDelta.y - 220.8f) < .1f);

        foreach (var screen in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400), new Vector2Int(360, 640) })
        {
            changeResolution(screen.x, screen.y); yield return null; yield return null;
            for (int variant = 0; variant < 2; variant++)
            {
                SetCapsule(variant); shell.Apply(); Canvas.ForceUpdateCanvases(); yield return null;
                var blocked = MenuScreenRect();
                var buttons = shell.GetComponentsInChildren<Button>(true).Where(b => b.isActiveAndEnabled && b.interactable);
                Record("Home " + screen + " variant " + variant + " clickable entrances avoid capsule", buttons.All(b => !VisibleRect((RectTransform)b.transform).Overlaps(blocked)));
                var header = shell.transform.Find("TopBar");
                Record("Home " + screen + " variant " + variant + " currency avoids capsule", new[] { "gold", "gem" }.All(n => !VisibleRect((RectTransform)header.Find(n)).Overlaps(blocked)));
                Record("Home " + screen + " variant " + variant + " chapter badge below capsule", ScreenRect((RectTransform)map.chapterText.transform.parent).yMax < blocked.yMin);
                if (screen.x == 1080 && (variant == 0 || screen.y == 1920))
                    yield return Capture("home-" + screen.x + "x" + screen.y + "-capsule-" + variant);
            }
        }

        if (overlay != null) Destroy(overlay.gameObject);
        yield return SceneManager.UnloadSceneAsync("Home");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null; yield return null;
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var coordinator in FindObjectsOfType<BattleOutcomeCoordinator>()) { coordinator.StopAllCoroutines(); coordinator.enabled = false; }
        Time.timeScale = 0;
        var hud = FindObjectOfType<GameLayer>().GetComponent<BattleThemeLayout>();
        var bridge = FindObjectOfType<PauseButtonBridge>(); var pause = bridge.GetComponent<BattleThemeLayout>();
        foreach (var screen in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400), new Vector2Int(360, 640) })
        {
            changeResolution(screen.x, screen.y); yield return null; yield return null;
            for (int variant = 0; variant < 2; variant++)
            {
                SetCapsule(variant); hud.Refresh(); pause.Refresh(); Canvas.ForceUpdateCanvases(); yield return null;
                var blocked = MenuScreenRect();
                Record("Battle " + screen + " variant " + variant + " pause avoids capsule", !ScreenRect((RectTransform)pause.transform).Overlaps(blocked));
                Record("Battle " + screen + " variant " + variant + " gradient reaches window top", ScreenRect((RectTransform)hud.top.Find("TopGradient")).yMax >= Screen.height - 1);
                foreach (var name in new[] { "Level", "TimerFace", "WaveFace", "KillsFace", "EnergyGroup/LevelFace", "EnergyGroup/Energy" })
                    Record("Battle " + screen + " variant " + variant + " " + name + " avoids capsule", !ScreenRect((RectTransform)hud.top.Find(name)).Overlaps(blocked));
                var pointer = new PointerEventData(EventSystem.current) { position = ScreenRect((RectTransform)bridge.transform).center };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Record("Battle " + screen + " variant " + variant + " pause remains clickable", hits.Count > 0 && hits[0].gameObject.GetComponentInParent<PauseButtonBridge>() == bridge);
                if (screen.x == 1080 && (variant == 0 || screen.y == 1920))
                    yield return Capture("battle-" + screen.x + "x" + screen.y + "-capsule-" + variant);
            }
        }
        Time.timeScale = 1; bridge.GetComponent<Button>().onClick.Invoke(); yield return new WaitForSecondsRealtime(.35f);
        Record("Relocated pause button opens existing pause page", UIManager.Instance.Top is GamePausePanel && Time.timeScale == 0);
        UIManager.Instance.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        PlatformMenuExclusion.EditorMenuRectOverride = null; hud.Refresh(); pause.Refresh();
        Record("Editor pause baseline restored without capsule", Mathf.Abs(((RectTransform)pause.transform).anchoredPosition.y + 210) < .1f);
        Time.timeScale = 1; changeResolution(1080, 1920);
    }

    private void SetCapsule(int variant)
    {
        Rect normalized;
        var css = variant == 0 ? new Rect(278, 52, 87, 32) : new Rect(250, 70, 112, 40);
        PlatformMenuExclusion.TryNormalize(css, new Vector2(375, 375f * Screen.height / Screen.width), out normalized);
        PlatformMenuExclusion.EditorMenuRectOverride = normalized;
        if (overlay == null)
        {
            overlay = new GameObject("Platform capsule preview", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 32760;
            capsule = new GameObject("WeChat capsule", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            capsule.SetParent(overlay.transform, false);
            var image = capsule.GetComponent<Image>(); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Res/image/common/surface_pill_cream_9slice.png");
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 3; image.color = new Color(.9f, .91f, .87f, .94f); image.raycastTarget = false;
            var label = new GameObject("Dots and close", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(capsule, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = Resources.Load<TMP_FontAsset>("Fonts/msyh SDF"); label.text = "···    ⊙"; label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        capsule.anchorMin = capsule.anchorMax = capsule.pivot = new Vector2(0, 1);
        capsule.anchoredPosition = new Vector2(normalized.x * Screen.width, -normalized.y * Screen.height);
        capsule.sizeDelta = new Vector2(normalized.width * Screen.width, normalized.height * Screen.height);
        capsule.GetComponentInChildren<TMP_Text>().fontSize = Screen.width * .043f;
    }
    private static Rect MenuScreenRect()
    {
        var menu = PlatformMenuExclusion.MenuRect;
        return new Rect(menu.x * Screen.width, (1 - menu.yMax) * Screen.height, menu.width * Screen.width, menu.height * Screen.height);
    }
    private static Rect ScreenRect(RectTransform rt)
    {
        var corners = new Vector3[4]; rt.GetWorldCorners(corners);
        var canvas = rt.GetComponentInParent<Canvas>(); var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private static Rect VisibleRect(RectTransform rt)
    {
        var result = ScreenRect(rt);
        for (var parent = rt.parent; parent != null; parent = parent.parent)
        {
            var mask = parent.GetComponent<RectMask2D>(); if (mask == null || !mask.enabled) continue;
            var bounds = ScreenRect((RectTransform)parent);
            float left = Mathf.Max(result.xMin, bounds.xMin), right = Mathf.Min(result.xMax, bounds.xMax);
            float bottom = Mathf.Max(result.yMin, bounds.yMin), top = Mathf.Min(result.yMax, bounds.yMax);
            if (right <= left || top <= bottom) return Rect.zero;
            result = Rect.MinMaxRect(left, bottom, right, top);
        }
        return result;
    }
    protected override void OnDestroy()
    {
        PlatformMenuExclusion.EditorMenuRectOverride = null; Time.timeScale = 1;
        if (overlay != null) Destroy(overlay.gameObject);
        Application.runInBackground = previousBackground; base.OnDestroy();
    }
}
#endif
