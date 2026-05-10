using UnityEngine;

/// <summary>
/// Do not use <see cref="Resources.GetBuiltinResource{T}"/> for UI/Skin/*.psd: many Unity versions log
/// errors on every failed lookup even when the return value is handled. Use a tiny procedural sprite instead.
/// </summary>
public static class RuntimeSprites
{
    private static Sprite _white;

    /// <summary>1×1 white sprite (cached). Tint via <see cref="SpriteRenderer.color"/> / Image.color.</summary>
    public static Sprite GetUiPlaceholderSprite()
    {
        if (_white != null) return _white;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.name = "RuntimeSprites_White1x1";
        tex.SetPixel(0, 0, Color.white);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.Apply(false, false);
        _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        _white.name = "RuntimeSprites_WhiteSprite";
        return _white;
    }
}
