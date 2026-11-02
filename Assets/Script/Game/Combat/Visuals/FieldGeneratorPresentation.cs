using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Persistent, skill-driven aura. Rendering only; no Update, collisions or damage scheduling.</summary>
[DisallowMultipleComponent]
public sealed partial class FieldGeneratorPresentation : MonoBehaviour
{
    [SerializeField] Sprite normalAuraSprite;
    [SerializeField] Sprite breakthroughAuraSprite;
    [SerializeField] SpriteRenderer flowRenderer;
    [SerializeField] MeshRenderer boundaryRenderer;
    [SerializeField] MeshRenderer pulseRenderer;
    [SerializeField] float normalRotationSpeed = 32f;
    [SerializeField] float breakthroughRotationSpeed = 49f;

    float radius = 1, intensity = 1, cycle = 1.2f, artworkDiameter = .875f;
    float phase, angle, contact;
    bool breakthrough;
    MaterialPropertyBlock block;
    static Mesh fallbackMesh;
    static Material fallbackMaterial;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    public bool IsBreakthrough => breakthrough;
    public float AnimationPhase => phase;
    public float VisualRadius => radius;
    public Sprite CurrentSprite => flowRenderer != null ? flowRenderer.sprite : null;

    public void Author(Sprite normal, Sprite burst, SpriteRenderer flow, MeshRenderer boundary, MeshRenderer pulse, float normalSpeed = 32, float breakthroughSpeed = 49)
    {
        surfaceFieldStyle = false;
        normalAuraSprite = normal; breakthroughAuraSprite = burst; flowRenderer = flow; boundaryRenderer = boundary; pulseRenderer = pulse;
        normalRotationSpeed = normalSpeed; breakthroughRotationSpeed = breakthroughSpeed;
    }
    public void Configure(float newRadius, float newIntensity, float newCycle, bool isBreakthrough, int order, float diameter)
    {
        radius = Mathf.Max(.3f, newRadius); intensity = Mathf.Clamp(newIntensity, .1f, 1.5f);
        cycle = Mathf.Max(.1f, newCycle); breakthrough = isBreakthrough; artworkDiameter = Mathf.Max(.01f, diameter);
        if (block == null) block = new MaterialPropertyBlock();
        if (flowRenderer != null)
        {
            flowRenderer.sprite = breakthrough && breakthroughAuraSprite != null ? breakthroughAuraSprite : normalAuraSprite;
            flowRenderer.enabled = flowRenderer.sprite != null; flowRenderer.sortingOrder = order;
        }
        if (boundaryRenderer != null) boundaryRenderer.sortingOrder = order;
        if (pulseRenderer != null) pulseRenderer.sortingOrder = order;
        if (surfaceRenderer != null) surfaceRenderer.sortingOrder = order;
        if (hexWaveRenderer != null) hexWaveRenderer.sortingOrder = order;
        RenderState();
    }
    public void NotifyDamageTick() { contact = .18f; }
    public void TickVisual(float deltaTime)
    {
        if (deltaTime > 0)
        {
            phase = Mathf.Repeat(phase + deltaTime / (cycle * (surfaceFieldStyle ? fieldTravelMultiplier : 1)), 1);
            if (!surfaceFieldStyle) angle = Mathf.Repeat(angle - deltaTime * (breakthrough ? breakthroughRotationSpeed : normalRotationSpeed), 360);
            if (UsesFieldOrbit) fieldOrbitAngle = Mathf.Repeat(fieldOrbitAngle + deltaTime * FieldOrbitDegreesPerSecond, 360);
            contact = Mathf.Max(0, contact - deltaTime * 1.8f);
        }
        RenderState();
    }
#if UNITY_EDITOR
    public void SamplePreview(float seconds)
    {
        phase = Mathf.Repeat(seconds / (cycle * (surfaceFieldStyle ? fieldTravelMultiplier : 1)), 1); angle = Mathf.Repeat(-seconds * (breakthrough ? breakthroughRotationSpeed : normalRotationSpeed), 360);
        fieldOrbitAngle = Mathf.Repeat(seconds * FieldOrbitDegreesPerSecond,360); contact = 0; RenderState();
    }
#endif
    void RenderState()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (surfaceFieldStyle) { RenderFieldSurface(); return; }
        float strength = Mathf.Clamp01((intensity - .5f) / .8f);
        float breathe = .5f + .5f * Mathf.Sin(phase * Mathf.PI * 2);
        if (flowRenderer != null)
        {
            // Artwork's visible outer diameter is measured by the builder, not the sprite's padded rect.
            float scale = radius * 2 / artworkDiameter * (.94f + .025f * breathe);
            flowRenderer.transform.localScale = Vector3.one * scale;
            flowRenderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
            flowRenderer.color = new Color(1, 1, 1, Mathf.Clamp01(.78f + .13f * strength + contact));
        }
        Color tint = breakthrough ? new Color(1, .25f, .09f) : new Color(1, .70f, .20f);
        if (boundaryRenderer != null)
        {
            boundaryRenderer.transform.localScale = Vector3.one * radius;
            tint.a = Mathf.Clamp01(.73f + .12f * strength + contact); SetTint(boundaryRenderer, tint);
        }
        if (pulseRenderer != null)
        {
            float t = Mathf.SmoothStep(0, 1, phase);
            pulseRenderer.transform.localScale = Vector3.one * (radius * Mathf.Lerp(.24f, .98f, t));
            tint.a = Mathf.Sin(phase * Mathf.PI) * (.42f + .12f * strength);
            SetTint(pulseRenderer, tint);
        }
    }
    void SetTint(Renderer renderer, Color color) { block.SetColor(ColorId, color); renderer.SetPropertyBlock(block); }

    public void Follow(Transform owner)
    {
        transform.position = owner.position; transform.rotation = Quaternion.identity;
        if (transform.parent != null)
        {
            Vector3 s = transform.parent.lossyScale;
            transform.localScale = new Vector3(Mathf.Abs(s.x) > .001f ? 1 / s.x : 1, Mathf.Abs(s.y) > .001f ? 1 / s.y : 1, 1);
        }
    }

    public static FieldGeneratorPresentation CreateFallback(Transform owner)
    {
        if (fallbackMesh == null) { fallbackMesh = CreateRingMesh(); fallbackMesh.hideFlags = HideFlags.HideAndDontSave; }
        if (fallbackMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default"); if (shader == null) return null;
            fallbackMaterial = new Material(shader) { name = "FieldAuraFallback", hideFlags = HideFlags.HideAndDontSave };
        }
        var root = new GameObject("FieldGeneratorRoot"); root.transform.SetParent(owner, false);
        var boundary = MakeRing(root.transform, "Boundary", fallbackMesh, fallbackMaterial);
        var pulse = MakeRing(root.transform, "Pulse", fallbackMesh, fallbackMaterial);
        var view = root.AddComponent<FieldGeneratorPresentation>(); view.Author(null, null, null, boundary, pulse); return view;
    }
    public static MeshRenderer MakeRing(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
    }
    public static Mesh CreateRingMesh()
    {
        const int segments = 48;
        float[] radii = { .927f, .938f, .949f, .975f, .988f, 1 };
        Color[] colors = { new Color(.3f,.2f,.1f,0), new Color(.32f,.19f,.1f,.85f), new Color(1,.96f,.82f,1), new Color(1,.86f,.52f,1), new Color(.35f,.22f,.12f,.9f), new Color(.3f,.2f,.1f,0) };
        var vertices = new Vector3[(segments + 1) * radii.Length]; var vertexColors = new Color[vertices.Length]; var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * (radii.Length - 1) * 6]; int n = 0;
        for (int band=0;band<radii.Length;band++) for (int s=0;s<=segments;s++)
        {
            float a = s * Mathf.PI * 2 / segments; int i = band * (segments + 1) + s;
            vertices[i] = new Vector3(Mathf.Cos(a) * radii[band], Mathf.Sin(a) * radii[band]); vertexColors[i] = colors[band]; uv[i] = new Vector2(.5f,.5f);
            if (band == radii.Length-1 || s == segments) continue;
            int j = i + segments + 1;
            triangles[n++] = i; triangles[n++] = j; triangles[n++] = i+1;
            triangles[n++] = i+1; triangles[n++] = j; triangles[n++] = j+1;
        }
        var mesh = new Mesh { name = "FieldAuraRing48", vertices = vertices, colors = vertexColors, uv = uv, triangles = triangles };
        mesh.RecalculateBounds(); return mesh;
    }
}
