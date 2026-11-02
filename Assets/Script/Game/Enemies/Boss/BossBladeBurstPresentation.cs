using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Directional preview and release ring. Contains no damage, targeting, random calls or generated textures.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class BossBladeBurstPresentation : MonoBehaviour
{
    [SerializeField] Sprite bladeSprite;
    [SerializeField] Material solidMaterial;
    [SerializeField] Material bladeMaterial;
    [SerializeField] int sortingOrder=4;
    readonly List<Vector3> vertices=new List<Vector3>(768);
    readonly List<Color32> colors=new List<Color32>(768);
    readonly List<Vector2> uv=new List<Vector2>(768);
    readonly List<int> solidTriangles=new List<int>(1536),bladeTriangles=new List<int>(192);
    Mesh mesh;MeshRenderer meshRenderer;Rigidbody2D body;EnemyBase enemy;BossBrain brain;
    int phase,count,chargeToken;
    float phaseAt,chargeSeconds,lastTime=float.NegativeInfinity;
    Vector2 releaseOrigin;
    Matrix4x4 worldToLocal,lastMatrix;
    bool dirty=true;
    public int Phase=>phase;
    public int MarkerCount=>phase==1?count:0;
    public int ChargeToken=>chargeToken;
    public int MeshBuildCount{get;private set;}
    public int RendererCount=>meshRenderer!=null?1:0;
    public bool HasVisibleGeometry=>meshRenderer!=null && meshRenderer.enabled && vertices.Count>0;
    public MeshRenderer Renderer=>meshRenderer;

    void Awake() { EnsureResources(); }
    void EnsureResources()
    {
        if(mesh!=null)return;
        body=GetComponentInParent<Rigidbody2D>();enemy=GetComponentInParent<EnemyBase>();brain=GetComponentInParent<BossBrain>();
        mesh=new Mesh {name="boss_blade_burst_preview",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh=mesh;meshRenderer=GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterials=new[]{solidMaterial,bladeMaterial};meshRenderer.sortingOrder=sortingOrder;
        meshRenderer.shadowCastingMode=ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
        meshRenderer.lightProbeUsage=LightProbeUsage.Off;meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;meshRenderer.enabled=false;
    }
    public void BeginCharge(int bladeCount,float seconds)
    {
        EnsureResources();chargeToken++;count=Mathf.Max(2,bladeCount);chargeSeconds=Mathf.Max(.01f,seconds);phase=1;phaseAt=Time.time;dirty=true;RenderNow();
    }
    public void Commit(Vector2 worldOrigin)
    {
        EnsureResources();releaseOrigin=worldOrigin;phase=2;phaseAt=Time.time;dirty=true;RenderNow();
    }
    public void Cancel()
    {
        chargeToken++;phase=0;count=0;dirty=true;
        vertices.Clear();colors.Clear();uv.Clear();solidTriangles.Clear();bladeTriangles.Clear();
        if(mesh!=null)mesh.Clear();if(meshRenderer!=null)meshRenderer.enabled=false;
    }
    void OnDisable()
    {
        bool active=phase!=0;Cancel();
        if(active && brain!=null)brain.IsBusy=false;
    }
    void OnDestroy() { if(mesh!=null)Destroy(mesh); }
    void LateUpdate()
    {
        if(phase==0)return;
        if(enemy!=null && enemy.Hp<=0f) {Cancel();if(brain!=null)brain.IsBusy=false;return;}
        if(phase==2 && Time.time-phaseAt>=.20f) {Cancel();return;}
        RenderNow();
    }
    public void RenderNow()
    {
        EnsureResources();if(phase==0)return;
        Matrix4x4 next=transform.worldToLocalMatrix;float now=Time.time;
        if(!dirty && now==lastTime && next==lastMatrix)return;
        dirty=false;lastTime=now;lastMatrix=next;worldToLocal=next;
        vertices.Clear();colors.Clear();uv.Clear();solidTriangles.Clear();bladeTriangles.Clear();
        Vector2 center=phase==1?(body!=null?body.position:(Vector2)transform.parent.position):releaseOrigin;
        if(phase==1)DrawCharge(center,Mathf.Clamp01((now-phaseAt)/chargeSeconds));
        else DrawRelease(center,Mathf.Clamp01((now-phaseAt)/.20f));
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
        mesh.SetTriangles(solidTriangles,0);mesh.SetTriangles(bladeTriangles,1);mesh.RecalculateBounds();MeshBuildCount++;
        meshRenderer.enabled=vertices.Count>0 && solidMaterial!=null && bladeMaterial!=null;
    }
    void DrawCharge(Vector2 center,float progress)
    {
        float radius=Mathf.Lerp(1.65f,1.18f,progress),alpha=Mathf.Lerp(.32f,.9f,progress);
        var red=new Color(.65f,.055f,.025f,alpha*.8f);var warm=new Color(1,.46f,.12f,alpha);
        Arc(center,radius*.72f,.027f,0,360,red,48);
        for(int i=0;i<count;i++)
        {
            float angle=i*360f/count,rad=angle*Mathf.Deg2Rad;Vector2 direction=new Vector2(Mathf.Cos(rad),Mathf.Sin(rad));
            Arc(center,radius,.035f,angle-5,angle+5,warm,3);
            Segment(center+direction*radius*.76f,center+direction*(radius+.17f),.035f,red);
            Blade(center+direction*(radius+.17f),direction,.62f,.32f,new Color(1,1,1,alpha));
        }
        Arc(center,radius*.65f,.024f,-90,-90+360*progress,new Color(1,.72f,.24f,alpha*.65f),48);
    }
    void DrawRelease(Vector2 center,float progress)
    {
        float fade=1-progress,r=Mathf.Lerp(.65f,1.65f,progress);
        Arc(center,r,.05f*fade,0,360,new Color(1,.35f,.055f,fade*.65f),48);
        Arc(center,r*.86f,.018f*fade,0,360,new Color(1,.79f,.33f,fade*.75f),48);
        for(int i=0;i<count;i++)
        {
            float rad=i*Mathf.PI*2/count;Vector2 direction=new Vector2(Mathf.Cos(rad),Mathf.Sin(rad));
            Segment(center+direction*r*.75f,center+direction*(r+.14f),.025f*fade,new Color(.75f,.075f,.025f,fade*.7f));
        }
    }
    void AddVertex(Vector2 world,Vector2 tex,Color color)
    {vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(world.x,world.y,-.15f)));uv.Add(tex);colors.Add(color);}
    void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color,List<int> indices,Vector2 uv0,Vector2 uv1)
    {
        int start=vertices.Count;AddVertex(a,new Vector2(uv0.x,uv0.y),color);AddVertex(b,new Vector2(uv1.x,uv0.y),color);AddVertex(c,new Vector2(uv1.x,uv1.y),color);AddVertex(d,new Vector2(uv0.x,uv1.y),color);
        indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
    }
    void Segment(Vector2 a,Vector2 b,float width,Color color)
    {
        Vector2 direction=(b-a).normalized,n=new Vector2(-direction.y,direction.x)*width*.5f;
        Quad(a-n,b-n,b+n,a+n,color,solidTriangles,Vector2.zero,Vector2.one);
    }
    void Arc(Vector2 center,float radius,float width,float from,float to,Color color,int segments)
    {
        for(int i=0;i<segments;i++)
        {
            float a=Mathf.Lerp(from,to,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/segments)*Mathf.Deg2Rad;
            Segment(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);
        }
    }
    void Blade(Vector2 center,Vector2 direction,float length,float width,Color color)
    {
        if(bladeSprite==null)return;
        Vector2 along=direction*length*.5f,side=new Vector2(-direction.y,direction.x)*width*.5f;Rect rect=bladeSprite.rect;
        Vector2 min=new Vector2(rect.xMin/bladeSprite.texture.width,rect.yMin/bladeSprite.texture.height),max=new Vector2(rect.xMax/bladeSprite.texture.width,rect.yMax/bladeSprite.texture.height);
        Quad(center-along-side,center+along-side,center+along+side,center-along+side,color,bladeTriangles,min,max);
    }
}
