using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 批量转换 UI.Text → TextMeshProUGUI（含场景和预制体）。
/// 菜单：Tools / 批量转换 Text 到 TMP
/// </summary>
public class ConvertTextToTMP : EditorWindow
{
    private bool includePrefabs = true;
    private bool includeScenes = true;

    [MenuItem("Tools/批量转换 Text 到 TMP")]
    private static void ShowWindow()
    {
        var w = GetWindow<ConvertTextToTMP>("Text → TMP");
        w.minSize = new Vector2(360, 140);
        w.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("将场景和预制体中的 UI.Text 替换为 TextMeshProUGUI", EditorStyles.boldLabel);
        GUILayout.Space(10);
        includeScenes = EditorGUILayout.Toggle("转换当前场景", includeScenes);
        includePrefabs = EditorGUILayout.Toggle("转换所有预制体", includePrefabs);
        GUILayout.Space(10);

        if (GUILayout.Button("执行转换", GUILayout.Height(36)))
        {
            int count = 0;
            if (includeScenes)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.isLoaded)
                {
                    int n = ConvertScene(scene);
                    count += n;
                    Debug.Log($"[Text→TMP] 场景 '{scene.name}': 转换 {n} 个 Text");
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            if (includePrefabs)
            {
                var guids = AssetDatabase.FindAssets("t:Prefab");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    bool modified = false;
                    var all = prefab.GetComponentsInChildren<Text>(true);
                    foreach (var t in all)
                    {
                        ConvertSingle(t);
                        modified = true;
                    }

                    if (modified)
                    {
                        count += all.Length;
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }
                Debug.Log($"[Text→TMP] 预制体: 转换 {count} 个 Text");
            }

            EditorUtility.DisplayDialog("完成", $"共转换 {count} 个 Text 组件。\n\n请手动检查转换结果（字体、颜色、对齐等可能需要微调）。", "确定");
        }
    }

    private static int ConvertScene(Scene scene)
    {
        int count = 0;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            var all = root.GetComponentsInChildren<Text>(true);
            foreach (var t in all)
            {
                ConvertSingle(t);
                count++;
            }
        }
        return count;
    }

    private static void ConvertSingle(Text uiText)
    {
        if (uiText == null) return;
        var go = uiText.gameObject;

        // 如果已有 TMP 组件，跳过
        var existingTMP = go.GetComponent<TextMeshProUGUI>();
        if (existingTMP != null) return;

        // 记录原始属性
        var text = uiText.text;
        var color = uiText.color;
        var fontSize = uiText.fontSize;
        var fontStyle = uiText.fontStyle;
        var alignment = uiText.alignment;
        var raycast = uiText.raycastTarget;
        var bestFit = uiText.resizeTextForBestFit;
        var bestFitMin = uiText.resizeTextMinSize;
        var bestFitMax = uiText.resizeTextMaxSize;
        var richText = uiText.supportRichText;

        // 创建 TMP 组件
        var tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        EditorUtility.CopySerializedManagedFieldsOnly(uiText, tmp);

        // 覆盖关键属性
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.fontStyle = ConvertFontStyle(fontStyle);
        tmp.alignment = ConvertAlignment(alignment);
        tmp.raycastTarget = raycast;
        tmp.enableAutoSizing = bestFit;
        tmp.fontSizeMin = bestFitMin;
        tmp.fontSizeMax = bestFitMax;
        tmp.richText = richText;

        // 禁用原 Text（保留以避免引用断裂）
        Undo.DestroyObjectImmediate(uiText);
    }

    private static FontStyles ConvertFontStyle(FontStyle fs)
    {
        var style = FontStyles.Normal;
        if ((fs & FontStyle.Bold) != 0) style |= FontStyles.Bold;
        if ((fs & FontStyle.Italic) != 0) style |= FontStyles.Italic;
        return style;
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };
    }
}
