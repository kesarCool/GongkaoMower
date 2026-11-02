using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A compact identity mark. Head socket / renderer bindings also support a future animated body.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BossHeadCrestPresentation : MonoBehaviour, IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [Tooltip("Optional animated head socket. Its position is the top of the head, before the gap below.")]
    [SerializeField] Transform headAnchor;
    [Tooltip("Optional animated body renderer, used when there is no head socket or word label.")]
    [SerializeField] Renderer bodyRenderer;
    [SerializeField] EnemyWordLabel wordLabel;
    [SerializeField] float headGap = .13f;
    [SerializeField] int sortingOrder = 6;

    readonly List<Vector3> vertices = new List<Vector3>(128);
    readonly List<Color32> colors = new List<Color32>(128);
    readonly List<Vector2> uv = new List<Vector2>(128);
    readonly List<int> triangles = new List<int>(192);
    static readonly Vector2[] Crown = {
        new Vector2(-.36f,-.10f), new Vector2(-.43f,.16f),
        new Vector2(-.18f,.035f), new Vector2(0,.27f),
        new Vector2(.18f,.035f), new Vector2(.43f,.16f), new Vector2(.36f,-.10f)
    };
    static readonly Vector2[] DiamondDirections = {Vector2.left,Vector2.down,Vector2.right,Vector2.up};
    EnemyBase enemy;
    Mesh mesh;
    MeshRenderer meshRenderer;
    Matrix4x4 worldToLocal;
    Vector2 origin;
    public bool IsClone => enemy != null && enemy.GetComponent<CloneMarker>() != null && enemy.GetComponent<CloneMarker>().IsConfigured;
    public bool HasVisibleGeometry => meshRenderer != null && meshRenderer.enabled && vertices.Count > 0;
    public Vector2 HeadPosition { get; private set; }
    public int VertexCount => vertices.Count;

    void Awake() { EnsureResources(); }
    void EnsureResources()
    {
        if (mesh != null) return;
        enemy = GetComponentInParent<EnemyBase>();
        if (wordLabel == null && enemy != null) wordLabel = enemy.GetComponentInChildren<EnemyWordLabel>(true);
        mesh = new Mesh { name = "boss_head_crest", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = solidMaterial;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
    }

    /// <summary>May be bound to a Spine head follower without changing BossBrain or combat colliders.</summary>
    public void BindBody(Transform socket, Renderer renderer)
    {
        headAnchor = socket;
        bodyRenderer = renderer;
    }

    public bool TryGetHeadPosition(out Vector2 position)
    {
        position = Vector2.zero;
        if (headAnchor != null)
        {
            if (!headAnchor.gameObject.activeInHierarchy || (bodyRenderer != null && !bodyRenderer.enabled)) return false;
            position = headAnchor.position;
            return true;
        }
        // Explicit body bindings take priority over the transitional TMP body.
        if (bodyRenderer != null)
        {
            if (!bodyRenderer.enabled || !bodyRenderer.gameObject.activeInHierarchy) return false;
            position = new Vector2(bodyRenderer.bounds.center.x, bodyRenderer.bounds.max.y);
            return true;
        }
        TextMeshPro text;
        if (wordLabel == null || !wordLabel.TryGetWorldTextForShatter(out text) || !text.isActiveAndEnabled || !text.renderer.enabled) return false;
        if (text.havePropertiesChanged || text.textInfo.characterCount == 0) text.ForceMeshUpdate();
        bool found = false;
        Vector2 lo = Vector2.zero, hi = Vector2.zero;
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
            var c = text.textInfo.characterInfo[i];
            if (!c.isVisible) continue;
            // All four corners keep the mark above rotated / punched glyphs too.
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 p = text.transform.TransformPoint(new Vector3(corner % 2 == 0 ? c.bottomLeft.x : c.topRight.x, corner < 2 ? c.bottomLeft.y : c.topRight.y, 0));
                if (!found) { lo = hi = p; found = true; }
                else { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            }
        }
        if (found) position = new Vector2((lo.x + hi.x) * .5f, hi.y);
        return found;
    }

    void LateUpdate() { RenderNow(); }
    public void RenderNow()
    {
        EnsureResources();
        Vector2 head;
        if (enemy == null || enemy.Hp <= 0 || !enemy.gameObject.activeInHierarchy || !TryGetHeadPosition(out head)) { Clear(); return; }
        HeadPosition = head;
        origin = head + Vector2.up * (headGap + .10f + Mathf.Sin(Time.time * 2.4f) * .012f);
        worldToLocal = transform.worldToLocalMatrix;
        vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        var clone = enemy.GetComponent<CloneMarker>();
        if (clone != null && clone.IsConfigured)
        {
            float alpha = .64f * Mathf.Lerp(.6f, 1, Mathf.Clamp01((Time.time - clone.BornAt) / .42f));
            if (clone.RemainingSeconds < .32f) alpha *= Mathf.Lerp(.55f, 1, clone.RemainingSeconds / .32f);
            Color dark = new Color(.32f,.025f,.02f,alpha), light = new Color(1,.3f,.13f,alpha);
            Diamond(new Vector2(-.11f,.035f), .13f, .065f, dark);
            Diamond(new Vector2(.11f,.035f), .13f, .065f, dark);
            Diamond(new Vector2(-.11f,.035f), .13f, .025f, light);
            Diamond(new Vector2(.11f,.035f), .13f, .025f, light);
        }
        else
        {
            FillCrown(1, new Color(.25f,.065f,.025f,1));
            FillCrown(.76f, new Color(1,.57f,.16f,1));
            Segment(new Vector2(-.28f,-.065f),new Vector2(.28f,-.065f),.035f,new Color(1,.83f,.4f,1));
            Quad(new Vector2(-.075f,.015f),new Vector2(0,-.06f),new Vector2(.075f,.015f),new Vector2(0,.09f),new Color(.74f,.065f,.025f,1));
            Segment(new Vector2(0,.14f),new Vector2(0,.19f),.035f,new Color(1,.91f,.62f,1));
        }
        mesh.Clear(false);
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        meshRenderer.enabled = vertices.Count > 0 && solidMaterial != null;
    }
    void Vertex(Vector2 p, Color color)
    {
        var world = origin + p;
        vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(world.x,world.y,enemy.transform.position.z - .2f)));
        colors.Add(color); uv.Add(Vector2.zero);
    }
    void FillCrown(float scale, Color color)
    {
        var center = new Vector2(0,-.025f);
        for (int n = 0; n < Crown.Length; n++)
        {
            int i = vertices.Count;
            Vertex(center,color); Vertex(center + (Crown[n]-center)*scale,color); Vertex(center + (Crown[(n+1)%Crown.Length]-center)*scale,color);
            triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2);
        }
    }
    void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int i=vertices.Count; Vertex(a,color); Vertex(b,color); Vertex(c,color); Vertex(d,color);
        triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2); triangles.Add(i); triangles.Add(i+2); triangles.Add(i+3);
    }
    void Segment(Vector2 a,Vector2 b,float width,Color color)
    {
        var direction=(b-a).normalized; var normal=new Vector2(-direction.y,direction.x)*width*.5f;
        Quad(a-normal,b-normal,b+normal,a+normal,color);
    }
    void Diamond(Vector2 center,float radius,float width,Color color)
    {
        for(int i=0;i<4;i++) Segment(center+DiamondDirections[i]*radius,center+DiamondDirections[(i+1)%4]*radius,width,color);
    }
    void Clear() { vertices.Clear(); if(mesh!=null)mesh.Clear(); if(meshRenderer!=null)meshRenderer.enabled=false; }
    public void OnPoolGet() { Clear(); }
    public void OnPoolRelease() { Clear(); }
    void OnDisable() { Clear(); }
    void OnDestroy() { if(mesh!=null)Destroy(mesh); }
}
