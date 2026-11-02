using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Immediate directional knife release flash, anchored to visible glyphs. Does not delay or move a damaging projectile.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BossKnifePresentation : MonoBehaviour, IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] int sortingOrder = 5;
    [SerializeField] float flashSeconds = .12f;
    readonly List<Vector3> vertices = new List<Vector3>(32);
    readonly List<Color32> colors = new List<Color32>(32);
    readonly List<Vector2> uv = new List<Vector2>(32);
    readonly List<int> triangles = new List<int>(48);
    Mesh mesh;
    MeshRenderer meshRenderer;
    EnemyWordLabel label;
    EnemyBase enemy;
    Transform pointAnchor;
    Vector3 localPoint;
    Vector2 fallbackPoint;
    Vector2 direction = Vector2.right;
    float began, lastTime = float.NegativeInfinity;
    Matrix4x4 lastMatrix;
    Vector2 lastOrigin;
    bool active, dirty;
    public int Phase => active ? 1 : 0;
    public Vector2 Direction => direction;
    public Vector2 FlashOrigin => pointAnchor != null ? (Vector2)pointAnchor.TransformPoint(localPoint) : fallbackPoint;
    public int MeshBuildCount { get; private set; }
    public bool HasVisibleGeometry => meshRenderer != null && meshRenderer.enabled && vertices.Count > 0;
    public MeshRenderer Renderer => meshRenderer;

    void Awake() { EnsureResources(); }
    void EnsureResources()
    {
        if (mesh != null) return;
        enemy = GetComponentInParent<EnemyBase>();
        label = enemy != null ? enemy.GetComponentInChildren<EnemyWordLabel>(true) : GetComponentInParent<EnemyWordLabel>();
        mesh = new Mesh { name = "boss_knife_release_flash", hideFlags = HideFlags.DontSave };
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
    public void Flash(Vector2 shotDirection)
    {
        EnsureResources();
        direction = shotDirection.sqrMagnitude > .0001f ? shotDirection.normalized : Vector2.right;
        fallbackPoint = enemy != null ? (Vector2)enemy.transform.position : (Vector2)transform.position;
        pointAnchor = null;
        localPoint = Vector3.zero;
        // Query the front visible glyph, independent of the physics collider and real spawn position.
        if (label != null) label.TryGetHitAnchor(fallbackPoint + direction * 100f, out pointAnchor, out localPoint);
        active = true; began = Time.time; dirty = true;
        RenderNow();
    }
    public void Cancel()
    {
        active = false; pointAnchor = null; dirty = true;
        vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        if (mesh != null) mesh.Clear();
        if (meshRenderer != null) meshRenderer.enabled = false;
    }
    public void OnPoolGet() { Cancel(); }
    public void OnPoolRelease() { Cancel(); }
    void OnDisable() { Cancel(); }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
    void LateUpdate()
    {
        if (!active) return;
        if ((enemy != null && enemy.Hp <= 0f) || Time.time - began >= flashSeconds) { Cancel(); return; }
        RenderNow();
    }
    public void RenderNow()
    {
        EnsureResources(); if (!active) return;
        Vector2 origin = FlashOrigin;
        Matrix4x4 matrix = transform.worldToLocalMatrix;
        float now = Time.time;
        if (!dirty && lastTime == now && lastMatrix == matrix && lastOrigin == origin) return;
        dirty = false; lastTime = now; lastMatrix = matrix; lastOrigin = origin;
        vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        float t = Mathf.Clamp01((now - began) / Mathf.Max(.01f, flashSeconds)), fade = 1f - t;
        Vector2 side = new Vector2(-direction.y, direction.x);
        origin += direction * .045f;
        // A compact deep-red dart and two red strokes make the release readable on bright terrain.
        Diamond(origin + direction * (.09f + .14f * t), direction, .42f + .12f * t, .22f * fade,
            new Color(.48f, .025f, .02f, fade * .9f), matrix);
        Diamond(origin + direction * (.10f + .14f * t), direction, .3f + .1f * t, .11f * fade,
            new Color(1f, .31f, .055f, fade), matrix);
        Diamond(origin + direction * .08f, direction, .12f, .055f * fade,
            new Color(1f, .94f, .65f, fade), matrix);
        Segment(origin - direction * .13f + side * .16f, origin + direction * .02f + side * .045f, .035f * fade,
            new Color(.88f, .08f, .025f, fade * .85f), matrix);
        Segment(origin - direction * .13f - side * .16f, origin + direction * .02f - side * .045f, .035f * fade,
            new Color(.88f, .08f, .025f, fade * .85f), matrix);
        mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); MeshBuildCount++;
        meshRenderer.enabled = solidMaterial != null;
    }
    void Vertex(Vector2 world, Color color, Matrix4x4 matrix)
    {
        vertices.Add(matrix.MultiplyPoint3x4(new Vector3(world.x, world.y, -.15f)));
        colors.Add(color); uv.Add(Vector2.zero);
    }
    void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, Matrix4x4 matrix)
    {
        int i = vertices.Count; Vertex(a, color, matrix); Vertex(b, color, matrix); Vertex(c, color, matrix); Vertex(d, color, matrix);
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
    }
    void Diamond(Vector2 center, Vector2 along, float length, float width, Color color, Matrix4x4 matrix)
    {
        Vector2 side = new Vector2(-along.y, along.x) * width * .5f;
        Quad(center - along * length * .5f, center - side, center + along * length * .5f, center + side, color, matrix);
    }
    void Segment(Vector2 a, Vector2 b, float width, Color color, Matrix4x4 matrix)
    {
        Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x) * width * .5f;
        Quad(a - side, b - side, b + side, a + side, color, matrix);
    }
}
