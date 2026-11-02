using UnityEngine;
using UnityEngine.UI;

/// <summary>Fades the shared opaque header strip into the illustration without another texture.</summary>
[DisallowMultipleComponent]
public sealed class BattleLoadingVeilFade : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var rect = ((RectTransform)transform).rect;
        UIVertex vertex = default(UIVertex);
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            float alpha = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * alpha);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
