#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Uses an isolated temporary scene and real game prefabs/data; never saves player progress.</summary>
[InitializeOnLoad]
public static class SkillSelectionUIV4Preview
{
    private const string Pending = "GongkaoMower.SkillSelectV4.PreviewPending";
    private const string SetupKey = "GongkaoMower.SkillSelectV4.SceneSetup";
    private const string SizeKey = "GongkaoMower.SkillSelectV4.GameViewSize";

    static SkillSelectionUIV4Preview()
    {
        EditorApplication.update += RestoreWhenEditorReady;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                var runner = new GameObject("SkillSelection UI verification").AddComponent<SkillSelectionUIV4PreviewRunner>();
                runner.changeResolution = SetSize;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(SetupKey, "") != "")
                EditorApplication.delayCall += Restore;
        };
    }

    private static void RestoreWhenEditorReady()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !SessionState.GetBool(Pending, false) && SessionState.GetString(SetupKey, "") != "")
            Restore();
    }

    [MenuItem("工具/技能选卡/运行 V4 预览与行为检查")]
    private static void BeginMenu() { Debug.Log(Begin()); }

    public static string Begin()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("已有运行中的游戏，请先退出后再预览。");
        foreach (var setup in EditorSceneManager.GetSceneManagerSetup())
        {
            var scene = SceneManager.GetSceneByPath(setup.path);
            if (scene.isDirty) throw new InvalidOperationException("场景存在未保存编辑，不能自动切换预览场景。");
        }
        var saved = new SceneList();
        foreach (var setup in EditorSceneManager.GetSceneManagerSetup())
            saved.scenes.Add(new SceneRecord { path = setup.path, isLoaded = setup.isLoaded, isActive = setup.isActive });
        SessionState.SetString(SetupKey, JsonUtility.ToJson(saved));
        var gv = GameViewWindow();
        SessionState.SetInt(SizeKey, (int)gv.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gv));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetSize(1080, 1920);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
        return "已进入隔离预览；报告与截图保存至 美术资源/UI设计/技能选卡_Unity实装。";
    }

    [MenuItem("工具/技能选卡/退出预览并恢复场景")]
    public static void Finish()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else Restore();
    }

    public static void SetSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new [] { Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var fixedResolution = Enum.Parse(kind, "FixedResolution");
        string label = "选卡验证 " + width + "x" + height;
        var builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        var custom = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < builtin + custom; i++)
        {
            var value = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width").GetValue(value) == width && (int)sizeType.GetProperty("height").GetValue(value) == height)
            { index = i; break; }
        }
        if (index < 0)
        {
            var size = Activator.CreateInstance(sizeType, fixedResolution, width, height, label);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new [] { size });
            index = builtin + custom;
        }
        var window = GameViewWindow();
        window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, index);
        window.Show(); window.Focus(); window.Repaint();
    }

    private static EditorWindow GameViewWindow() => EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));

    private static void Restore()
    {
        string json = SessionState.GetString(SetupKey, "");
        SessionState.EraseString(SetupKey);
        if (string.IsNullOrEmpty(json)) return;
        var saved = JsonUtility.FromJson<SceneList>(json);
        var setup = new List<SceneSetup>();
        foreach (var scene in saved.scenes)
            if (!string.IsNullOrEmpty(scene.path)) setup.Add(new SceneSetup { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive });
        if (setup.Count > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.ToArray());
        var window = GameViewWindow();
        window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(window, SessionState.GetInt(SizeKey, 0));
    }

    [Serializable] private class SceneList { public List<SceneRecord> scenes = new List<SceneRecord>(); }
    [Serializable] private class SceneRecord { public string path; public bool isLoaded, isActive; }
}

#endif
