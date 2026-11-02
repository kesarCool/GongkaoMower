using UnityEngine;
using UnityEngine.UI;

/// <summary>Small UI-native shapes; no extra textures, shader variants or material instances.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialShapeGraphic : MaskableGraphic
{
    public Color stroke = Color.clear;
    public float border = 3, radius = 20;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        Rounded(vh, rect, radius, stroke);
        rect.xMin += border; rect.yMin += border; rect.xMax -= border; rect.yMax -= border;
        Rounded(vh, rect, Mathf.Max(0, radius - border), color);
    }

    public static void Rounded(VertexHelper vh, Rect rect, float radius, Color color)
    {
        if (rect.width <= 0 || rect.height <= 0 || color.a <= 0) return;
        radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
        int start = vh.currentVertCount;
        vh.AddVert(rect.center, color, Vector2.zero);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
            for (int segment = 0; segment <= 6; segment++)
            {
                float angle = (90 - corner * 90 - segment * 15) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
        }
        const int count = 28;
        for (int i = 0; i < count; i++) vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % count);
    }
}
