using UnityEngine;
using UnityEngine.UI;

/// <summary>A sideways figure eight. The hand and ribbon share the same parametric path.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialGestureGraphic : MaskableGraphic
{
    const int Segments = 128;
    public static Vector2 PathPoint(float phase, Vector2 size) =>
        new Vector2(Mathf.Sin(phase) * size.x * .5f, Mathf.Sin(phase * 2) * size.y * .5f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Ribbon(vh, 12, new Color32(111, 73, 27, 110));
        Ribbon(vh, 7, new Color32(255, 210, 99, 230));
    }

    void Ribbon(VertexHelper vh, float width, Color tint)
    {
        var rect = rectTransform.rect;
        Vector2 size = rect.size - Vector2.one * 14;
        int start = vh.currentVertCount;
        for (int i = 0; i <= Segments; i++)
        {
            float phase = i * 2 * Mathf.PI / Segments;
            Vector2 point = rect.center + PathPoint(phase, size);
            Vector2 tangent = new Vector2(Mathf.Cos(phase) * size.x * .5f, Mathf.Cos(phase * 2) * size.y);
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * width * .5f;
            vh.AddVert(point - normal, tint, Vector2.zero);
            vh.AddVert(point + normal, tint, Vector2.zero);
            if (i == 0) continue;
            int index = start + i * 2;
            vh.AddTriangle(index - 2, index - 1, index + 1);
            vh.AddTriangle(index - 2, index + 1, index);
        }
    }
}
