using UnityEngine;
using UnityEngine.UI;

/// <summary>A white glove pointing at its local origin. Animated by the overlay with unscaled time.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialHandGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var ink = new Color32(73, 54, 39, 255);
        var white = new Color32(255, 252, 240, 255);
        // First draw the silhouette pieces, then their inset faces to join them as a single glove.
        var pieces = new[] { new Rect(-13, -78, 28, 78), new Rect(8, -84, 23, 45),
            new Rect(27, -87, 23, 41), new Rect(43, -92, 23, 37),
            new Rect(-13, -114, 79, 61), new Rect(-30, -100, 30, 48) };
        foreach (var part in pieces) TutorialShapeGraphic.Rounded(vh, part, 13, ink);
        foreach (var part in pieces)
        {
            var inset = Rect.MinMaxRect(part.xMin + 4, part.yMin + 4, part.xMax - 4, part.yMax - 4);
            TutorialShapeGraphic.Rounded(vh, inset, 10, white);
        }
        TutorialShapeGraphic.Rounded(vh, new Rect(-7, -125, 66, 17), 5, ink);
        TutorialShapeGraphic.Rounded(vh, new Rect(-3, -121, 58, 9), 3, new Color32(255, 199, 72, 255));
    }
}
