#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoginUIValidationRunner : MonoBehaviour
{
    public Action<int, int> changeResolution;
    private readonly List<Check> checks = new List<Check>();
    private readonly List<string> errors = new List<string>();
    private UIProfileFixture fixture;
    private string output;
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        output = Path.GetFullPath("ArtOutput/UI/LoginUnity"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "status.txt"), "running");
        Application.logMessageReceived += OnLog;
        fixture = new UIProfileFixture(); fixture.SetProgress(1);
        var flow = RunChecks();
        while (true)
        {
            bool next;
            try { next = flow.MoveNext(); }
            catch (Exception ex) { Record("No validation exception", false, ex.ToString()); break; }
            if (!next) break;
            yield return flow.Current;
        }
        Record("PlayerPrefs preserved", fixture.IsPersistentSaveUnchanged);
        Record("No runtime errors", errors.Count == 0, string.Join("\n", errors));
        File.WriteAllText(Path.Combine(output, "validation-report.json"), JsonUtility.ToJson(new Report
        { passed = checks.All(c => c.passed), checks = checks, note = "Unity Editor real Login scene. In-memory profile. Safe area simulated; no WeChat device/build validation." }, true));
        File.WriteAllText(Path.Combine(output, "status.txt"), checks.All(c => c.passed) ? "passed" : "failed");
        EditorApplication.isPlaying = false;
    }
    private IEnumerator RunChecks()
    {
        yield return SceneManager.LoadSceneAsync("Login", LoadSceneMode.Single);
        yield return null; yield return null;
        var login = FindObjectOfType<LoginSceneController>();
        var so = new SerializedObject(login);
        var button = (Button)so.FindProperty("startGameButton").objectReferenceValue;
        var version = (TMP_Text)so.FindProperty("versionText").objectReferenceValue;
        Record("Unified Home navigation configuration", so.FindProperty("homeSceneName").stringValue == "Home" && so.FindProperty("loadAsync").boolValue);
        Record("Runtime version", version.text == "版本号：v" + Application.version, version.text);
        Record("Start button interactive", button.interactable && button.targetGraphic.raycastTarget && button.onClick != null);
        Record("Title and poster assigned", login.transform.Find("bg").GetComponent<Image>().sprite != null &&
            login.transform.Find("SafeContent/TopContent/GameTitle").GetComponent<Image>().sprite != null);
        Record("Existing font reused", login.GetComponentsInChildren<TMP_Text>().All(t => t.font == Resources.Load<TMP_FontAsset>("Fonts/msyh SDF")));
        yield return Capture("login-1080x1920");
        CheckGeometry(login, "1080x1920");
        var startRect = (RectTransform)button.transform;
        Record("Approved button geometry", startRect.sizeDelta == new Vector2(680, 136) && startRect.anchoredPosition == new Vector2(200, -50));
        foreach (int height in new[] { 2400, 1440 })
        {
            changeResolution(1080, height); yield return null; yield return null;
            yield return Capture("login-1080x" + height);
            CheckGeometry(login, "1080x" + height);
        }
        changeResolution(1080, 2400); yield return null; yield return null;
        var layout = login.GetComponent<LoginScreenLayout>(); layout.enabled = false;
        var safe = new Rect(0, 72f / 2400, 1, (2400 - 132 - 72f) / 2400);
        layout.ApplyLayout(safe); Canvas.ForceUpdateCanvases();
        yield return Capture("login-1080x2400-safe-area");
        var corners = new Vector3[4]; startRect.GetWorldCorners(corners);
        var first = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var last = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        Record("Safe area button bounds", first.y >= 72 && last.y <= 2400 - 132);
        // A fresh profile now exercises the same unmodified login button as returning players.
        fixture.SetProgress(0);
        button.onClick.Invoke();
        float timeout = Time.realtimeSinceStartup + 15;
        while (SceneManager.GetActiveScene().name != "Home" && Time.realtimeSinceStartup < timeout) yield return null;
        Record("New player start button loads Home", SceneManager.GetActiveScene().name == "Home");
        yield return new WaitForSecondsRealtime(.8f);
        Record("New player continues with Home tutorial", TutorialDirector.IsShowing && TutorialDirector.Current.ActiveStep == TutorialDirector.StartBattle);
        fixture.SetProgress(1);
        yield return SceneManager.LoadSceneAsync("Login", LoadSceneMode.Single);
        yield return null; yield return null;
        login = FindObjectOfType<LoginSceneController>(); so = new SerializedObject(login);
        button = (Button)so.FindProperty("startGameButton").objectReferenceValue;
        button.onClick.Invoke();
        timeout = Time.realtimeSinceStartup + 15;
        while (SceneManager.GetActiveScene().name != "Home" && Time.realtimeSinceStartup < timeout) yield return null;
        Record("Returning player start button also loads Home", SceneManager.GetActiveScene().name == "Home");
    }
    private void CheckGeometry(LoginSceneController login, string size)
    {
        var root = (RectTransform)login.transform;
        var bg = (RectTransform)login.transform.Find("bg");
        Record(size + " poster covers viewport", bg.rect.width >= root.rect.width - .1f && bg.rect.height >= root.rect.height - .1f);
        Record(size + " poster preserves aspect", Mathf.Abs(bg.rect.width / bg.rect.height - 1080f / 1920) < .0001f);
        var version = (RectTransform)login.transform.Find("SafeContent/TopContent/VersionLabel");
        var lastLine = (RectTransform)login.transform.Find("SafeContent/BottomContent/HealthLine4");
        var corners = new Vector3[4]; version.GetWorldCorners(corners);
        float titleTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
        lastLine.GetWorldCorners(corners);
        float footerBottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
        Record(size + " controls inside viewport", titleTop <= Screen.height + 1 && footerBottom >= -1);
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.35f); Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null;
    }
    private void Record(string name, bool passed, string detail = "") { checks.Add(new Check { name = name, passed = passed, detail = detail }); }
    private void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
    private void OnDestroy() { Application.logMessageReceived -= OnLog; if (fixture != null) fixture.Restore(); }
    [Serializable] private class Check { public string name, detail; public bool passed; }
    [Serializable] private class Report { public bool passed; public List<Check> checks; public string note; }
}
#endif
