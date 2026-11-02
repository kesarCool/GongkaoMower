using UnityEngine;

/// <summary>Translucent coverage with three fixed-orientation hexagonal waves. No authored bitmap required.</summary>
public sealed partial class FieldGeneratorPresentation
{
    [SerializeField] bool surfaceFieldStyle;
    [SerializeField] MeshRenderer surfaceRenderer;
    [SerializeField] MeshRenderer hexWaveRenderer;
    [SerializeField] Color normalFieldColor = new Color(.08f, .90f, .17f, 1);
    [SerializeField] Color breakthroughFieldColor = new Color(1, .43f, .04f, 1);
    [SerializeField, Range(.01f,.4f)] float surfaceOpacity = .27f;
    [SerializeField, Range(.1f,1)] float waveOpacity = .88f;
    [SerializeField, Range(1,3)] float fieldTravelMultiplier = 1.6f;
    const int FieldWaveCount = 3;
    const int FieldSegments = 24;
    static readonly float[] FieldBandRadii = { .90f, .955f, .98f, 1.015f, 1.04f };
    static readonly float[] FieldBandAlpha = { 0, .25f, .90f, .35f, 0 };
    static readonly Vector3[] HexContour = CreateHexContour();
    Mesh runtimeFieldWaveMesh;
    Vector3[] fieldVertices;
    Color32[] fieldColors;
    public bool UsesFieldSurface => surfaceFieldStyle;
    public int HexWaveCount => FieldWaveCount;
    public Mesh RuntimeFieldWaveMesh => runtimeFieldWaveMesh;
    public float FieldTravelSeconds => cycle * fieldTravelMultiplier;

