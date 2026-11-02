#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HomeUIBuilderUtility
{
    public const string Common = "Assets/Res/image/common/";
    public static TMP_FontAsset Font;
    public static Material TextMaterial;
    public static Color Ink => ColorOf("493627");
    public static Color Muted => ColorOf("76634C");

    public static void Prepare(string phase)
    {
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/UI/" + phase + "-assets.json"));
        foreach (var asset in manifest.assets)
        {
            if (!string.IsNullOrEmpty(asset.moveFrom) && !File.Exists(asset.path))
            {
                string error = AssetDatabase.MoveAsset(asset.moveFrom, asset.path);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var importer = AssetImporter.GetAtPath(asset.path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing UI importer: " + asset.path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(asset.border[0], asset.border[1], asset.border[2], asset.border[3]);
            importer.maxTextureSize = asset.maxSize; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.alphaIsTransparency = !asset.path.EndsWith(".jpg");
            importer.SaveAndReimport();
        }
        Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        string materialPath = Common + "home_ui_text.mat";
        TextMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (TextMaterial == null)
        {
            TextMaterial = new Material(Font.material) { name = "home_ui_text" };
            AssetDatabase.CreateAsset(TextMaterial, materialPath);
        }
        TextMaterial.SetColor("_FaceColor", Color.white);
        foreach (var key in new [] { "_OutlineWidth", "_OutlineSoftness", "_FaceDilate" })
            if (TextMaterial.HasProperty(key)) TextMaterial.SetFloat(key, 0);
        if (TextMaterial.HasProperty("_UnderlayColor")) TextMaterial.SetColor("_UnderlayColor", Color.clear);
        foreach (var keyword in new [] { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "GLOW_ON" }) TextMaterial.DisableKeyword(keyword);
        EditorUtility.SetDirty(TextMaterial); AssetDatabase.SaveAssetIfDirty(TextMaterial);
    }

    public static Sprite Sprite(string name, string folder = null)
    {
        string path = (folder ?? Common) + name;
        if (!path.EndsWith(".png") && !path.EndsWith(".jpg")) path += ".png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing UI sprite: " + path);
        return sprite;
    }
    public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
    {
        var rt = parent.Find(name) as RectTransform;
        if (rt == null) { rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false); }
        Rect(rt, x, y, w, h); return rt;
    }
    public static void Rect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
    }
    public static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f,.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
    }
    public static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        var rt = Node(parent, name, x, y, w, h);
        var image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.color = Color.white; image.raycastTarget = false; return image;
    }
    public static TextMeshProUGUI Text(Transform parent, string name, string text, float x, float y, float w, float h,
        float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = Node(parent, name, x, y, w, h);
        var tmp = rt.GetComponent<TextMeshProUGUI>() ?? rt.gameObject.AddComponent<TextMeshProUGUI>();
        Style(tmp, size, color, bold, align); tmp.text = text; return tmp;
    }
    public static void Style(TMP_Text tmp, float size, Color color, bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        tmp.font = Font; tmp.fontSharedMaterial = TextMaterial; tmp.fontSize = size;
        tmp.color = color; tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; tmp.alignment = align;
        tmp.enableAutoSizing = false; tmp.enableWordWrapping = false; tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.margin = Vector4.zero; tmp.raycastTarget = false;
    }
    public static Button Button(Image image)
    {
        image.raycastTarget = true;
        var button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.transition = Selectable.Transition.None;
        return button;
    }
    public static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void Disable(Transform parent, string name) { var child = parent.Find(name); if (child != null) child.gameObject.SetActive(false); }
    public static void RemoveLayout(RectTransform rt)
    {
        foreach (var group in rt.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(group);
        var fitter = rt.GetComponent<ContentSizeFitter>(); if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
    }
    public static Color ColorOf(string hex) { Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c; }
    [Serializable] private class Manifest { public Asset[] assets; }
    [Serializable] private class Asset { public string path, moveFrom; public int[] border; public int maxSize; }
}
#endif
