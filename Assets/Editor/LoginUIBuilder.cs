#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class LoginUIBuilder
{
    private const string ScenePath = "Assets/Scenes/Login.unity";
    private const string Art = "Assets/Res/image/Home/login/";

    [MenuItem("工具/Home UI/Apply Login")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save edited scenes before applying login UI.");
        U.Prepare("login");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Canvas>(true)).First();
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
            var controller = canvas.GetComponentInChildren<LoginSceneController>(true);
            var root = (RectTransform)controller.transform; U.Fill(root);
            var refs = new SerializedObject(controller);
            var start = (Button)refs.FindProperty("startGameButton").objectReferenceValue;
            var version = (TextMeshProUGUI)refs.FindProperty("versionText").objectReferenceValue;
            var oldHealth = root.Find("Text (TMP)");
            var bg = U.Image(root, "bg", U.Sprite("login_background.jpg", Art), 0, 0, 1080, 1920);
            bg.preserveAspect = false; bg.transform.SetAsFirstSibling();
            var veil = U.Image(root, "BottomVeil", U.Sprite("bottom_veil", Art), 0, 1530, 1080, 390);
            veil.transform.SetSiblingIndex(1);
            var safe = U.Node(root, "SafeContent", 0, 0, 1080, 1920); U.Fill(safe);
            var top = U.Node(safe, "TopContent", 0, 0, 1080, 520);
            var bottom = U.Node(safe, "BottomContent", 0, 1530, 1080, 390);
            var logo = U.Image(top, "GameTitle", U.Sprite("game_title", Art), 20, 212, 1040, 250);
            logo.preserveAspect = true;
            version.transform.SetParent(top, false); version.name = "VersionLabel";
            U.Rect(version.rectTransform, 48, 86, 510, 44);
            U.Style(version, 27, U.Muted, false, TextAlignmentOptions.MidlineLeft);
            version.text = "版本号：v" + Application.version;
            start.transform.SetParent(bottom, false); start.name = "StartButton";
            U.Rect((RectTransform)start.transform, 200, 50, 680, 136);
            var face = start.GetComponent<Image>(); face.sprite = U.Sprite("button_primary_normal_9slice");
            face.type = Image.Type.Sliced; face.pixelsPerUnitMultiplier = 1; face.color = Color.white; face.raycastTarget = true;
            start.targetGraphic = face; start.transition = Selectable.Transition.SpriteSwap;
            var states = new SpriteState { pressedSprite = U.Sprite("button_primary_pressed_9slice"),
                disabledSprite = U.Sprite("button_primary_disabled_9slice") };
            start.spriteState = states;
            var label = start.GetComponentInChildren<TextMeshProUGUI>(true);
            label.name = "Label"; U.Rect(label.rectTransform, 128, 0, 480, 136);
            U.Style(label, 56, U.Ink, true); label.text = "开始游戏";
            U.Image(start.transform, "SwordsIcon", U.Sprite("icon_swords"), 56, 32, 72, 72);
            if (oldHealth != null) { oldHealth.SetParent(bottom, false); oldHealth.name = "HealthHeading"; }
            string[] lines = { "健康游戏忠告", "抵制不良游戏，拒绝盗版游戏。", "注意自我保护，谨防受骗上当。",
                "适度游戏益脑，沉迷游戏伤身。", "合理安排时间，享受健康生活。" };
            for (int i = 0; i < lines.Length; i++)
                U.Text(bottom, i == 0 ? "HealthHeading" : "HealthLine" + i, lines[i], 90, 209 + i * 29,
                    900, 34, i == 0 ? 25 : 24, U.Muted, i == 0);
            var layout = root.GetComponent<LoginScreenLayout>() ?? root.gameObject.AddComponent<LoginScreenLayout>();
            U.Ref(layout, "background", bg.rectTransform); U.Ref(layout, "safeContent", safe);
            U.Ref(layout, "topContent", top); U.Ref(layout, "bottomContent", bottom); U.Ref(layout, "bottomVeil", veil.rectTransform);
            Canvas.ForceUpdateCanvases(); layout.Apply();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Login UI updated; controller references and player navigation settings preserved.";
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
#endif
