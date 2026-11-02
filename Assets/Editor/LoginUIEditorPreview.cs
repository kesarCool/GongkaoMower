#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LoginUIEditorPreview
{
    private const string Key = "GongkaoMower.LoginUIPreview.";
    static LoginUIEditorPreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + "pending", false))
            {
                SessionState.SetBool(Key + "pending", false);
                var runner = new GameObject("Login UI validation").AddComponent<LoginUIValidationRunner>();
                runner.changeResolution = SkillSelectionUIV4Preview.SetSize;
            }
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore;
        };
    }
    [MenuItem("工具/Home UI/Preview Login")]
    public static string Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var setups = EditorSceneManager.GetSceneManagerSetup();
        foreach (var setup in setups)
            if (SceneManager.GetSceneByPath(setup.path).isDirty) throw new InvalidOperationException("Save edited scenes before preview.");
        var saved = new SceneList();
        saved.scenes.AddRange(setups.Select(s => new Record { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }));
        SessionState.SetString(Key + "scenes", JsonUtility.ToJson(saved));
        var window = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        SessionState.SetInt(Key + "size", (int)window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(window));
        SessionState.SetBool(Key + "background", Application.runInBackground);
        Application.runInBackground = true;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SkillSelectionUIV4Preview.SetSize(1080, 1920);
        SessionState.SetBool(Key + "pending", true);
        EditorApplication.isPlaying = true;
        return "Started Login UI validation with in-memory profile and real Login scene.";
    }
    private static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string json = SessionState.GetString(Key + "scenes", "");
        if (string.IsNullOrEmpty(json)) return;
        SessionState.EraseString(Key + "scenes");
        var setups = JsonUtility.FromJson<SceneList>(json).scenes.Where(s => !string.IsNullOrEmpty(s.path))
            .Select(s => new SceneSetup { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray();
        if (setups.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setups);
        var window = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(window, SessionState.GetInt(Key + "size", 0));
        Application.runInBackground = SessionState.GetBool(Key + "background", false);
    }
    [Serializable] private class SceneList { public List<Record> scenes = new List<Record>(); }
    [Serializable] private class Record { public string path; public bool isLoaded, isActive; }
}
#endif
