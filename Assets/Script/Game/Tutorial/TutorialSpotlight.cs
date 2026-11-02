using UnityEngine;
using UnityEngine.UI;

/// <summary>Four solid quads leave one aperture; the same rectangle controls input passthrough.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialSpotlight : MaskableGraphic, ICanvasRaycastFilter
{
    public Rect Hole { get; private set; }
    bool hasHole;

    public void SetHole(Rect hole, bool visible, bool blocksInput, float alpha)
    {
        raycastTarget = blocksInput;
        var shade = new Color(.10f, .075f, .05f, alpha);
        if (Hole == hole && hasHole == visible && color == shade) return;
        Hole = hole; hasHole = visible; color = shade;
        SetVerticesDirty();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        Vector2 point;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out point);
        return !hasHole || !Hole.Contains(point);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var bounds = rectTransform.rect;
        if (!hasHole) { Quad(vh, bounds, color); return; }
        Quad(vh, Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, Hole.yMin), color);
        Quad(vh, Rect.MinMaxRect(bounds.xMin, Hole.yMax, bounds.xMax, bounds.yMax), color);
        Quad(vh, Rect.MinMaxRect(bounds.xMin, Hole.yMin, Hole.xMin, Hole.yMax), color);
        Quad(vh, Rect.MinMaxRect(Hole.xMax, Hole.yMin, bounds.xMax, Hole.yMax), color);
        Color rim = new Color32(255, 208, 89, 255);
        Quad(vh, new Rect(Hole.xMin, Hole.yMin, Hole.width, 4), rim);
        Quad(vh, new Rect(Hole.xMin, Hole.yMax - 4, Hole.width, 4), rim);
        Quad(vh, new Rect(Hole.xMin, Hole.yMin, 4, Hole.height), rim);
        Quad(vh, new Rect(Hole.xMax - 4, Hole.yMin, 4, Hole.height), rim);
    }

    static void Quad(VertexHelper vh, Rect rect, Color color)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        int first = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
