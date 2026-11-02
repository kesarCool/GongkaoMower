using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class BattleDecorationToggleBuilder
{
    const string HudPath = "Assets/Prefab/Battle/GameLayer.prefab";
    const string PausePath = "Assets/Prefab/Result/GamePausePanel.prefab";

    [MenuItem("Tools/Battle UI/Add Decoration Checkbox")]
    public static void Menu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        string backup = "Library/UIBackups/PauseMapDecoration-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(HudPath, backup + "/GameLayer.prefab"); File.Copy(PausePath, backup + "/GamePausePanel.prefab");
        var hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var old = hud.transform.Find("HudTop/DecorationToggle");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        var root = PrefabUtility.LoadPrefabContents(PausePath);
        try { BuildInto(root); PrefabUtility.SaveAsPrefabAsset(root, PausePath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "Decoration checkbox moved from battle HUD into pause settings. Backup: " + backup;
    }

    public static void BuildInto(GameObject root)
    {
        var panel = root.transform.Find("Panel");
        if (panel == null || root.GetComponent<GamePausePanel>() == null) throw new InvalidOperationException("Missing pause panel.");
        U.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        U.TextMaterial = AssetDatabase.LoadAssetAtPath<Material>(U.Common + "home_ui_text.mat");
        StyleCard(panel, "Music", "背景音乐", 42);
        StyleCard(panel, "Sound", "游戏音效", 332);
        var decoration = StyleCard(panel, "DecorationToggle", "地图装饰", 622);
        if (decoration.GetComponent<MapDecorationToggle>() == null) decoration.gameObject.AddComponent<MapDecorationToggle>();

        // Preserve all five active-skill rows: move the content below the taller settings row.
        SetY(panel, "AudioLine", 390);
        foreach (string name in new[] { "ActiveTitle", "ActiveCount", "DamageTitle" }) SetY(panel, name, 390);
        SetY(panel, "ActiveSkills", 466); SetY(panel, "EmptyActive", 496);
        SetY(panel, "PassiveTitle", 935); SetY(panel, "PassiveCount", 935); SetY(panel, "PassiveSkills", 994);
        SetY(panel, "Quit", 1300); SetY(panel, "Resume", 1300);
        var paper = (RectTransform)panel; paper.sizeDelta = new Vector2(paper.sizeDelta.x, 1470);
        var layout = root.GetComponent<BattleThemeLayout>(); layout.height = 1470; layout.Refresh();
    }

    static Toggle StyleCard(Transform panel, string name, string label, float x)
    {
        var face = U.Image(panel, name, U.Sprite("surface_soft_9slice", "Assets/Res/image/Game/battle/"), x, 252, 272, 120);
        face.color = U.ColorOf("F0ECD9"); face.raycastTarget = true;
        var toggle = face.GetComponent<Toggle>() ?? face.gameObject.AddComponent<Toggle>();
        toggle.graphic = null; toggle.targetGraphic = face; toggle.transition = Selectable.Transition.None;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        foreach (string oldName in new[] { "Icon", "Box" })
        {
            var old = face.transform.Find(oldName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        U.Text(face.transform, "Label", label, 12, 10, 248, 36, 30, U.Ink, false, TextAlignmentOptions.Center);
        var track = U.Image(face.transform, "Switch", U.Sprite("switch_track_9slice"), 84, 56, 104, 52);
        var knob = U.Image(track.transform, "Knob", U.Sprite("switch_knob"), 0, 0, 44, 44);
        knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = knob.rectTransform.pivot = new Vector2(.5f, .5f);
        var view = face.GetComponent<ThemeAudioToggle>() ?? face.gameObject.AddComponent<ThemeAudioToggle>();
        view.track = track; view.knob = knob.rectTransform; view.Refresh();
        return toggle;
    }

    static void SetY(Transform panel, string name, float y)
    {
        var rect = panel.Find(name) as RectTransform;
        if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
    }
}