    public void AuthorField(MeshRenderer surface, MeshRenderer waves, MeshRenderer boundary, Color ordinary, Color upgraded, float areaAlpha, float waveAlpha, float travelMultiplier)
    {
        surfaceFieldStyle = true; surfaceRenderer = surface; hexWaveRenderer = waves; boundaryRenderer = boundary;
        fieldOrbitStyle = false;
        flowRenderer = null; pulseRenderer = null;
        normalFieldColor = ordinary; breakthroughFieldColor = upgraded;
        surfaceOpacity = Mathf.Clamp(areaAlpha,.01f,.4f); waveOpacity = Mathf.Clamp01(waveAlpha); fieldTravelMultiplier = Mathf.Clamp(travelMultiplier,1,3);
    }
    void EnsureFieldWaveMesh()
    {
        if (runtimeFieldWaveMesh != null || hexWaveRenderer == null) return;
        var filter = hexWaveRenderer.GetComponent<MeshFilter>();
        runtimeFieldWaveMesh = filter.sharedMesh != null ? Instantiate(filter.sharedMesh) : (UsesFieldOrbit ? CreateFieldOrbitMesh() : CreateFieldWaveMesh());
        runtimeFieldWaveMesh.name = "FieldHexWaves_Runtime"; runtimeFieldWaveMesh.hideFlags = HideFlags.DontSave;
        runtimeFieldWaveMesh.MarkDynamic(); fieldVertices = runtimeFieldWaveMesh.vertices; fieldColors = runtimeFieldWaveMesh.colors32;
        filter.sharedMesh = runtimeFieldWaveMesh;
    }
    void RenderFieldSurface()
    {
        float strength = Mathf.Clamp01((intensity - .5f) / .8f);
        Color tint = breakthrough ? breakthroughFieldColor : normalFieldColor;
        if (surfaceRenderer != null)
        {
            surfaceRenderer.transform.localScale = Vector3.one * radius;
            // Low-opacity centre; most colour lives across the coverage area, never an opaque disk.
            tint.a = Mathf.Clamp01((surfaceOpacity + (UsesFieldOrbit && breakthrough ? breakthroughAreaBoost : 0)) * (.75f + .25f * strength) + contact * .08f);
            SetTint(surfaceRenderer,tint);
        }
        if (boundaryRenderer != null)
        {
            boundaryRenderer.transform.localScale = Vector3.one * radius;
            var edge = Color.Lerp(tint,Color.black,.55f); edge.a = .18f + .04f * strength;
            if (UsesFieldOrbit && breakthrough) { edge = new Color(.9f,.4f,.04f,.4f); }
            SetTint(boundaryRenderer,edge);
        }
        if (hexWaveRenderer == null) return;
        EnsureFieldWaveMesh(); if (runtimeFieldWaveMesh == null) return;
        if (UsesFieldOrbit) UpdateFieldOrbitBuffers(phase,fieldOrbitAngle,breakthrough,tint,strength,fieldVertices,fieldColors);
        else UpdateFieldWaveBuffers(phase,fieldVertices,fieldColors);
        runtimeFieldWaveMesh.vertices = fieldVertices; runtimeFieldWaveMesh.colors32 = fieldColors;
        // Immutable conservative bounds; updating them every frame is unnecessary.
        hexWaveRenderer.transform.localScale = Vector3.one * radius;
        var waveTint = Color.Lerp(tint,Color.white,.10f); waveTint.a = Mathf.Clamp01(waveOpacity * (.85f + .15f * strength) + contact * .10f);
        SetTint(hexWaveRenderer,UsesFieldOrbit ? Color.white : waveTint);
    }
    void OnDestroy() { ReleaseFieldMesh(!Application.isPlaying); }
#if UNITY_EDITOR
    // PreviewRenderUtility can dispose never-awakened behaviours without invoking OnDestroy.
    public void ReleasePreviewResources() { ReleaseFieldMesh(true); }
#endif
    void ReleaseFieldMesh(bool immediate)
    {
        if (runtimeFieldWaveMesh == null) return;
        if (immediate) DestroyImmediate(runtimeFieldWaveMesh); else Destroy(runtimeFieldWaveMesh);
        runtimeFieldWaveMesh = null; fieldVertices = null; fieldColors = null;
    }
    static Vector3[] CreateHexContour()
    {
        var points = new Vector3[FieldSegments+1];
        for (int i=0;i<=FieldSegments;i++)
        {
            float a = i * Mathf.PI * 2 / FieldSegments;
            float sector = Mathf.Repeat(a + Mathf.PI / 6, Mathf.PI / 3) - Mathf.PI / 6;
            float hexRadius = Mathf.Cos(Mathf.PI/6) / Mathf.Cos(sector);
            float bevel = Mathf.Lerp(hexRadius,1,.10f);
            points[i] = new Vector3(Mathf.Cos(a)*bevel,Mathf.Sin(a)*bevel,0);
        }
        return points;
    }
    static void UpdateFieldWaveBuffers(float progress, Vector3[] vertices, Color32[] colors)
    {
        int index=0;
        for (int wave=0;wave<FieldWaveCount;wave++)
        {
            float t = Mathf.Repeat(progress + wave/(float)FieldWaveCount,1);
            float scale = Mathf.Lerp(.14f,.96f,t);
            float envelope = Mathf.Pow(Mathf.Sin(t*Mathf.PI),.65f);
            for (int band=0;band<FieldBandRadii.Length;band++) for (int point=0;point<=FieldSegments;point++)
            {
                vertices[index] = HexContour[point] * (scale*FieldBandRadii[band]);
                colors[index] = new Color32(255,255,255,(byte)Mathf.RoundToInt(255*FieldBandAlpha[band]*envelope)); index++;
            }
        }
    }
    public static Mesh CreateFieldWaveMesh()
    {
        int stride=FieldSegments+1, bands=FieldBandRadii.Length;
        var vertices = new Vector3[FieldWaveCount*bands*stride]; var colors = new Color32[vertices.Length]; var uv = new Vector2[vertices.Length];
        var triangles = new int[FieldWaveCount*(bands-1)*FieldSegments*6]; int n=0;
        UpdateFieldWaveBuffers(.15f,vertices,colors);
        for (int i=0;i<uv.Length;i++) uv[i]=new Vector2(.5f,.5f);
        for (int wave=0;wave<FieldWaveCount;wave++) for (int band=0;band<bands-1;band++) for (int point=0;point<FieldSegments;point++)
        {
            int a=wave*bands*stride+band*stride+point, b=a+stride;
            triangles[n++]=a; triangles[n++]=b; triangles[n++]=a+1; triangles[n++]=a+1; triangles[n++]=b; triangles[n++]=b+1;
        }
        return new Mesh { name="FieldHexWavesV2",vertices=vertices,colors32=colors,uv=uv,triangles=triangles,bounds=new Bounds(Vector3.zero,new Vector3(2,2,.1f)) };
    }
    public static Mesh CreateFieldSurfaceMesh() => CreateFieldRings("FieldCoverageV2",new[] {0f,.26f,.60f,.85f,.95f,1f},new[] {.35f,.55f,.78f,.72f,.25f,0f});
    public static Mesh CreateFieldBoundaryMesh() => CreateFieldRings("FieldRangeV2",new[] {.98f,.991f,.997f,1f},new[] {0f,.65f,.18f,0f});
    static Mesh CreateFieldRings(string meshName,float[] radii,float[] alpha)
    {
        const int segments=48; int stride=segments+1;
        var vertices=new Vector3[radii.Length*stride]; var colors=new Color[vertices.Length]; var uv=new Vector2[vertices.Length];
        var triangles=new int[(radii.Length-1)*segments*6]; int n=0;
        for (int band=0;band<radii.Length;band++) for (int point=0;point<=segments;point++)
        {
            int i=band*stride+point; float a=point*Mathf.PI*2/segments;
            vertices[i]=new Vector3(Mathf.Cos(a)*radii[band],Mathf.Sin(a)*radii[band]); colors[i]=new Color(1,1,1,alpha[band]); uv[i]=new Vector2(.5f,.5f);
            if (band==radii.Length-1 || point==segments) continue; int j=i+stride;
            triangles[n++]=i; triangles[n++]=j; triangles[n++]=i+1; triangles[n++]=i+1; triangles[n++]=j; triangles[n++]=j+1;
        }
        return new Mesh {name=meshName,vertices=vertices,colors=colors,uv=uv,triangles=triangles,bounds=new Bounds(Vector3.zero,new Vector3(2,2,.1f))};
    }
}
