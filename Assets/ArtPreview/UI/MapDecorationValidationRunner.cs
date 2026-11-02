#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Actual pause-page clicks, panel reopening and battle re-entry; preserves the original preference.</summary>
public sealed class MapDecorationValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "MapDecoration";
    protected override string ValidationNote => "Actual Game scene and pause-page checkbox, with persisted PlayerPrefs setting. Isolated player profile; original decoration preference restored. No WeChat-device validation.";
    bool hadKey, previousBackground, restored;
    int previousValue;
    const string Key = BattleMapPreferences.ShowDecorationKey;

    void Awake()
    {
        hadKey = PlayerPrefs.HasKey(Key); previousValue = PlayerPrefs.GetInt(Key, 1);
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
    }
    Toggle Checkbox => FindObjectOfType<MapDecorationToggle>().GetComponent<Toggle>();
    GamePausePanel Pause => FindObjectOfType<GamePausePanel>();
    Transform Decoration => BattleMapLoader.Instance.GroundTilemap.transform.parent.Find("Decoration");
    bool SwitchMatches(Toggle control)
    {
        var view = control.GetComponent<ThemeAudioToggle>();
        return view != null && view.knob != null && Mathf.Approximately(view.knob.anchoredPosition.x, control.isOn ? 29 : -29);
    }
    void Click(Toggle toggle)
    {
        ExecuteEvents.Execute(toggle.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    void Freeze()
    {
        foreach (var s in FindObjectsOfType<SpawnerWaves>()) { s.StopAllCoroutines(); s.enabled = false; }
        foreach (var c in FindObjectsOfType<BattleOutcomeCoordinator>()) { c.StopAllCoroutines(); c.enabled = false; }
        Time.timeScale = 0;
    }
    IEnumerator OpenPause()
    {
        FindObjectOfType<PauseButtonBridge>().GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.35f);
        Canvas.ForceUpdateCanvases();
    }
    IEnumerator Resume()
    {
        Pause.resumeButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.35f);
    }
    protected override IEnumerator RunChecks()
    {
        yield return SceneManager.UnloadSceneAsync("Home");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null; yield return null; Freeze();
        var hud = FindObjectOfType<GameLayer>();
        Record("Battle HUD has no decoration checkbox", hud.GetComponentInChildren<MapDecorationToggle>(true) == null);
        Record("HUD retains original movement-layer ordering", hud.transform.GetSiblingIndex() < FindObjectOfType<TouchLayer>().transform.GetSiblingIndex());
        yield return OpenPause();
        var toggle = Checkbox;
        Record("First use defaults to visible and checked", toggle != null && toggle.isOn && Decoration.gameObject.activeSelf && !PlayerPrefs.HasKey(Key));
        var label = (RectTransform)toggle.transform.Find("Label");
        var point = RectTransformUtility.WorldToScreenPoint(null, label.TransformPoint(label.rect.center));
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        Record("Label area receives checkbox taps", hits.Count > 0 && hits[0].gameObject.GetComponentInParent<MapDecorationToggle>() != null);
        var layout = Pause.GetComponent<BattleThemeLayout>();
        Record("Checkbox belongs to pause panel and inherits its safe-area layout", toggle.transform.parent == layout.panel);
        var music = (RectTransform)Pause.bgmToggle.transform;
        var sound = (RectTransform)Pause.sfxToggle.transform;
        var decor = (RectTransform)toggle.transform;
        Record("Three settings share one row without overlap", music.anchoredPosition.y == sound.anchoredPosition.y && sound.anchoredPosition.y == decor.anchoredPosition.y &&
            music.anchoredPosition.x + music.sizeDelta.x <= sound.anchoredPosition.x && sound.anchoredPosition.x + sound.sizeDelta.x <= decor.anchoredPosition.x);
        Record("Music and sound bindings stay interactive", Pause.bgmToggle.interactable && Pause.sfxToggle.interactable && Pause.bgmLabel != null && Pause.sfxLabel != null);
        Record("Three setting cards have matching size and fixed labels", music.sizeDelta == sound.sizeDelta && sound.sizeDelta == decor.sizeDelta && decor.sizeDelta.y >= 100 &&
            Pause.bgmLabel.text == "背景音乐" && Pause.sfxLabel.text == "游戏音效" && label.GetComponent<TMPro.TMP_Text>().text == "地图装饰");
        Record("All three switch visuals match their values", SwitchMatches(Pause.bgmToggle) && SwitchMatches(Pause.sfxToggle) && SwitchMatches(toggle));
        var child = Decoration.GetChild(0); bool childActive = child.gameObject.activeSelf;
        child.gameObject.SetActive(false);
        Click(toggle); yield return null;
        Record("Uncheck hides Decoration immediately", !toggle.isOn && !Decoration.gameObject.activeSelf);
        Record("Uncheck persists false", PlayerPrefs.HasKey(Key) && PlayerPrefs.GetInt(Key, -1) == 0 && !BattleMapPreferences.ShowDecoration);
        Record("Decoration switch renders its off state", SwitchMatches(toggle));
        var map = BattleMapLoader.Instance.GroundTilemap.transform.parent;
        Record("Ground and four boundary walls remain active", map.Find("Ground").gameObject.activeInHierarchy && map.Find("Wall").gameObject.activeInHierarchy && map.Find("Wall").childCount == 4);
        Click(toggle); yield return null;
        Record("Check restores Decoration without enabling individual hidden props", toggle.isOn && Decoration.gameObject.activeSelf && !child.gameObject.activeSelf);
        child.gameObject.SetActive(childActive);
        Record("Check persists true", PlayerPrefs.GetInt(Key, -1) == 1);
        yield return Capture("pause-decorations-visible-1080x1920");
        Click(toggle); yield return null;
        yield return Capture("pause-decorations-hidden-1080x1920");
        yield return Resume();
        Record("Resume keeps decoration hidden and HUD clear", !Decoration.gameObject.activeSelf && FindObjectOfType<MapDecorationToggle>() == null &&
            FindObjectOfType<GameLayer>().GetComponentInChildren<MapDecorationToggle>(true) == null);
        yield return Capture("battle-no-decoration-control-1080x1920");
        BattleMapLoader.Instance.LoadMapForCurrentLevel(); yield return null;
        Record("Reloaded map respects saved hidden preference", !Decoration.gameObject.activeSelf && !BattleMapPreferences.ShowDecoration);
        yield return OpenPause();
        Record("Reopened pause panel restores saved checkbox state", !Checkbox.isOn && !Decoration.gameObject.activeSelf);
        Record("Reopened switch restores its saved visual state", SwitchMatches(Checkbox));
        yield return Resume();
        yield return SceneManager.UnloadSceneAsync("Game");
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null; yield return null; Freeze();
        yield return OpenPause();
        Record("Fresh battle and HUD restore saved hidden preference", !Checkbox.isOn && !Decoration.gameObject.activeSelf && PlayerPrefs.GetInt(Key, -1) == 0);
        Click(Checkbox); yield return null;
        Record("Fresh battle checkbox restores visible state", Checkbox.isOn && Decoration.gameObject.activeSelf && PlayerPrefs.GetInt(Key, -1) == 1);
        RestorePreference();
        Record("Original local preference restored after validation", PlayerPrefs.HasKey(Key) == hadKey && (!hadKey || PlayerPrefs.GetInt(Key) == previousValue));
    }
    void RestorePreference()
    {
        if (restored) return;
        if (hadKey) PlayerPrefs.SetInt(Key, previousValue); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save(); restored = true;
    }
    protected override void OnDestroy()
    {
        RestorePreference(); Application.runInBackground = previousBackground; base.OnDestroy();
    }
}
#endif
