using UnityEngine;
using UnityEngine.UI;

/// <summary>One UGUI mesh for all slanted energy segments; no extra texture or font.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EnergySegmentGraphic : MaskableGraphic
{
    [Range(0, 1)] public float progress;
    public int segmentCount = 20;
    public float flash;
    public void SetProgress(float value, float pulse)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(progress - value) < .0001f && Mathf.Abs(flash - pulse) < .001f) return;
        progress = value; flash = pulse; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        int count = Mathf.Clamp(segmentCount, 1, 32);
        float step = r.width / count, gap = Mathf.Min(5, step * .16f), slant = Mathf.Min(10, step * .28f);
        for (int i = 0; i < count; i++)
        {
            float left = r.xMin + i * step, width = step - gap;
            Quad(vh, left, r.yMin, width, r.height, slant, new Color32(69, 91, 79, 255));
            float fill = Mathf.Clamp01(progress * count - i);
            if (fill <= 0) continue;
            Color green = Color.Lerp(new Color32(47, 190, 83, 255), new Color32(167, 239, 107, 255), flash);
            Quad(vh, left, r.yMin, width * fill, r.height, slant * fill, green);
            Quad(vh, left + slant * .65f, r.yMin + r.height * .68f, Mathf.Max(0, (width - slant * .7f) * fill), r.height * .19f, 0, new Color( .75f, 1f, .55f, .7f));
        }
    }
    private static void Quad(VertexHelper vh, float x, float y, float w, float h, float slant, Color color)
    {
        if (w <= 0) return;
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(x, y), color, Vector2.zero);
        vh.AddVert(new Vector3(x + w - slant, y), color, Vector2.zero);
        vh.AddVert(new Vector3(x + w, y + h), color, Vector2.zero);
        vh.AddVert(new Vector3(x + slant, y + h), color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
