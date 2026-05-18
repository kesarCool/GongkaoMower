using TMPro;
using UnityEngine;

/// <summary>
/// Legacy UI Text → TextMeshProUGUI 的创建与对齐映射。
/// </summary>
public static class UITextMeshProUtil
{
    public static TextAlignmentOptions ToAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    public static FontStyles ToFontStyles(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }

    public static TextMeshProUGUI CreateUGUI(
        string name,
        Transform parent,
        string defaultText,
        TextAnchor anchor,
        int fontSize,
        Color color,
        bool raycastTarget = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText ?? string.Empty;
        tmp.fontSize = fontSize;
        tmp.alignment = ToAlignment(anchor);
        tmp.color = color;
        tmp.raycastTarget = raycastTarget;

        BattleChineseFontRuntime.EnsureLoaded();
        if (BattleChineseFontRuntime.LoadedFont != null)
            tmp.font = BattleChineseFontRuntime.LoadedFont;

        return tmp;
    }
}
