using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>World-space danger circles and deterministic fracture geometry. No damage, RNG, textures or per-zone objects.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class BossZonePresentation : MonoBehaviour,IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] int sortingOrder=2;
    [SerializeField] float residueSeconds=.9f;
    struct Zone {public Vector2 center;public float radius,at,duration;public bool active,impact;public int seed;}
    public const int Capacity=32;
    readonly Zone[] zones=new Zone[Capacity];
    List<Vector3> vertices;
    List<Color32> colors;
    List<Vector2> uv;
    List<int> triangles;
    Mesh mesh;MeshRenderer renderer_;Matrix4x4 toLocal,lastMatrix;
    float lastTime=float.NegativeInfinity;bool dirty,hasZones;
    public int MeshBuildCount {get;private set;}
    public bool Ready=>isActiveAndEnabled&&solidMaterial!=null;
    public bool HasVisibleGeometry=>renderer_!=null&&renderer_.enabled&&vertices!=null&&vertices.Count>0;
    public int WarningCount {get {int n=0;for(int i=0;i<Capacity;i++)if(zones[i].active&&!zones[i].impact)n++;return n;}}
    public bool HasWarningAt(Vector3 point,float radius,float tolerance)
    {for(int i=0;i<Capacity;i++)if(zones[i].active&&!zones[i].impact&&Vector2.Distance(point,zones[i].center)<=tolerance&&Mathf.Abs(radius-zones[i].radius)<=tolerance)return true;return false;}
    void Awake(){renderer_=GetComponent<MeshRenderer>();renderer_.enabled=false;}
    void EnsureMesh()
    {
        if(mesh!=null)return;
        vertices=new List<Vector3>(4096);colors=new List<Color32>(4096);uv=new List<Vector2>(4096);triangles=new List<int>(6144);
        mesh=new Mesh{name="boss_zone_world_geometry",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;
        renderer_=GetComponent<MeshRenderer>();renderer_.sharedMaterial=solidMaterial;renderer_.sortingOrder=sortingOrder;
        renderer_.shadowCastingMode=ShadowCastingMode.Off;renderer_.receiveShadows=false;renderer_.lightProbeUsage=LightProbeUsage.Off;renderer_.reflectionProbeUsage=ReflectionProbeUsage.Off;
    }
    public void Warn(int index,Vector2 center,float radius,float seconds)
    {
        if(index<0||index>=Capacity)return;EnsureMesh();zones[index]=new Zone{active=true,center=center,radius=radius,at=Time.time,duration=seconds,seed=index};dirty=true;hasZones=true;RenderNow();
    }
    public void Impact(int index)
    {if(index<0||index>=Capacity||!zones[index].active)return;var z=zones[index];z.impact=true;z.at=Time.time;zones[index]=z;dirty=true;RenderNow();}
    public void CancelAll()
    {for(int i=0;i<Capacity;i++)zones[i].active=false;hasZones=false;vertices?.Clear();if(mesh!=null)mesh.Clear();if(renderer_!=null)renderer_.enabled=false;dirty=true;}
    void LateUpdate(){if(hasZones)RenderNow();}
    public void RenderNow()
    {
        if(mesh==null||!hasZones)return;var matrix=transform.worldToLocalMatrix;
        if(!dirty&&Time.time==lastTime&&matrix==lastMatrix)return;
        dirty=false;lastTime=Time.time;lastMatrix=matrix;toLocal=matrix;
        vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();
        for(int i=0;i<Capacity;i++)
        {
            var z=zones[i];if(!z.active)continue;
            if(z.impact){float u=(Time.time-z.at)/residueSeconds;if(u>=1){zones[i].active=false;continue;}Fracture(z,i,u);}
            else Warning(z,Mathf.Clamp01((Time.time-z.at)/z.duration));
        }
        if(vertices.Count==0){hasZones=false;mesh.Clear();renderer_.enabled=false;return;}
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();renderer_.enabled=Ready;MeshBuildCount++;
    }
    static Color Tint(float r,float g,float b,float a)=>new Color(r,g,b,Mathf.Clamp01(a));
    static Vector2 Direction(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
    void Warning(Zone z,float u)
    {
        // Outer edge is exactly the damage radius; the countdown and ticks stay inside it.
        Disk(z.center,z.radius,Tint(.45f,.015f,.01f,.13f+u*.04f));
        Ring(z.center,z.radius,.09f,Tint(.24f,.012f,.008f,.97f));
        Ring(z.center,z.radius-.025f,.035f,Tint(1,.18f,.025f,.95f));
        Ring(z.center,Mathf.Lerp(z.radius*.92f,.08f,u),.032f,Tint(1,.5f,.11f,.75f));
        for(int i=0;i<8;i++){var d=Direction(i*45);Segment(z.center+d*z.radius*.89f,z.center+d*z.radius*.98f,.035f,Tint(1,.56f,.16f,.95f));}
        if(u>.55f)Cracks(z,3,.018f,Tint(.29f,.018f,.01f,(u-.55f)*.85f),.7f);
        var c=z.center;float size=.12f;Segment(c-Vector2.right*size,c+Vector2.right*size,.045f,Tint(1,.38f,.06f,.85f));Segment(c-Vector2.up*size,c+Vector2.up*size,.045f,Tint(1,.38f,.06f,.85f));
    }
    void Fracture(Zone z,int index,float u)
    {
        float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,u)),open=Mathf.Min(1,u*6+.1f);
        Disk(z.center,z.radius*.83f,Tint(.25f,.045f,.015f,.18f*fade));
        Cracks(z,6,.135f,Tint(.19f,.018f,.008f,.95f*fade),open);
        Cracks(z,6,.065f,Tint(.95f,.19f,.018f,fade),open);
        Cracks(z,6,.018f,Tint(1,.73f,.25f,fade*Mathf.Max(.15f,1-u*1.5f)),open);
        float burst=Mathf.Clamp01(u*3.4f);
        if(burst<1){Ring(z.center,z.radius*(.25f+burst*.8f),.065f*(1-burst),Tint(1,.48f,.1f,(1-burst)*.85f));Disk(z.center,z.radius*.25f*(1-burst),Tint(1,.87f,.5f,(1-burst)*.7f));}
        for(int i=0;i<8;i++)
        {
            float angle=i*45+index*23;Vector2 d=Direction(angle),side=new Vector2(-d.y,d.x);
            float launch=Mathf.Min(1,u*2.7f),height=Mathf.Sin(launch*Mathf.PI)*z.radius*.21f;
            Vector2 p=z.center+d*z.radius*(.3f+launch*.54f)+Vector2.up*height;float s=z.radius*(.06f+(i%3)*.013f)*(1-u*.4f);
            Quad(p-d*s-side*s*.6f,p+d*s*.8f-side*s*.65f,p+d*s*.6f+side*s*.6f,p-d*s*.85f+side*s*.45f,Tint(.28f,.055f,.016f,fade));
            Segment(p-d*s+side*s*.45f,p+d*s*.6f+side*s*.6f,.028f,Tint(1,.48f,.12f,fade*.8f));
            if(u<.55f){var dust=z.center+d*z.radius*(.6f+u*.6f);Disk(dust,z.radius*.055f,Tint(.7f,.28f,.045f,(1-u/.55f)*.23f),8);}
        }
    }
    void Cracks(Zone z,int arms,float width,Color color,float open)
    {
        Vector2 d=Direction(17+z.seed*31),side=new Vector2(-d.y,d.x);
        float r=z.radius*open;Vector2 a=z.center-d*r*.91f,b=z.center-d*r*.43f+side*r*.12f,c=z.center-side*r*.08f,e=z.center+d*r*.46f+side*r*.17f,f=z.center+d*r*.91f-side*r*.035f;
        Segment(a,b,width*.52f,color);Segment(b,c,width*.9f,color);Segment(c,e,width,color);Segment(e,f,width*.52f,color);
        for(int arm=0;arm<arms;arm++)
        {
            float t=(arm+.6f)/(arms+.3f),sign=arm%2==0?1:-1;Vector2 root=Vector2.Lerp(b,e,t),branch=(side*sign+d*(.25f*Mathf.Sin(arm*2.3f+z.seed))).normalized;
            Vector2 joint=root+branch*r*(.2f+(arm%3)*.04f),tip=joint+branch*r*.2f+d*r*(arm%2==0?.09f:-.12f);
            Segment(root,joint,width*.66f,color);Segment(joint,tip,width*.32f,color);
            if(arm%2==0)Segment(joint,joint+d*r*.17f+side*sign*r*.05f,width*.23f,color);
        }
    }
    void Vertex(Vector2 p,Color c){vertices.Add(toLocal.MultiplyPoint3x4(new Vector3(p.x,p.y,-.14f)));colors.Add(c);uv.Add(Vector2.zero);}
    void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint){int n=vertices.Count;Vertex(a,tint);Vertex(b,tint);Vertex(c,tint);Vertex(d,tint);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);}
    void Segment(Vector2 a,Vector2 b,float width,Color tint){var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Quad(a-n,b-n,b+n,a+n,tint);}
    void Ring(Vector2 center,float radius,float width,Color tint)
    {if(radius<=0||width<=0)return;float inner=Mathf.Max(0,radius-width);for(int i=0;i<48;i++){Vector2 a=Direction(i*7.5f),b=Direction((i+1)*7.5f);Quad(center+a*inner,center+a*radius,center+b*radius,center+b*inner,tint);}}
    void Disk(Vector2 c,float r,Color tint,int steps=32)
    {if(r<=0)return;for(int i=0;i<steps;i++){int n=vertices.Count;Vertex(c,tint);Vertex(c+Direction(i*360f/steps)*r,tint);Vertex(c+Direction((i+1)*360f/steps)*r,tint);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}}
    public void OnPoolGet(){CancelAll();}
    public void OnPoolRelease(){CancelAll();}
    void OnDisable(){CancelAll();}
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
