#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

/// <summary>Isolated checks; never enables AppBootstrap or touches the player profile.</summary>
public static class IosFrameRateValidation
{
    private const int ExpectedEditorTarget = 60;
    private const int AlternateTarget = 120;

    public static string RunLoginStartup()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode from the first build scene.");
        var scenes = UnityEditor.EditorBuildSettings.scenes;
        string firstScene = Array.Find(scenes, s => s.enabled).path;
        int checks = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("FAIL: " + name); checks++; };
        check(firstScene == "Assets/Scenes/Login.unity", "test follows actual Login build entry");
        check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == firstScene, "Login is active");
        check(UnityEngine.Object.FindObjectsOfType<AppBootstrap>().Length == 0, "Boot bootstrap is absent");
        var controllers = UnityEngine.Object.FindObjectsOfType<AppFrameRateController>();
        check(controllers.Length == 1, "runtime hook installed exactly one controller without Boot");
        var controller = controllers[0];
        check(controller.gameObject.scene.name == "DontDestroyOnLoad", "controller survives scene changes");
        check(Application.targetFrameRate == ExpectedEditorTarget && QualitySettings.vSyncCount == 0, "real startup applied Editor frame policy");
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var focus = typeof(AppFrameRateController).GetMethod("OnApplicationFocus", flags);
        var pause = typeof(AppFrameRateController).GetMethod("OnApplicationPause", flags);
        try
        {
            Application.targetFrameRate = AlternateTarget;
            QualitySettings.vSyncCount = 1;
            focus.Invoke(controller, new object[] { false });
            check(Application.targetFrameRate == AlternateTarget, "focus loss does not change target");
            focus.Invoke(controller, new object[] { true });
            check(Application.targetFrameRate == ExpectedEditorTarget && QualitySettings.vSyncCount == 0, "Login focus return restores Editor policy");
            Application.targetFrameRate = AlternateTarget;
            QualitySettings.vSyncCount = 1;
            pause.Invoke(controller, new object[] { true });
            check(Application.targetFrameRate == AlternateTarget, "pause does not change target");
            pause.Invoke(controller, new object[] { false });
            check(Application.targetFrameRate == ExpectedEditorTarget && QualitySettings.vSyncCount == 0, "Login resume restores Editor policy");
            var install = typeof(AppFrameRateController).GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Static);
            install.Invoke(null, null);
            install.Invoke(null, null);
            check(UnityEngine.Object.FindObjectsOfType<AppFrameRateController>().Length == 1, "repeated installation is idempotent");
            typeof(AppFrameRateController).GetMethod("ResetInstance", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            install.Invoke(null, null);
            check(UnityEngine.Object.FindObjectsOfType<AppFrameRateController>().Length == 1 && UnityEngine.Object.FindObjectOfType<AppFrameRateController>() == controller, "static reset reuses the existing controller");
            return "PASS: " + checks + " actual Login-entry checks, with no AppBootstrap in scene.";
        }
        finally { AppBootstrap.ApplyFrameRateSettings(); }
    }

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        var host = new GameObject("FrameRateValidation");
        host.SetActive(false);
        var bootstrap = host.AddComponent<AppBootstrap>();
        int previousTarget = Application.targetFrameRate, previousVsync = QualitySettings.vSyncCount;
        int checks = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("FAIL: " + name); checks++; };
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var focus = typeof(AppBootstrap).GetMethod("OnApplicationFocus", flags);
        var pause = typeof(AppBootstrap).GetMethod("OnApplicationPause", flags);
        try
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
            AppBootstrap.ApplyFrameRateSettings();
            check(AppBootstrap.TargetFrameRate == 30, "player budget remains 30 FPS");
            check(AppBootstrap.EditorTargetFrameRate == ExpectedEditorTarget, "Editor budget is 60 FPS");
            check(Application.targetFrameRate == ExpectedEditorTarget, "Editor applies its independent 60 FPS target");
            check(QualitySettings.vSyncCount == 0, "vSync does not override target");

            Application.targetFrameRate = AlternateTarget;
            QualitySettings.vSyncCount = 1;
            focus.Invoke(bootstrap, new object[] { false });
            check(Application.targetFrameRate == AlternateTarget, "focus loss does not change the target");
            focus.Invoke(bootstrap, new object[] { true });
            check(Application.targetFrameRate == ExpectedEditorTarget && QualitySettings.vSyncCount == 0, "focus return restores Editor settings");

            Application.targetFrameRate = AlternateTarget;
            QualitySettings.vSyncCount = 1;
            pause.Invoke(bootstrap, new object[] { true });
            check(Application.targetFrameRate == AlternateTarget, "pause does not change the target");
            pause.Invoke(bootstrap, new object[] { false });
            check(Application.targetFrameRate == ExpectedEditorTarget && QualitySettings.vSyncCount == 0, "resume restores Editor settings");
            check(!host.activeSelf, "bootstrap never enabled; no startup/profile work");
            return "PASS: " + checks + " Unity frame-target and lifecycle checks. JS replay and device FPS require separate validation.";
        }
        finally
        {
            Application.targetFrameRate = previousTarget;
            QualitySettings.vSyncCount = previousVsync;
            UnityEngine.Object.Destroy(host);
        }
    }
}
#endif
